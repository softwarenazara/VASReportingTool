using System.Collections.Generic;

namespace VASReportingTool.Models
{
    public class DashboardDataResponse
    {
        public string Username { get; set; }
        public string Role { get; set; }
        public IList<Region> Regions { get; set; }
        public IList<ReportRow> Rows { get; set; }
        public object Kpis { get; set; }
    }
}
