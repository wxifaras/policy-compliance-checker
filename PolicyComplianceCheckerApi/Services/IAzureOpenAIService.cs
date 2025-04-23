namespace PolicyComplianceCheckerApi.Services;

public interface IAzureOpenAIService
{
    Task<string> AnalyzeWithSchemaAsync(string violation, string llmResponseChunk);
    Task<string> AnalyzePolicyAsync(string engagementLetter, string policyChunk);
    int MaxTokens { get; }
    int RetryCount { get; }
    int RetryDelayInSeconds { get; }
}