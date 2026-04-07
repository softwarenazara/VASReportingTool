using System.ComponentModel.DataAnnotations;

namespace VASReportingTool.Models
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [StringLength(128, MinimumLength = 8)]
        public string NewPassword { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; }

        public string ErrorMessage { get; set; }
        public string InfoMessage { get; set; }
    }
}
