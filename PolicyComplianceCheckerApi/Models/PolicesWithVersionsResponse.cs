namespace PolicyComplianceCheckerApi.Models;

public record PolicesWithVersionsResponse
{
    public string VersionId { get; set; }
    public bool? IsLatestVersion { get; set; }
    public DateTimeOffset? LastModified { get; set; }
}