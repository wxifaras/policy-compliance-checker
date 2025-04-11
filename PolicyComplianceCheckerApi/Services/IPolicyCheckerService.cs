namespace PolicyComplianceCheckerApi.Services;

public interface IPolicyCheckerService
{
    Task<string> CheckPolicyAsync(IFormFile engagementLetter, string policyFileName, string policyVersion);
}