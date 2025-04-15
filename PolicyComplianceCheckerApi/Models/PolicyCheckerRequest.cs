using System.ComponentModel.DataAnnotations;

namespace PolicyComplianceCheckerApi.Models;

public record PolicyCheckerRequest
{
    [Required]
    public string UserId { get; set; }

    [Required]
    public string EngagementLetter { get; set; }

    [Required]
    public string PolicyFileName { get; set; }

    [Required]
    public string VersionId { get; set; }
}