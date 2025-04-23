namespace PolicyComplianceCheckerApi.Services;

public interface IAzureOpenAIService
{
    Task<string> AnalyzePolicy(string engagementLetter, string policyChunk);
    Task<string> AnalyzeWithSchemaAsync(string violation, string llmResponseChunk);
    int MaxTokens { get; }
    int RetryCount { get; }
    int RetryDelayInSeconds { get; }
}