using System;
using System.Collections.Generic;
using System.Linq;

namespace VASReportingTool.Models
{
    public class DashboardViewModel
    {
        public string Username { get; set; }
        public string Role { get; set; }
        public bool IsAdmin { get; set; }
        public IList<Region> Regions { get; set; }

        public int DefaultRegionId
        {
            get
            {
                if (Regions == null || Regions.Count == 0)
                {
                    return 0;
                }

                var africaRegion = Regions.FirstOrDefault(region =>
                    string.Equals(region.Name, "Africa", StringComparison.OrdinalIgnoreCase));

                return africaRegion != null ? africaRegion.RegionId : Regions[0].RegionId;
            }
        }
    }
}
