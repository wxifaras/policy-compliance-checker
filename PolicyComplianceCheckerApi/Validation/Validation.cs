using Azure.AI.OpenAI;
using concierge_agent_api.Models;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using OpenAI.Chat;
using PolicyComplianceCheckerApi.Models;
using PolicyComplianceCheckerApi.Services;
using Polly;
using System.Text.Json;

namespace PolicyComplianceCheckerApi.Validation;
public interface IValidationUtility
{
    Task<ValidationResponse> EvaluateSearchResultAsync(ValidationRequest validationRequest);
}

public class ValidationUtility : IValidationUtility
{
    private IAzureOpenAIService _azureOpenAIService;
    private readonly int _retryCount;
    private readonly int _retryDelayInSeconds;
    private readonly ILogger<ValidationUtility> _logger;
    private readonly TiktokenTokenizer _tokenizer;

    public ValidationUtility(
        IAzureOpenAIService azureOpenAIService,
        IOptions<AzureOpenAIOptions> azureOpenAIOptions,
        ILogger<ValidationUtility> logger)
    {
        _azureOpenAIService = azureOpenAIService;
        var options = azureOpenAIOptions.Value;
        _retryCount = options.RetryCount;
        _retryDelayInSeconds = options.RetryDelayInSeconds;
        _logger = logger;
    }

    public async Task<ValidationResponse> EvaluateSearchResultAsync(ValidationRequest validationRequest)
    {
        ValidationResponse validationResponse = new ValidationResponse();
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
   
        var retryPolicy = Policy<string>
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: _retryCount,
                sleepDurationProvider: _ => TimeSpan.FromSeconds(_retryDelayInSeconds),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    _logger.LogWarning($"Validation Retry {retryCount} failed after {timespan.TotalSeconds}s: {exception}");
                });
   
        var messageContentUpdates = await retryPolicy.ExecuteAsync(async () =>
        {
           return await _azureOpenAIService.AnalyzeWithSchemaAsync(validationRequest.Violations, validationRequest.LLMResponse);
        });

        var evaluationResponse = JsonSerializer.Deserialize<Evaluation>(messageContentUpdates);

        validationResponse.Evaluation = new Evaluation
        {
            GeneratedContent = validationRequest.LLMResponse,
            Rating = evaluationResponse.Rating,
            Thoughts = evaluationResponse.Thoughts,
            GroundTruthContent = validationRequest.Violations
        };

       return validationResponse;
    }

}