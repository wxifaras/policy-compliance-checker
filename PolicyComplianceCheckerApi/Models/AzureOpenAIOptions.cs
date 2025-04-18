namespace concierge_agent_api.Models;

using System.ComponentModel.DataAnnotations;

public record AzureOpenAIOptions
{
    public const string AzureOpenAI = "AzureOpenAIOptions";

    [Required]
    public string DeploymentName { get; set; }

    [Required]
    public string EndPoint { get; set; }

    [Required]
    public string ApiKey { get; set; }

    [Required]
    public int MaxTokens { get; set; }
}