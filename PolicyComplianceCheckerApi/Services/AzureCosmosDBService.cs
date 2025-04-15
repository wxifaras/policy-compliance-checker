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
               })
       );

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

    public async Task<List<PolicyLog>> GetPolicyComplianceLogs(string documentType, string userId)
    {
        var policyComplianceLogs = new List<PolicyLog>();

        var query = _logContainer.GetItemLinqQueryable<PolicyLog>()
            .Where(p => p.UserId == userId && p.DocumentType == documentType).ToFeedIterator();

        while (query.HasMoreResults)
        {
            foreach (var item in await query.ReadNextAsync())
            {
                // Process each item
            }
        }

        return policyComplianceLogs;
    }
}