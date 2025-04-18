using Azure.AI.OpenAI;
using Azure;
using concierge_agent_api.Models;
using Microsoft.Extensions.Options;
using PolicyComplianceCheckerApi.Prompts;
using OpenAI.Chat;

namespace PolicyComplianceCheckerApi.Services;

public class AzureOpenAIService : IAzureOpenAIService
{
    private ILogger<AzureOpenAIService> _logger;
    private readonly AzureOpenAIClient _azureOpenAIClient;
    private readonly string _deploymentName;
    private int _maxTokens;
    private int _retryCount;
    private int _retryDelayInSeconds;

    public AzureOpenAIService(
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAIService> logger)
    {
        _logger = logger;

        _deploymentName = options.Value.DeploymentName;

        _maxTokens = options.Value.MaxTokens;
        _retryCount = options.Value.RetryCount;
        _retryDelayInSeconds = options.Value.RetryDelayInSeconds;

        _azureOpenAIClient = new(
           new Uri(options.Value.EndPoint),
           new AzureKeyCredential(options.Value.ApiKey));
    }

    public int MaxTokens => _maxTokens;

    public int RetryCount => _retryCount;

    public int RetryDelayInSeconds => _retryDelayInSeconds;

    public async Task<string> AnalyzePolicy(string engagementLetter, string policyChunk)
    {
        var systemPrompt = CorePrompts.GetSystemPrompt(engagementLetter);
        var userPrompt = CorePrompts.GetUserPrompt(policyChunk);
        var chatClient = _azureOpenAIClient.GetChatClient(_deploymentName);

        List<ChatMessage> messages = new List<ChatMessage>()
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt),
        };

        var response = await chatClient.CompleteChatAsync(messages);

        return response.Value.Content[0].Text;
    }
}