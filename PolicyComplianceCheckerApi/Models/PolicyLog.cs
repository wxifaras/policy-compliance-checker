namespace PolicyComplianceCheckerApi.Models;

public enum DocumentType
{
    Engagement,
    Policy
}

public class LogBase
{
    public Guid id { get; set; } = Guid.NewGuid();
    public string DocumentType { get; set; } // Partition Key
    public string UserId { get; set; }   // Partition Key
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PolicyLog : LogBase
{
    public string PolicyFile { get; set; }
    public string VersionId { get; set; }
}

public class EngagementLog : LogBase
{
    public string EngagementLetter { get; set; }
    public string PolicyFile { get; set; }
    public string PolicyFileVersionId { get; set; }
    public string PolicyViolationsFile { get; set; }
}