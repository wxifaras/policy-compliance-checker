using Azure.AI.OpenAI;
using concierge_agent_api.Models;
using Microsoft.Extensions.Options;
using Microsoft.ML.Tokenizers;
using OpenAI.Chat;
using PolicyComplianceCheckerApi.Models;
using PolicyComplianceCheckerApi.Services;
using Polly;
using System.Text.Json;
using System.Text;

namespace PolicyComplianceCheckerApi.Validation;
public interface IValidationUtility
{
    Task<ValidationResponse> EvaluateSearchResultAsync(ValidationRequest validationRequest);
}

public class ValidationUtility : IValidationUtility
{
    private readonly AzureOpenAIClient _azureOpenAIClient;
    private readonly string _azureOpenAIDeployment;
    private readonly int _maxTokens;
    private readonly int _retryCount;
    private readonly int _retryDelayInSeconds;
    private readonly ILogger<ValidationUtility> _logger;
    private readonly TiktokenTokenizer _tokenizer;

    public ValidationUtility(
        AzureOpenAIClient azureOpenAIClient,
        IOptions<AzureOpenAIOptions> azureOpenAIOptions,
        ILogger<ValidationUtility> logger)
    {
        _azureOpenAIClient = azureOpenAIClient;
        var options = azureOpenAIOptions.Value;
        _azureOpenAIDeployment = options.DeploymentName ?? throw new ArgumentNullException(nameof(options.DeploymentName));
        _maxTokens = options.MaxTokens;
        _retryCount = options.RetryCount;
        _retryDelayInSeconds = options.RetryDelayInSeconds;
        _logger = logger;
        _tokenizer = TiktokenTokenizer.CreateForModel("gpt-4o");
    }

    public async Task<ValidationResponse> EvaluateSearchResultAsync(ValidationRequest validationRequest)
    {
        ValidationResponse validationResponse = new ValidationResponse();
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var evaluationSchemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Validation", "EvaluationSchema.json");
        var evaluationSchema = await File.ReadAllTextAsync(evaluationSchemaPath);

        var violationsChunks = ChunkDocument(validationRequest.Violations, _maxTokens / 2);
        var totalViolationsChunks = violationsChunks.Count;
        var allEvaluations = new List<Evaluation>();
        int violationsChunkNumber = 1;
        int llmChunkNumber = 1;

        var retryPolicy = Policy<string>
            .Handle<Exception>()
            .WaitAndRetryAsync(
                retryCount: _retryCount,
                sleepDurationProvider: _ => TimeSpan.FromSeconds(_retryDelayInSeconds),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    _logger.LogWarning($"Validation Retry {retryCount} failed after {timespan.TotalSeconds}s: {exception}");
                });

        var largestEngagementChunkCount = _tokenizer.CountTokens(violationsChunks[0]);
        var availableTokens = _maxTokens - largestEngagementChunkCount - 1000;
        var llmResponseChunks = ChunkDocument(validationRequest.LLMResponse, availableTokens);

        foreach (var violation in violationsChunks)
        {
            var violationTokensChunk = _tokenizer.CountTokens(violation);
            _logger.LogInformation($"violationTokenCheck: {violationTokensChunk}");

            foreach (var llmResponseChunk in llmResponseChunks)
            {
                var llmTokensChunk = _tokenizer.CountTokens(llmResponseChunk);
                _logger.LogInformation($"llmTokenChunk: {llmTokensChunk}");

                var evaluationPrompt = $@"
                You are an AI assistant tasked with evaluating the correctness of generated content. The generated content represents potential violations found in a policy. 
                Your goal is to assess whether the generated content aligns with the ground truth content provided.

                The evaluation is based on a 'correctness metric,' which measures how accurately the generated content identified potential policy violations and compares it to the ground truth content.
                You will be provided with both the generated content and the ground truth content.

                Your task is to:
                1. Compare the generated content against the ground truth content.
                2. Assign a rating based on the following scale:
                   - **1**: The content is incorrect.
                   - **3**: The content is partially correct but may lack key context or nuance, making it potentially misleading or incomplete compared to the ground truth content.
                   - **5**: The content is correct and complete based on the ground truth content.

                Additionally, you must provide a detailed explanation for the rating you selected.

                **Important Notes**:
                - The rating must always be one of the following values: 1, 3, or 5.
                - Construct a JSON object containing the Rating and your Thoughts. Return this JSON object as the response.

                **Input Data**:
                - Ground truth content: {violation}
                - Generated content: {llmResponseChunk}
            ";

                var client = _azureOpenAIClient.GetChatClient(_azureOpenAIDeployment);

                var messageContent = new List<ChatMessage>()
                {
                    new SystemChatMessage(evaluationPrompt)
                };

                var messageContentUpdates = await retryPolicy.ExecuteAsync(async () =>
                {
                    var result = await client.CompleteChatAsync(
                        messageContent,
                        new ChatCompletionOptions()
                        {
                            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("Eval", BinaryData.FromString(evaluationSchema))
                        });

                    return result.Value.Content[0].Text;
                });

                var evaluationResponse = JsonSerializer.Deserialize<Evaluation>(messageContentUpdates);

                if (evaluationResponse != null)
                {
                      allEvaluations.Add(evaluationResponse);
                }

                _logger.LogInformation($"Processed llmresponseChunk for chunk {llmChunkNumber} of {llmResponseChunks.Count}.");
                llmChunkNumber++;
            }

            _logger.LogInformation($"Processed violations chunk {violationsChunkNumber} of {violationsChunks.Count}.");
            violationsChunkNumber++;
        }

        validationResponse.Evaluation = new Evaluation
        {
            GeneratedContent = validationRequest.LLMResponse,
            Rating = allEvaluations
                .GroupBy(e => e.Rating)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key)
                .First().Key,
            //can list the Thoughts from each evaluation instead of summarizing them
            //Thoughts = string.Join("\n", allEvaluations.Select(e => e.Thoughts)),
            //summarize the thoughts from multiple chunks
            Thoughts = await SummarizeThoughtsAsync(allEvaluations.Select(e => e.Thoughts).ToList()),
            GroundTruthContent = validationRequest.Violations
        };

        return validationResponse;
    }

    private async Task<string> SummarizeThoughtsAsync(List<string> thoughts)
    {
        if (thoughts == null || !thoughts.Any())
            return string.Empty;

        if (thoughts.Count > 1)
        {
            var combinedThoughts = string.Join("\n", thoughts);

            // Create a prompt for the LLM to summarize the thoughts
            var summarizationPrompt = $@"
            You are an AI assistant tasked with summarizing feedback from multiple evaluations.
            Below is a list of detailed thoughts from various evaluations. Your task is to:

            1. Provide a concise summary of the feedback in a few sentences.

            **Detailed Thoughts**:
            {combinedThoughts}

            **Summary**:";

            var client = _azureOpenAIClient.GetChatClient(_azureOpenAIDeployment);

            var messageContent = new List<ChatMessage>
            {
               new SystemChatMessage(summarizationPrompt)
            };

            var result = await client.CompleteChatAsync(messageContent);

           return result.Value.Content[0].Text.ToString();
        }
        else
        {
           return thoughts.FirstOrDefault();
        }
    }

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