namespace PolicyComplianceCheckerApi.Services;

using PolicyComplianceCheckerApi.Models;

public interface IPolicyCheckerService
{
    Task<PolicyCheckerResult> CheckPolicyAsync(string userId, string engagementLetter, string policyFileName, string versionId);
}