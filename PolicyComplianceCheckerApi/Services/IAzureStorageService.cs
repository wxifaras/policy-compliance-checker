using PolicyComplianceCheckerApi.Models;

namespace PolicyComplianceCheckerApi.Services;

public interface IAzureStorageService
{
    Task<string> GetPolicySasUriAsync(string fileName, string versionId);
    Task<string> UploadPolicyAsync(Stream imageStream, string fileName);
    Task UploadFileToEngagementsContainerAsync(BinaryData file, string fileName);
    Task<string> GetEngagementSasUriAsync(string fileName);
    Task<Dictionary<string, List<PolicesWithVersionsResponse>>> GetPoliciesWithVersionsAsync();
    Task<BinaryData> ConvertSasUriToBinaryDataAsync(string sasUri);
}