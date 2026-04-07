using System.Collections.Generic;

namespace VASReportingTool.Models
{
    public class DashboardFilterOptions
    {
        public IList<Region> Regions { get; set; }
        public IList<string> Operators { get; set; }
        public IList<string> Services { get; set; }
    }
}
