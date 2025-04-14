using System.ComponentModel.DataAnnotations;

namespace concierge_agent_api.Models;

public record CosmosDbOptions
{
    public const string CosmosDb = "CosmosDbOptions";

    [Required]
    public string DatabaseName { get; set; }

    [Required]
    public string ContainerName { get; set; }

    [Required]
    public string AccountUri { get; set; }

    [Required]
    public string TenantId { get; set; }
}