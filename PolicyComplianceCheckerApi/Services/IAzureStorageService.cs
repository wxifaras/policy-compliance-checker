namespace PolicyComplianceCheckerApi.Services;

public interface IAzureStorageService
{
    Task<string> GeneratePolicySasUriAsync(string fileName, string version);
    Task UploadPolicyAsync(Stream imageStream, string fileName, string version);
    Task UploadFileToEngagementsContainerAsync(BinaryData file, string fileName);
    Task<string> GenerateViolationsSasUriAsync(string fileName);
}