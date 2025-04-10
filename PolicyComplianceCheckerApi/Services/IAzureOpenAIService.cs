namespace PolicyComplianceCheckerApi.Services;

public interface IAzureOpenAIService
{
    Task<string> AnalyzePolicy(string engagementLetter, string policyChunk);
}