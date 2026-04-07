using System;

namespace VASReportingTool.Models
{
    public class DashboardRequest
    {
        public int RegionId { get; set; }
        public string Country { get; set; }
        public string OperatorName { get; set; }
        public string ServiceName { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string ViewMode { get; set; }
    }
}
