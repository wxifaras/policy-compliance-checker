using PolicyComplianceCheckerApi.Models;

namespace PolicyComplianceCheckerApi.Services;

public interface IAzureCosmosDBService
{
    Task<PolicyLog> AddPolicyComplianceLogAsync(PolicyLog log);
    Task<EngagementLog> AddEngagementLogAsync(EngagementLog log);
    Task<List<PolicyLog>> GetPolicyComplianceLogs(string documentType, string userId);
    Task<List<EngagementLog>> GetEngagementLogs(string documentType, string userId);
}
