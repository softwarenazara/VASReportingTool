using System.Collections.Generic;

namespace VASReportingTool.Models
{
    public class DashboardViewModel
    {
        public string Username { get; set; }
        public string Role { get; set; }
        public bool IsAdmin { get; set; }
        public IList<Region> Regions { get; set; }
    }
}
