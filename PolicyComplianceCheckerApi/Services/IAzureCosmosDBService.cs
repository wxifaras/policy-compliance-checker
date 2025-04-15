using PolicyComplianceCheckerApi.Models;

namespace PolicyComplianceCheckerApi.Services;

public interface IAzureCosmosDBService
{
    Task<PolicyLog> AddPolicyComplianceLogAsync(PolicyLog log);
    Task<List<PolicyLog>> GetPolicyComplianceLogs(string documentType, string userId);
}
