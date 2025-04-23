namespace PolicyComplianceCheckerApi.Models;

public record Evaluation
{ 
    public string GeneratedContent { get; set; }

    public int Rating { get; set; }

    public string Thoughts { get; set; }

    public string GroundTruthContent { get; set; }
}
