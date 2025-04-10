namespace PolicyComplianceCheckerApi.Services;

public interface IAzureStorageService
{
    Task<string> GenerateSasUriAsync(string fileName, string version);

    Task UploadPolicyAsync(Stream imageStream, string fileName, string version);
}