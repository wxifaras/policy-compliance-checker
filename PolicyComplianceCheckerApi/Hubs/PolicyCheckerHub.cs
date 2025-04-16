namespace PolicyComplianceCheckerApi.Hubs;

using Microsoft.AspNetCore.SignalR;

public class PolicyCheckerHub : Hub
{
    private readonly ILogger<PolicyCheckerHub> _logger;

    public PolicyCheckerHub(ILogger<PolicyCheckerHub> logger)
    {
        _logger = logger;
    }

    // Method to allow clients to register with a user ID (effectively a "group")
    public async Task RegisterUser(string userId)
    {
        _logger.LogInformation("Client {ConnectionId} registered with User ID {UserId}", Context.ConnectionId, userId);
        await Groups.AddToGroupAsync(Context.ConnectionId, userId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}