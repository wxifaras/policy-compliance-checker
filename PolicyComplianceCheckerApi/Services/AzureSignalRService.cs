namespace PolicyComplianceCheckerApi.Services;

using Microsoft.AspNetCore.SignalR;
using PolicyComplianceCheckerApi.Hubs;
using PolicyComplianceCheckerApi.Models;

public class AzureSignalRService : IAzureSignalRService
{
    private readonly ILogger<IAzureSignalRService> _logger;
    private readonly IHubContext<PolicyCheckerHub> _hubContext;

    public AzureSignalRService(
        IHubContext<PolicyCheckerHub> hubContext,
        ILogger<IAzureSignalRService> logger)
    {
        _logger = logger;
        _hubContext = hubContext;            
    }

    public async Task SendPolicyResultAsync(string groupName, PolicyCheckerResult policyCheckerResult)
    {
        try
        {
            // send an update to all clients which have joined a specific group
            await _hubContext.Clients.Group(groupName).SendAsync("ReceivePolicyCheckerResult", policyCheckerResult);

            // send a broadcast to all clients
            //await _hubContext.Clients.All.SendAsync("ReceiveBroadcast", policyCheckerResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SignalR update");
        }
    }

    public async Task SendProgressAsync(string requestId, int progress)
    {
        try
        {
            await _hubContext.Clients.Group(requestId).SendAsync("ReceiveProgress", progress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending SignalR progress update");
        }
    }
}