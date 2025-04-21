using Azure.Identity;
using concierge_agent_api.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Options;
using PolicyComplianceCheckerApi.Models;

namespace PolicyComplianceCheckerApi.Services;

public class AzureCosmosDBService : IAzureCosmosDBService
{
    private readonly Container _logContainer;
    private readonly ILogger<AzureCosmosDBService> _logger;

    public AzureCosmosDBService(
        IOptions<CosmosDbOptions> options,
        ILogger<AzureCosmosDBService> logger)
    {
        CosmosClient cosmosClient = new(
           accountEndpoint: options.Value.AccountUri,
           tokenCredential: new DefaultAzureCredential(
               new DefaultAzureCredentialOptions
               {
                   TenantId = options.Value.TenantId,
                   ExcludeEnvironmentCredential = true
               }));

        _logContainer = cosmosClient.GetContainer(options.Value.DatabaseName, options.Value.ContainerName);
        _logger = logger;
    }

    public async Task<PolicyLog> AddPolicyComplianceLogAsync(PolicyLog log)
    {
        _logger.LogInformation($"Adding policy compliance log for user {log.UserId} and document type {log.DocumentType}");

        ItemResponse<PolicyLog> response = await _logContainer.CreateItemAsync(
            item: log,
            partitionKey: new PartitionKeyBuilder()
                .Add(log.DocumentType)
                .Add(log.UserId)
                .Build());

        return response.Resource;
    }

    public async Task<EngagementLog> AddEngagementLogAsync(EngagementLog log)
    {
        _logger.LogInformation($"Adding engagement log for user {log.UserId} and Engagement letter {log.EngagementLetter}");

        ItemResponse<EngagementLog> response = await _logContainer.CreateItemAsync(
            item: log,
            partitionKey: new PartitionKeyBuilder()
                .Add(log.DocumentType)
                .Add(log.UserId)
                .Build());

        return response.Resource;
    }

    public async Task<List<TLog>> GetLogsAsync<TLog>(string documentType, string? userId = null) where TLog : class, ILog
    {
        var queryable = _logContainer.GetItemLinqQueryable<TLog>(allowSynchronousQueryExecution: false)
            .Where(p => p.DocumentType == documentType);

        if (!string.IsNullOrEmpty(userId))
        {
            queryable = queryable.Where(p => p.UserId == userId);
        }

        var query = queryable.ToFeedIterator();
        var logs = new List<TLog>();

        while (query.HasMoreResults)
        {
            var response = await query.ReadNextAsync();
            logs.AddRange(response);
        }

        return logs;
    }
}