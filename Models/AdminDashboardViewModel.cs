using System.Collections.Generic;

namespace VASReportingTool.Models
{
    public class AdminDashboardViewModel
    {
        public IList<User> Users { get; set; }
        public IList<UserEditViewModel> EditableUsers { get; set; }
        public IList<Region> Regions { get; set; }
        public IList<RegionUrl> RegionUrls { get; set; }
        public IList<UserActivityLog> RecentActivities { get; set; }
        public UserEditViewModel NewUser { get; set; }
        public RegionUrlViewModel NewRegionUrl { get; set; }
        public ActivityLogFilterViewModel ActivityFilter { get; set; }
        public bool IsArchiveMode { get; set; }
        public int ActivityWindowDays { get; set; }
        public int? EditingUserId { get; set; }
    }
}
