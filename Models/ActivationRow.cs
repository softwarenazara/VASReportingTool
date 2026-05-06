using System;

namespace VASReportingTool.Models
{
    public class ActivationRow
    {
        public DateTime ForDate      { get; set; }
        public string   Operator     { get; set; }
        public string   Country      { get; set; }
        public string   Service      { get; set; }
        public string   Source       { get; set; }
        public long     ActivationCnt { get; set; }
    }
}
