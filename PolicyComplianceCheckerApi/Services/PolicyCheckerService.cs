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

    private const int MAX_TOKENS = 50000;

    public PolicyCheckerService(
        ILogger<PolicyCheckerService> logger,
        IAzureOpenAIService azureOpenAIService,
        IAzureStorageService azureStorageService,
        IOptions<AzureDocIntelOptions> docIntelOptions)
    {
        _logger = logger;
        _azureOpenAIService = azureOpenAIService;
        _azureStorageService = azureStorageService;
        _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");

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
    /// <param name="policyVersion">Policy File Version</param>
    /// <returns>SAS Uri of Policy Violations Markdown Report</returns>
    public async Task<string> CheckPolicyAsync(string engagementLetter, string policyFileName, string policyVersion)
    {
        var engagementSasUri = await _azureStorageService.GetEngagementSasUriAsync(engagementLetter);

        var engagementLetterContent = await ReadDocumentContentAsync(new Uri(engagementSasUri));

        var policySas = await _azureStorageService.GetPolicySasUriAsync(policyFileName, policyVersion);

        var policyFileContent = await ReadDocumentContentAsync(new Uri(policySas));

        var engagementLetterTokens = _tokenizer.CountTokens(engagementLetterContent);

        var availableTokens = MAX_TOKENS - engagementLetterTokens - 1000; // Reserve 1000 tokens for prompts

        var policyChunks = ChunkDocument(policyFileContent, availableTokens);

        var allViolations = new StringBuilder();

        foreach (var policyChunk in policyChunks)
        {
            _logger.LogInformation($"Analyzing policy chunk of size {policyChunk.Length} tokens.");
            var violation = await _azureOpenAIService.AnalyzePolicy(engagementLetterContent, policyChunk);

            if (!string.IsNullOrWhiteSpace(violation) && !violation.Contains("No violations found.", StringComparison.OrdinalIgnoreCase))
            {
                allViolations.AppendLine(violation);
            }
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

        return violationsSas;
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

    private List<string> ChunkDocument(string source, int availableTokens)
    {
        var tokens = _tokenizer.CountTokens(source);
        var chunks = new List<string>();
        var tokenIds = _tokenizer.EncodeToIds(source).ToList();

        for (int i = 0; i < tokens; i += availableTokens)
        {
            var chunkTokens = tokenIds.GetRange(i, Math.Min(availableTokens, tokens - i));
            var chunk = _tokenizer.Decode(chunkTokens);
            chunks.Add(chunk);
        }

        return chunks;
    }
}