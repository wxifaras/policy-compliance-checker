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

    public AdminController(ILogger<AdminController> logger,
        IAzureStorageService azureStorageService)
    {
        _logger = logger;
        _azureStorageService = azureStorageService;
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
            var version = await _azureStorageService.UploadPolicyAsync(request.Policy.OpenReadStream(), request.Policy.FileName);
            return Ok(version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading policy.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}