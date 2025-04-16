namespace PolicyComplianceCheckerApi.Services
{
    using PolicyComplianceCheckerApi.Models;

    public interface IAzureSignalRService
    {
        Task SendPolicyResultAsync(string userId, PolicyCheckerResult policyCheckerResult);
        Task SendProgressAsync(string userId, int progress);
    }
}