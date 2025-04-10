using System.ComponentModel.DataAnnotations;

namespace concierge_agent_api.Models;

public record AzureStorageOptions
{
    public const string AzureStorage = "AzureStorageOptions";

    [Required]
    public string PoliciesContainer { get; set; }
    [Required]
    public string EngagementsContainer { get; set; }
    [Required]
    public string StorageConnectionString { get; set; }
}