using System.ComponentModel.DataAnnotations;

namespace PolicyComplianceCheckerApi.Models;

public record PolicyCheckerRequest
{
    public string UserId { get; set; } = string.Empty;

    [Required]
    public IFormFile EngagementLetter { get; set; }

    [Required]
    public string PolicyFileName { get; set; }

    [Required]
    public string PolicyVersion { get; set; }
}