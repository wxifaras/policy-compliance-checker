namespace PolicyComplianceCheckerApi.Services;

public interface IPolicyCheckerService
{
    Task CheckPolicyAsync(IFormFile engagementLetter, string policyFileName, string policyVersion);
}