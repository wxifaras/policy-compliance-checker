using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PolicyComplianceCheckerApi.Models;
using PolicyComplianceCheckerApi.Services;

namespace PolicyComplianceCheckerApi.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{v:apiVersion}/[controller]")]
public class AdminController : ControllerBase
{
    private readonly ILogger<AdminController> _logger;
    private readonly IAzureStorageService _azureStorageService;
    private readonly IAzureCosmosDBService _cosmosDBService;

    public AdminController(
        ILogger<AdminController> logger,
        IAzureStorageService azureStorageService,
        IAzureCosmosDBService cosmosDBService)
    {
        _logger = logger;
        _azureStorageService = azureStorageService;
        _cosmosDBService = cosmosDBService;
    }

    [MapToApiVersion("1.0")]
    [HttpPost("policy")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UploadPolicy([FromForm] PolicyAdminRequest request)
    {
        try
        {
            var versionId = await _azureStorageService.UploadPolicyAsync(request.Policy.OpenReadStream(), request.Policy.FileName);

            var policyLog = new PolicyLog
            {
                DocumentType = DocumentType.Policy.ToString(),
                UserId = request.UserId,
                VersionId = versionId,
                PolicyFile = request.Policy.FileName
            };

            await _cosmosDBService.AddPolicyComplianceLogAsync(policyLog);

            return Ok(versionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading policy.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [MapToApiVersion("1.0")]
    [HttpGet("policy-logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetPolicyLogs([FromQuery] string? userId = null)
    {
        try
        {
            var logs = await _cosmosDBService.GetPolicyComplianceLogs(DocumentType.Policy.ToString(), userId);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving policy logs.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [MapToApiVersion("1.0")]
    [HttpGet("engagement-logs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetEngagementLogs([FromQuery] string? userId = null)
    {
        try
        {
            var logs = await _cosmosDBService.GetEngagementLogs(DocumentType.Engagement.ToString(), userId);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving EngagementLog logs.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }


}