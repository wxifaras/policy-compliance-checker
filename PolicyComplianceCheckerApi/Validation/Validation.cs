using Azure.AI.OpenAI;
using concierge_agent_api.Models;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using PolicyComplianceCheckerApi.Models;
using PolicyComplianceCheckerApi.Services;
using System.Text.Json;

namespace PolicyComplianceCheckerApi.Validation;
public interface IValidationUtility
{
    Task<ValidationResponse> EvaluateSearchResultAsync(ValidationRequest validationRequest);
}

public class ValidationUtility : IValidationUtility
{
    private readonly AzureOpenAIClient _azureOpenAIClient;
    private readonly string _azureOpenAIDeployment;

    public ValidationUtility(AzureOpenAIClient azureOpenAIClient, IOptions<AzureOpenAIOptions> azureOpenAIOptions)
    {
       _azureOpenAIClient = azureOpenAIClient;
       _azureOpenAIDeployment = azureOpenAIOptions.Value.DeploymentName ?? throw new ArgumentNullException(nameof(azureOpenAIOptions.Value.DeploymentName));
    }

    public async Task<ValidationResponse> EvaluateSearchResultAsync(ValidationRequest validationRequest)
    {
        ValidationResponse validationResponse = new ValidationResponse();

           var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
           var evaluationSchemaPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Validation", "EvaluationSchema.json");
           var evaluationSchema = await File.ReadAllTextAsync(evaluationSchemaPath);

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
            - Construct a JSON object containing your thoughts, the rating, the ground truth content, and the generated content. Return this JSON object as the response.

            **Input Data**:
            - Ground truth content: {validationRequest.Violations}
            - Generated content: {validationRequest.LLMResponse}
        ";

        var client = _azureOpenAIClient.GetChatClient(_azureOpenAIDeployment);

        var chat = new List<ChatMessage>()
            {
                new SystemChatMessage(evaluationPrompt)
            };

        var chatUpdates = await client.CompleteChatAsync(
            chat,
            new ChatCompletionOptions()
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat("Eval", BinaryData.FromString(evaluationSchema))
            });

            var evaluationResponse = JsonSerializer.Deserialize<Evaluation>(chatUpdates.Value.Content[0].Text);

            evaluationResponse = new Evaluation
            {
                GeneratedContent = evaluationResponse.GeneratedContent,
                Rating = evaluationResponse.Rating,
                Thoughts = evaluationResponse.Thoughts,
                GroundTruthContent = evaluationResponse.GroundTruthContent
            };

            validationResponse.Evaluation = evaluationResponse;

        return validationResponse;
    }
}
