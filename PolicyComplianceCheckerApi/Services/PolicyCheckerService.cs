﻿using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using PolicyComplianceCheckerApi.Models;
using System.Text;
using Polly;
using Polly.Retry;
using concierge_agent_api.Models;

namespace PolicyComplianceCheckerApi.Services;

public class PolicyCheckerService : IPolicyCheckerService
{
    private ILogger<PolicyCheckerService> _logger;
    private IAzureOpenAIService _azureOpenAIService;
    private readonly IAzureStorageService _azureStorageService;
    private readonly TiktokenTokenizer _tokenizer;
    private readonly DocumentIntelligenceClient _documentIntelligenceClient;
    private readonly IAzureSignalRService _azureSignalRService;
    private readonly IAzureCosmosDBService _cosmosDBService;
    private readonly float _overlapPercentage;

    public PolicyCheckerService(
        ILogger<PolicyCheckerService> logger,
        IAzureOpenAIService azureOpenAIService,
        IAzureStorageService azureStorageService,
        IOptions<AzureDocIntelOptions> docIntelOptions,
        IOptions<ChunkingOptions> chunkingOptions,
        IAzureSignalRService azureSignalRService,
        IAzureCosmosDBService cosmosDBService)
    {
        _logger = logger;
        _azureOpenAIService = azureOpenAIService;
        _azureStorageService = azureStorageService;
        _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
        _azureSignalRService = azureSignalRService;

        _documentIntelligenceClient = new DocumentIntelligenceClient(
            new Uri(docIntelOptions.Value.Endpoint),
            new AzureKeyCredential(docIntelOptions.Value.ApiKey));

        _cosmosDBService = cosmosDBService;
        _overlapPercentage = chunkingOptions.Value.OverlapPercentage;
    }

    /// <summary>
    /// Checks the policy compliance of the engagement letter against the policy file.
    /// </summary>
    /// <param name="engagementLetter">Engagement Letter</param>
    /// <param name="policyFileName">Policy File</param>
    /// <param name="versionId">VersionId of the blob</param>
    /// <param name="userId">UserId of the user</param>
    /// <returns>SAS Uri of Policy Violations Markdown Report</returns>
    public async Task<PolicyCheckerResult> CheckPolicyAsync(string engagementLetter, string policyFileName, string versionId, string userId)
    {
        var engagementSasUri = await _azureStorageService.GetEngagementSasUriAsync(engagementLetter);
        var engagementLetterContent = await ReadDocumentContentAsync(new Uri(engagementSasUri));

        var policySas = await _azureStorageService.GetPolicySasUriAsync(policyFileName, versionId);
        var policyFileContent = await ReadDocumentContentAsync(new Uri(policySas));

        var violationsFileName = string.Empty;
        var allViolations = new StringBuilder();

        //Instead of analyzing the entire engagement letter at once, we now split it into smaller pieces 
        var engagementChunks = ChunkDocument(engagementLetterContent, _azureOpenAIService.MaxTokens / 2);

        var totalChunks = engagementChunks.Count;
        var processedChunks = 0;

        var retryPolicy = Policy<string>
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: _azureOpenAIService.RetryCount,
            sleepDurationProvider: _ => TimeSpan.FromSeconds(_azureOpenAIService.RetryDelayInSeconds),
            onRetry: (exception, timespan, retryCount, context) =>
            {
                var message = exception?.Exception?.Message ?? "No exception message available";
                _logger.LogWarning($"Retry {retryCount} encountered an error: {message}. Waiting {timespan} before next retry.");
            });

        // Token management strategy:
        // 1. We use the first engagement chunk as a reference for calculating available tokens because:
        //    - All chunks are created with equal maximum size (except possibly the last one)
        //    - Using the first chunk provides a reliable upper bound for token consumption
        // 2. We calculate available tokens for policy chunks by subtracting:
        //    - The token count of a typical engagement chunk (using the first one as reference)
        //    - A safety buffer (1000 tokens)
        //    - This ensures we stay within OpenAI's token limits during each comparison
        // 3. We process each engagement chunk against each policy chunk individually, rather than
        //    trying to process the entire documents at once, enabling analysis of large documents
        var largestEngagementChunkCount = _tokenizer.CountTokens(engagementChunks[0]);
        var availableTokens = _azureOpenAIService.MaxTokens - largestEngagementChunkCount - 1000; // Reserve buffer
        var policyChunks = ChunkDocument(policyFileContent, availableTokens);

        foreach (var engagementChunk in engagementChunks)
        {
            //calculate how much space we have left in the token budget for the policy chunk
            var engagementTokens = _tokenizer.CountTokens(engagementChunk);

            _logger.LogInformation($"Analyzing engagement chunk of size {engagementTokens} tokens.");

            foreach (var policyChunk in policyChunks)
            {
                var policyChunkTokenCount = _tokenizer.CountTokens(policyChunk);

                _logger.LogInformation($"Analyzing policy chunk of size {policyChunkTokenCount} tokens.");

                var totalTokensPerRequest = engagementTokens + policyChunkTokenCount;

                _logger.LogInformation($"Total tokens per request: {totalTokensPerRequest}.");

                // Use Polly to retry AnalyzePolicy
                var violation = await retryPolicy.ExecuteAsync(() =>
                    _azureOpenAIService.AnalyzePolicyAsync(engagementChunk, policyChunk));

                if (!string.IsNullOrWhiteSpace(violation) && !violation.Contains("No violations found.", StringComparison.OrdinalIgnoreCase))
                {
                    allViolations.AppendLine(violation);
                }

                // user realistic progress updates.
                var overallProgress = (int)((float)(++processedChunks) / (totalChunks * policyChunks.Count) * 100);

                _logger.LogInformation($"Overall progress: {overallProgress}%");

                await _azureSignalRService.SendProgressAsync(userId, overallProgress);
            }
        }

        var violationsSas = string.Empty;
        if (allViolations.Length == 0)
        {
            _logger.LogInformation($"No violations found in the engagement letter: {engagementLetter} for policy: {policyFileName}.");
        }
        else
        {
            violationsFileName = $"{Path.GetFileNameWithoutExtension(engagementLetter)}_Violations.MD";
            var binaryData = BinaryData.FromString(allViolations.ToString());

            await _azureStorageService.UploadFileToEngagementsContainerAsync(binaryData, violationsFileName);

            binaryData = BinaryData.FromString(engagementLetterContent);
            violationsSas = await _azureStorageService.GetEngagementSasUriAsync(violationsFileName);
        }

        var engagementLog = new EngagementLog
        {
            DocumentType = DocumentType.Engagement.ToString(),
            UserId = userId,
            EngagementLetter = engagementLetter,
            PolicyFile = policyFileName,
            PolicyFileVersionId = versionId,
            PolicyViolationsFile = violationsFileName
        };

        await _cosmosDBService.AddLogAsync<EngagementLog>(engagementLog);

        var policyCheckerResult = new PolicyCheckerResult
        {
            EngagementLetterName = engagementLetter,
            ViolationsSasUri = violationsSas,
            PolicyFileName = policyFileName,
            PolicyVersion = versionId,
            ViolationsContent = allViolations.ToString(),
        };

        return policyCheckerResult;
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

    private async Task<string> ReadDocumentContentAsync(BinaryData document)
    {
        Operation<AnalyzeResult> operation = await _documentIntelligenceClient.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            "prebuilt-read",
            document
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

        // Calculate overlap size as % of the maxChunkSize
        var overlapSize = (int)(maxChunkSize * _overlapPercentage);

        // Return a list of integers where each integer represents a token in the tokenizer's vocabulary
        var tokenIds = _tokenizer.EncodeToIds(source).ToList();

        var i = 0;
        while (i < tokenIds.Count)
        {
            // Get the tokens from the current position up to the max chunk size or the remaining tokens
            var chunkTokens = tokenIds.GetRange(i, Math.Min(maxChunkSize, tokenIds.Count - i));
            var chunk = _tokenizer.Decode(chunkTokens);
            chunks.Add(chunk);

            // Increment by (maxChunkSize - overlapSize) to allow overlap
            i += (maxChunkSize - overlapSize);
        }

        return chunks;
    }
}