using System;
using System.Linq;
using System.Web.Mvc;
using VASReportingTool.Filters;
using VASReportingTool.Models;
using VASReportingTool.Repositories;

namespace VASReportingTool.Controllers
{
    [SessionAuthorize]
    public class ActivationController : Controller
    {
        private readonly IReportingRepository _repository;

        public ActivationController()
            : this(new SqlReportingRepository())
        {
        }

        public ActivationController(IReportingRepository repository)
        {
            _repository = repository;
        }

        // GET: /Activation/Index
        public ActionResult Index()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");

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

            return View(model);
        }

        // GET: /Activation/Data?regionId=&country=&operatorName=&serviceName=&fromDate=&toDate=
        [HttpGet]
        public JsonResult Data(int? regionId, string country, string operatorName, string serviceName, string fromDate, string toDate)
        {
            if (Session["UserId"] == null)
                return Json(new { error = "Unauthorized" }, JsonRequestBehavior.AllowGet);

            try
            {
                DateTime from;
                DateTime to;

                if (!DateTime.TryParse(fromDate, out from))
                    from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

                if (!DateTime.TryParse(toDate, out to))
                    to = DateTime.Today.AddDays(-1);

                if (to < from) to = from;

                var userId = (int)Session["UserId"];
                var user = _repository.GetUserById(userId);
                var isAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);
                var rows = _repository.GetActivationRows(
                    userId,
                    from,
                    to,
                    regionId ?? 0,
                    country,
                    operatorName,
                    serviceName,
                    isAdmin);

                return Json(new
                {
                    FromDate = from.ToString("yyyy-MM-dd"),
                    ToDate = to.ToString("yyyy-MM-dd"),
                    Total = rows.Sum(r => r.ActivationCnt),
                    Rows = rows.Select(r => new
                    {
                        ForDate = r.ForDate.ToString("dd MMM yyyy"),
                        Operator = r.Operator,
                        Country = r.Country,
                        Service = r.Service,
                        Source = r.Source,
                        ActivationCnt = r.ActivationCnt
                    })
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
