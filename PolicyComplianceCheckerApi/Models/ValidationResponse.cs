namespace PolicyComplianceCheckerApi.Models;

public record ValidationResponse
{
    public Evaluation Evaluation { get; set; }
}