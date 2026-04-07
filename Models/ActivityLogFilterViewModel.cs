using System;

namespace VASReportingTool.Models
{
    public class ActivityLogFilterViewModel
    {
        public string Username { get; set; }
        public string ActionName { get; set; }
        public string IpAddress { get; set; }
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public int Top { get; set; }
    }
}
