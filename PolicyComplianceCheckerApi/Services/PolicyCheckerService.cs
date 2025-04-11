﻿using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using PolicyComplianceCheckerApi.Models;
using System.Text;

namespace PolicyComplianceCheckerApi.Services;

public class PolicyCheckerService : IPolicyCheckerService
{
    private ILogger<PolicyCheckerService> _logger;
    private IAzureOpenAIService _azureOpenAIService;
    private readonly IAzureStorageService _azureStorageService;
    private readonly string _docIntelligenceEndpoint;
    private readonly string _docIntelligenceApiKey;
    private readonly TiktokenTokenizer _tokenizer;

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
        _docIntelligenceEndpoint = docIntelOptions.Value.Endpoint;
        _docIntelligenceApiKey = docIntelOptions.Value.ApiKey;
        _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
    }

    public async Task CheckPolicyAsync(IFormFile engagementLetter, string policyFileName, string policyVersion)
    {
        using Stream fileStream = engagementLetter.OpenReadStream();
        
        var binaryData = BinaryData.FromStream(fileStream);
        
        var engagementLetterContent = await ReadFileAsync(binaryData, null);

        var policySas = await _azureStorageService.GeneratePolicySasUriAsync(policyFileName, policyVersion);

        var policyFileContent = await ReadFileAsync(null, new Uri(policySas));

        var engagementLetterTokens = _tokenizer.CountTokens(engagementLetterContent);

        var availableTokens = MAX_TOKENS - engagementLetterTokens - 1000; // Reserve 1000 tokens for prompts

        var policyChunks = ChunkDocument(policyFileContent, availableTokens);

        var allViolations = new StringBuilder();

        foreach(var policyChunk in policyChunks)
        {
            var violation = await _azureOpenAIService.AnalyzePolicy(engagementLetterContent, policyChunk);

            if (!string.IsNullOrWhiteSpace(violation) && !violation.Contains("No violations found.", StringComparison.OrdinalIgnoreCase))
            {
                allViolations.AppendLine(violation);
            }
        }

        if (allViolations.Length == 0)
        {
            _logger.LogInformation($"No violations found in the engagement letter. {engagementLetter.FileName}");
        }
        else
        {
            var violationsFileName = $"{Path.GetFileNameWithoutExtension(engagementLetter.FileName)}_Violations.MD";
            binaryData = BinaryData.FromString(allViolations.ToString());
            await _azureStorageService.UploadViolationsFileAsync(binaryData, violationsFileName);
            var violationsSas = await _azureStorageService.GenerateViolationsSasUriAsync(violationsFileName);

            // TODO: Send the violationsSas to the user via SignalR Service.
        }
    }

    private async Task<string> ReadFileAsync(BinaryData? engagementLetter, Uri? policyFile)
    {
        var documentIntelligenceClient = new DocumentIntelligenceClient(
            new Uri(_docIntelligenceEndpoint),
            new AzureKeyCredential(_docIntelligenceApiKey)
        );

        Operation<AnalyzeResult> operation = policyFile != null
            ? await documentIntelligenceClient.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-read", policyFile)
            : await documentIntelligenceClient.AnalyzeDocumentAsync(WaitUntil.Completed, "prebuilt-read", engagementLetter);

        AnalyzeResult result = operation.Value;

        return result.Content;
    }

    public List<string> ChunkDocument(string source, int maxChunkSize)
    {
        var tokens = _tokenizer.CountTokens(source);
        var chunks = new List<string>();
        var tokenIds = _tokenizer.EncodeToIds(source).ToList();

        for (int i = 0; i < tokens; i += maxChunkSize)
        {
            var chunkTokens = tokenIds.GetRange(i, Math.Min(maxChunkSize, tokens - i));
            var chunk = _tokenizer.Decode(chunkTokens);
            chunks.Add(chunk);
        }

        return chunks;
    }
}