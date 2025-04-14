using Asp.Versioning;
using Azure.Storage.Queues;
using concierge_agent_api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PolicyComplianceCheckerApi.Models;
using PolicyComplianceCheckerApi.Services;
using System.Text.Json;

namespace PolicyComplianceCheckerApi.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{v:apiVersion}/[controller]")]
public class PolicyCheckerController : ControllerBase
{
    private readonly ILogger<PolicyCheckerController> _logger;
    private readonly QueueClient _queueClient;

    public PolicyCheckerController(
        ILogger<PolicyCheckerController> logger,
        IOptions<AzureStorageOptions> storageOptions)
    {
        _logger = logger;
        _queueClient = new QueueClient(storageOptions.Value.StorageConnectionString, storageOptions.Value.QueueName);
    }

    [MapToApiVersion("1.0")]
    [HttpPost("enqueue-policy-check")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> EnqueuePolicyCheckAsync([FromForm] PolicyCheckerRequest request)
    {
        try
        {
            var message = JsonSerializer.Serialize(request);

            await _queueClient.SendMessageAsync(message);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking policy.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}