namespace PolicyComplianceCheckerApi.Models
{
    public class PolicyCheckerResult
    {
        public string ViolationsSasUri { get; set; }
        public int ViolationsCount { get; set; }
        public string EngagementLetterName { get; set; }
        public string PolicyFileName { get; set; }
        public string PolicyVersion { get; set; }
    }
}