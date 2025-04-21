using PolicyComplianceCheckerApi.Models;

namespace PolicyComplianceCheckerApi.Services;

public interface IAzureCosmosDBService
{
    Task<PolicyLog> AddPolicyComplianceLogAsync(PolicyLog log);
    Task<EngagementLog> AddEngagementLogAsync(EngagementLog log);
    Task<List<TLog>> GetLogsAsync<TLog>(string documentType, string? userId = null)
        where TLog : class, ILog;
}