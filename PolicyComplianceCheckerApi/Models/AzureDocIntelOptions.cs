using System.ComponentModel.DataAnnotations;

namespace PolicyComplianceCheckerApi.Models;

public record AzureDocIntelOptions
{
    public const string AzureDocIntel = "AzureDocIntelOptions";

    [Required]
    public string Endpoint { get; init; } = string.Empty;

    [Required]
    public string ApiKey { get; init; } = string.Empty;
}