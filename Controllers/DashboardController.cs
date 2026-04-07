using System;
using System.Linq;
using System.Web.Mvc;
using VASReportingTool.Filters;
using VASReportingTool.Models;
using VASReportingTool.Repositories;

namespace VASReportingTool.Controllers{
    [SessionAuthorize]
    public class DashboardController : Controller
    {
        private readonly IReportingRepository _repository;

        public DashboardController()
            : this(new SqlReportingRepository())
        {
        }

        public DashboardController(IReportingRepository repository)
        {
            _repository = repository;
        }

        public ActionResult Index()
        {
            if (Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userId = (int)Session["UserId"];
            var user = _repository.GetUserById(userId);
            var isAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);
            var model = new DashboardViewModel
            {
                Username = user.Username,
                Role = user.Role,
                IsAdmin = isAdmin,
                Regions = isAdmin ? _repository.GetAllRegions() : _repository.GetRegionsByUser(userId)
            };

            _repository.LogUserActivity(new UserActivityLog
            {
                UserId = user.UserId,
                Username = user.Username,
                SessionKey = Convert.ToString(Session["SessionKey"]),
                ActionName = "DashboardViewed",
                Details = "Dashboard page opened.",
                IpAddress = Convert.ToString(Session["UserIpAddress"]),
                LocationText = Convert.ToString(Session["UserLocation"]),
                UserAgent = Request.UserAgent,
                CreatedOnUtc = DateTime.UtcNow
            });

            return View(model);
        }
    }
}


