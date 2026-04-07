using System.ComponentModel.DataAnnotations;

namespace VASReportingTool.Models
{
    public class ForgotPasswordViewModel
    {
        [Required]
        [StringLength(150)]
        public string UsernameOrEmail { get; set; }
        public string InfoMessage { get; set; }
        public string ErrorMessage { get; set; }
    }
}
