namespace PolicyComplianceCheckerApi.Services;

public interface IPolicyCheckerService
{
    Task<string> CheckPolicyAsync(string engagementLetter, string policyFileName, string policyVersion);
}