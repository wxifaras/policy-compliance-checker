using PolicyComplianceCheckerApi.Models;

namespace PolicyComplianceCheckerApi.Services;

public interface IAzureCosmosDBService
{
    Task<TLog> AddLogAsync<TLog>(TLog log) where TLog : class, ILog;

    Task<List<TLog>> GetLogsAsync<TLog>(string documentType, string? userId = null) where TLog : class, ILog;
}