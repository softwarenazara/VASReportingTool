using System;

namespace VASReportingTool.Models
{
    public class ReportRow
    {
        public DateTime ReportDate { get; set; }
        public string ReportDateText
        {
            get { return ReportDate == DateTime.MinValue ? string.Empty : ReportDate.ToString("yyyy-MM-dd"); }
        }
        public int RegionId { get; set; }
        public string RegionName { get; set; }
        public string OperatorName { get; set; }
        public string ServiceName { get; set; }
        public string Country { get; set; }
        public string ActivationSource { get; set; }
        public string ActivationCategory { get; set; }
        public int TotalVisitors { get; set; }
        public int UniqueVisitors { get; set; }
        public int ActivationAttempts { get; set; }
        public int FreeTrials { get; set; }
        public int ActivationCount { get; set; }
        public decimal ActivationRevenue { get; set; }
        public int RenewalCount { get; set; }
        public decimal RenewalRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
        public int Churn { get; set; }
        public int UserChurn { get; set; }
        public int SystemChurn { get; set; }
        public int Deactivation { get; set; }
        public int GrossBase { get; set; }
        public int ActiveBase { get; set; }
    }
}
