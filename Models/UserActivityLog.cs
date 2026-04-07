using System;

namespace VASReportingTool.Models
{
    public class UserActivityLog
    {
        public int ActivityLogId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; }
        public string SessionKey { get; set; }
        public string ActionName { get; set; }
        public string Details { get; set; }
        public string IpAddress { get; set; }
        public string LocationText { get; set; }
        public string UserAgent { get; set; }
        public DateTime CreatedOnUtc { get; set; }
    }
}
