namespace PolicyComplianceCheckerApi.Models;

public class ValidationRequest
{
    public string EngagementLetterFileName { get; set; }
    public string PolicyName { get; set; }
    public string PolicyVersion { get; set; }
    public string Violations { get; set; }
    public string LLMResponse { get; set; }
}