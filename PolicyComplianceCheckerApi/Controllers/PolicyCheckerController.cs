using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PolicyComplianceCheckerApi.Models;
using PolicyComplianceCheckerApi.Services;

namespace PolicyComplianceCheckerApi.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{v:apiVersion}/[controller]")]
public class PolicyCheckerController : ControllerBase
{
    private readonly ILogger<PolicyCheckerController> _logger;
    private readonly IPolicyCheckerService _policyCheckerService;

    public PolicyCheckerController(ILogger<PolicyCheckerController> logger,
        IPolicyCheckerService policyCheckerService)
    {
        _logger = logger;
        _policyCheckerService = policyCheckerService;
    }

    [MapToApiVersion("1.0")]
    [HttpPost("compliance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CheckPolicy([FromForm] PolicyCheckerRequest request)
    {
        try
        {
            await _policyCheckerService.CheckPolicyAsync(request.EngagementLetter, request.PolicyFileName, request.PolicyVersion);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking policy.");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }
}