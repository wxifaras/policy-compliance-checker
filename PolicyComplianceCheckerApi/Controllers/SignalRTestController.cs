namespace PolicyComplianceCheckerApi.Controllers;

using Microsoft.AspNetCore.Mvc;
using PolicyComplianceCheckerApi.Models;
using PolicyComplianceCheckerApi.Services;

[Route("api/[controller]")]
[ApiController]
public class SignalRTestController : Controller
{
    private readonly IAzureSignalRService _signalRService;
    private readonly ILogger<SignalRTestController> _logger;

    public SignalRTestController(
        IAzureSignalRService signalRService,
        ILogger<SignalRTestController> logger)
    {
        _signalRService = signalRService;
        _logger = logger;
    }

    [HttpPost("policy-result")]
    public async Task<IActionResult> SendPolicyResult([FromBody] SendPolicyResultRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.GroupName))
        {
            return BadRequest("GroupName is required");
        }

            // Create PolicyCheckerResult from the request
            var result = new PolicyCheckerResult
            {
                ViolationsSasUri = request.ViolationsSasUri ?? "https://placeholder.blob.core.windows.net/reports/custom-report.md",
                ViolationsCount = request.ViolationsCount ?? 0,
                EngagementLetterName = request.EngagementLetterName ?? "Custom Engagement Letter",
                PolicyFileName = request.PolicyFileName ?? "Custom Policy Document.pdf",
                PolicyVersion = request.PolicyVersion ?? "1.0.0"
            };

        // Send the result
        await _signalRService.SendPolicyResultAsync(request.GroupName, result);
        _logger.LogInformation("Sent custom policy check results to {GroupName}", request.GroupName);

        return Ok(new { status = "Custom policy check results sent", groupName = request.GroupName });
    }

    [HttpGet("progress/{groupName}/{percentage}")]
    public async Task<IActionResult> SendProgress(string groupName, int percentage)
    {
        await _signalRService.SendProgressAsync(groupName, percentage);
        _logger.LogInformation("Sent progress update to {groupName}: {Progress}%", groupName, percentage);
        return Ok(new { status = "Progress sent", groupName, progress = percentage });
    }
}

    // Class for custom policy result requests
    public class SendPolicyResultRequest
    {
        public string GroupName { get; set; }
        public string ViolationsSasUri { get; set; }
        public int? ViolationsCount { get; set; }
        public string EngagementLetterName { get; set; }
        public string PolicyFileName { get; set; }
        public string PolicyVersion { get; set; }
    }
}