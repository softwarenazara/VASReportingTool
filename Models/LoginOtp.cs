using System;

namespace VASReportingTool.Models
{
    public class LoginOtp
    {
        public int LoginOtpId { get; set; }
        public int UserId { get; set; }
        public string OtpHash { get; set; }
        public string OtpSalt { get; set; }
        public DateTime ExpiresOnUtc { get; set; }
        public bool IsUsed { get; set; }
        public DateTime CreatedOnUtc { get; set; }
    }
}
