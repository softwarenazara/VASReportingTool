namespace VASReportingTool.Models
{
    public class IpLocationResult
    {
        public string IpAddress { get; set; }
        public string City { get; set; }
        public string Region { get; set; }
        public string Country { get; set; }
        public string Provider { get; set; }

        public string ToDisplayText()
        {
            var combined = string.Empty;
            if (!string.IsNullOrWhiteSpace(City))
            {
                combined = City;
            }
            if (!string.IsNullOrWhiteSpace(Region))
            {
                combined = string.IsNullOrWhiteSpace(combined) ? Region : combined + ", " + Region;
            }
            if (!string.IsNullOrWhiteSpace(Country))
            {
                combined = string.IsNullOrWhiteSpace(combined) ? Country : combined + ", " + Country;
            }
            return string.IsNullOrWhiteSpace(combined) ? "Location unavailable" : combined;
        }
    }
}
