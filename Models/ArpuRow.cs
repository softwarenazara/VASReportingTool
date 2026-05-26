using System;

namespace VASReportingTool.Models
{
    public class ArpuRow
    {
        public string Operator { get; set; }
        public string Country { get; set; }
        public string Service { get; set; }
        public DateTime ActivationDate { get; set; }
        public string Source { get; set; }
        public int Activations { get; set; }
        public int FreeToPaid { get; set; }
        public int Churn { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal Arpu { get; set; }
        public DateTime BillingDate { get; set; }
    }
}
