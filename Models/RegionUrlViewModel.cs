using System.ComponentModel.DataAnnotations;

namespace VASReportingTool.Models
{
    public class RegionUrlViewModel
    {
        public int Id { get; set; }
        public int RegionId { get; set; }

        [Required]
        [StringLength(500)]
        [RegularExpression(@"^https?://.+$", ErrorMessage = "URL must start with http:// or https://")]
        public string Url { get; set; }

        public bool IsActive { get; set; }
    }
}
