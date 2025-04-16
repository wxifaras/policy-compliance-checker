using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using PolicyComplianceCheckerApi.Models;
using System;
using System.Text;

namespace PolicyComplianceCheckerApi.Services;

public class PolicyCheckerService : IPolicyCheckerService
{
    private ILogger<PolicyCheckerService> _logger;
    private IAzureOpenAIService _azureOpenAIService;
    private readonly IAzureStorageService _azureStorageService;
    private readonly TiktokenTokenizer _tokenizer;
    private readonly DocumentIntelligenceClient _documentIntelligenceClient;
    private readonly IAzureSignalRService _azureSignalRService;

    private const int MAX_TOKENS = 50000;

    public PolicyCheckerService(
        ILogger<PolicyCheckerService> logger,
        IAzureOpenAIService azureOpenAIService,
        IAzureStorageService azureStorageService,
        IOptions<AzureDocIntelOptions> docIntelOptions,
        IAzureSignalRService azureSignalRService)
    {
        _logger = logger;
        _azureOpenAIService = azureOpenAIService;
        _azureStorageService = azureStorageService;
        _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
        _azureSignalRService = azureSignalRService;

        _documentIntelligenceClient = new DocumentIntelligenceClient(
            new Uri(docIntelOptions.Value.Endpoint),
            new AzureKeyCredential(docIntelOptions.Value.ApiKey)
        );
    }

    /// <summary>
    /// Checks the policy compliance of the engagement letter against the policy file.
    /// </summary>
    /// <param name="engagementLetter">Engagement Letter</param>
    /// <param name="policyFileName">Policy File</param>
    /// <param name="versionId">VersionId of the blob</param>
    /// <returns>SAS Uri of Policy Violations Markdown Report</returns>
    public async Task<PolicyCheckerResult> CheckPolicyAsync(string userId, string engagementLetter, string policyFileName, string versionId)
    {
        var engagementSasUri = await _azureStorageService.GetEngagementSasUriAsync(engagementLetter);

        var engagementLetterContent = await ReadDocumentContentAsync(new Uri(engagementSasUri));

        var policySas = await _azureStorageService.GetPolicySasUriAsync(policyFileName, versionId);

        var policyFileContent = await ReadDocumentContentAsync(new Uri(policySas));

        var engagementLetterTokens = _tokenizer.CountTokens(engagementLetterContent);

        var availableTokens = MAX_TOKENS - engagementLetterTokens - 1000; // Reserve 1000 tokens for prompts

        var policyChunks = ChunkDocument(policyFileContent, availableTokens);

        var allViolations = new StringBuilder();

        int policyChunkNumber = 1;

        foreach (var policyChunk in policyChunks)
        {
            _logger.LogInformation($"Analyzing policy chunk of size {policyChunk.Length} tokens.");
            var violation = await _azureOpenAIService.AnalyzePolicy(engagementLetterContent, policyChunk);

            if (!string.IsNullOrWhiteSpace(violation) && !violation.Contains("No violations found.", StringComparison.OrdinalIgnoreCase))
            {
                allViolations.AppendLine(violation);
            }

            // update progress
            await _azureSignalRService.SendProgressAsync(userId, (int)((float) policyChunkNumber / policyChunks.Count * 100));

            policyChunkNumber++;
        }

        var violationsSas = string.Empty;
        if (allViolations.Length == 0)
        {
            _logger.LogInformation($"No violations found in the engagement letter. {engagementLetter} for given policy {policyFileName}.");
        }
        else
        {
            // Upload violations markdown report and engagement letter to Azure Storage
            var violationsFileName = $"{Path.GetFileNameWithoutExtension(engagementLetter)}_Violations.MD";

            var binaryData = BinaryData.FromString(allViolations.ToString());

            await _azureStorageService.UploadFileToEngagementsContainerAsync(binaryData, violationsFileName);

            binaryData = BinaryData.FromString(engagementLetterContent);

            violationsSas = await _azureStorageService.GetEngagementSasUriAsync(violationsFileName);
        }

        return new PolicyCheckerResult
        {
            EngagementLetterName = engagementLetter,
            ViolationsSasUri = violationsSas,
            ViolationsCount = allViolations.Length,
            PolicyFileName = policyFileName,
            PolicyVersion = versionId
        };
    }

    private async Task<string> ReadDocumentContentAsync(Uri documentUri)
    {
        Operation<AnalyzeResult> operation = await _documentIntelligenceClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-read",
            documentUri
        );

        AnalyzeResult result = operation.Value;

        return result.Content;
    }

    /// <summary>
    /// Splits the document into chunks of approximately the size of the available tokens remaining in the context window, which is
    /// determined by subtracing the number of tokens of the engagement letter and a buffer of a specified number of tokens from
    /// the total tokens in the context window.
    /// </summary>
    /// <param name="source">Text of the source document</param>
    /// <param name="maxChunkSize">The maximum tokens a chunk can have based on remaining tokens in the context window</param>
    /// <returns>A list of tokens about the size of availableTokens which the source document is broken up into</returns>
    private List<string> ChunkDocument(string source, int maxChunkSize)
    {
        var chunks = new List<string>();

        // return a list of integers where each integer represents a token in the tokenizer's vocabulary
        var tokenIds = _tokenizer.EncodeToIds(source).ToList();

        // Go through all tokens and pull out as many tokens as will fit into the max chunk size
        for (int i = 0; i < tokenIds.Count; i += maxChunkSize)
        {
            // get the tokens from the last position (i) in the list of tokens up through the max chunk size or the remaining tokens (tokens - i) so we don't go beyond the list bounds
            var chunkTokens = tokenIds.GetRange(i, Math.Min(maxChunkSize, tokenIds.Count - i));
            var chunk = _tokenizer.Decode(chunkTokens);
            chunks.Add(chunk);
        }

        return chunks;
    }
}