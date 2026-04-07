using System;
using System.ComponentModel.DataAnnotations;

namespace VASReportingTool.Models
{
    public class OtpVerificationViewModel
    {
        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string OtpCode { get; set; }

        public string ErrorMessage { get; set; }
        public string InfoMessage { get; set; }
        public string MaskedEmail { get; set; }
        public int ResendCooldownSeconds { get; set; }
    }
}
