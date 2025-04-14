namespace PolicyComplianceCheckerApi.Services;

public interface IAzureStorageService
{
    Task<string> GetPolicySasUriAsync(string fileName, string versionId);
    Task UploadPolicyAsync(Stream imageStream, string fileName, string version);
    Task UploadFileToEngagementsContainerAsync(BinaryData file, string fileName);
    Task<string> GetEngagementSasUriAsync(string fileName);
}