using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using concierge_agent_api.Models;
using Microsoft.Extensions.Options;
using PolicyComplianceCheckerApi.Models;
using System.Text.Json;

namespace PolicyComplianceCheckerApi.Services;

public class PolicyCheckerQueueService : BackgroundService
{
    private readonly ILogger<PolicyCheckerQueueService> _logger;
    private readonly QueueClient _queueClient;
    private readonly IPolicyCheckerService _policyCheckerService;
    private readonly IAzureSignalRService _azureSignalRService;

    public PolicyCheckerQueueService(
        ILogger<PolicyCheckerQueueService> logger,
        IOptions<AzureStorageOptions> storageOptions,
        IPolicyCheckerService policyCheckerService,
        IAzureSignalRService azureSignalRService)
    {
        _logger = logger;
        _queueClient = new QueueClient(storageOptions.Value.StorageConnectionString, storageOptions.Value.QueueName);
        _policyCheckerService = policyCheckerService;
        _azureSignalRService = azureSignalRService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PolicyCheckerQueueService started at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            QueueMessage[] messages = await _queueClient.ReceiveMessagesAsync(maxMessages: 10, visibilityTimeout: TimeSpan.FromSeconds(30), cancellationToken: stoppingToken);

            foreach (QueueMessage message in messages)
            {
                try
                {
                    // Process the message
                    _logger.LogInformation("Processing message: {message}", message.MessageText);

                    var policyRequest = JsonSerializer.Deserialize<PolicyCheckerRequest>(message.MessageText);

                    var policyCheckerResult = await _policyCheckerService.CheckPolicyAsync(
                            policyRequest.UserId,
                            policyRequest.EngagementLetter,
                            policyRequest.PolicyFileName,
                            policyRequest.VersionId
                        );

                    // Send the violationsSas to SignalR hub
                    await _azureSignalRService.SendPolicyResultAsync(policyRequest.UserId, policyCheckerResult);

                    // Delete the message after processing
                    await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message: {message}", message.MessageText);
                    // Optionally, handle the message (e.g., move to a dead-letter queue)
                }
            }

            // Wait before polling for new messages
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("PolicyCheckerQueueService stopping at: {time}", DateTimeOffset.Now);
    }
}