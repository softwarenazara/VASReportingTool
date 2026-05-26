namespace VASReportingTool.Models
{
    public class ArpuReportRequest
    {
        public string Region { get; set; }
        public string Country { get; set; }
        public string Operator { get; set; }
        public string Service { get; set; }
        public string FromDate { get; set; }
        public string ToDate { get; set; }
    }
}
