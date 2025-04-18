using Asp.Versioning;
using Azure.Core;
using Microsoft.AspNetCore.Mvc;
using PolicyComplianceCheckerApi.Models;
using PolicyComplianceCheckerApi.Services;
using PolicyComplianceCheckerApi.Validation;
using System.Text;
using System.Text.Json;

namespace PolicyComplianceCheckerApi.Controllers;

[ApiVersion("1.0")]
[ApiController]
[Route("api/v{v:apiVersion}/[controller]")]
public class ValidationController : ControllerBase
{
    private readonly ILogger<ValidationController> _logger;
    private readonly IPolicyCheckerService _policyCheckerService;
    private readonly IValidationUtility _validationUtility;

    // Cache JsonSerializerOptions to avoid creating a new instance for every operation
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true // Enable case-insensitive deserialization
    };

    public ValidationController(ILogger<ValidationController> logger, IPolicyCheckerService policyCheckerService, IValidationUtility validationUtility)
    {
        _logger = logger;
        _policyCheckerService = policyCheckerService;
        _validationUtility = validationUtility;
    }

    [MapToApiVersion("1.0")]
    [HttpPost("ground-truth-validation")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GroundTruthValidationAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("File cannot be null or empty");
        }

        var validationRequests = new List<ValidationRequest>();
        var evaluationResponses = new List<Evaluation>();

        using var stream = file.OpenReadStream();
        using var reader = new StreamReader(stream);
        var fileContent = await reader.ReadToEndAsync();

        try
        {
            validationRequests = JsonSerializer.Deserialize<List<ValidationRequest>>(fileContent, _jsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError($"Failed to deserialize JSON file: {ex}");
            return new BadRequestObjectResult("Invalid JSON file");
        }

        if (validationRequests.Count == 0)
        {
            _logger.LogError("Request body and file content are both null or empty");
            return new BadRequestObjectResult("Request body and file content cannot be both null or empty");
        }

        var validationResponses = await ProcessGroundTruthDocAsync(validationRequests);
        evaluationResponses = validationResponses.Select(x => x.Evaluation).ToList();

        return new OkObjectResult(evaluationResponses);
    }

    private async Task<List<ValidationResponse>> ProcessGroundTruthDocAsync(List<ValidationRequest> validationRequests)
    {
        var validationResponses = new List<ValidationResponse>();

        foreach (var request in validationRequests)
        {
           var policyCheckerResult = await _policyCheckerService.CheckPolicyAsync(
               request.EngagementLetterFileName,
               request.PolicyName,
               request.PolicyVersion,
               "Validation" 
                );
           request.LLMResponse = policyCheckerResult.ViolationsContent;
           var response = await _validationUtility.EvaluateSearchResultAsync(request);
           validationResponses.Add(response);
        }

        return validationResponses;
    }
}
