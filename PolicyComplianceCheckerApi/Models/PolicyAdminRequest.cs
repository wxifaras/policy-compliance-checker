using System.ComponentModel.DataAnnotations;

namespace PolicyComplianceCheckerApi.Models;

public record PolicyAdminRequest
{
    [Required]
    public IFormFile Policy { get; set; }

    [Required]
    public string UserId { get; set; }
}