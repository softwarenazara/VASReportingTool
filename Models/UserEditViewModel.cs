using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace VASReportingTool.Models
{
    public class UserEditViewModel
    {
        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Enter a valid email address.")]
        [StringLength(150)]
        public string Email { get; set; }

        [StringLength(128)]
        public string Password { get; set; }

        [Required]
        public string Role { get; set; }

        public bool IsActive { get; set; }
        public IList<int> RegionIds { get; set; }
    }
}
