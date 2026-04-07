using System;
using System.Linq;
using System.Web.Mvc;
using VASReportingTool.Filters;
using VASReportingTool.Models;
using VASReportingTool.Repositories;
using VASReportingTool.Services;

namespace VASReportingTool.Controllers{
    [SessionAuthorize]
    public class ReportingApiController : Controller
    {
        private readonly IReportingRepository _repository;

        public ReportingApiController()
            : this(new SqlReportingRepository())
        {
        }

        public ReportingApiController(IReportingRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public JsonResult Index(DashboardRequest request)
        {
            try
            {
                var userId = (int)Session["UserId"];
                var user = _repository.GetUserById(userId);
                var isAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);
                var regions = isAdmin ? _repository.GetAllRegions() : _repository.GetRegionsByUser(userId);
                var rows = _repository.GetReportRows(userId, request, isAdmin);

                _repository.LogUserActivity(new UserActivityLog
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    SessionKey = Convert.ToString(Session["SessionKey"]),
                    ActionName = "ReportFetched",
                    Details = string.Format("Report fetched for region {0}, country {1}, operator {2}, service {3}.", request.RegionId, request.Country ?? "All", request.OperatorName ?? "All", request.ServiceName ?? "All"),
                    IpAddress = Convert.ToString(Session["UserIpAddress"]),
                    LocationText = Convert.ToString(Session["UserLocation"]),
                    UserAgent = Request.UserAgent,
                    CreatedOnUtc = DateTime.UtcNow
                });

                var response = new DashboardDataResponse
                {
                    Username = user.Username,
                    Role = user.Role,
                    Regions = regions,
                    Rows = rows,
                    Kpis = new
                    {
                        TotalRevenue = rows.Sum(r => r.TotalRevenue),
                        Renewals = rows.Sum(r => r.RenewalCount),
                        Activations = rows.Sum(r => r.ActivationCount),
                        ActiveBase = rows.Sum(r => r.ActiveBase)
                    }
                };

                return Json(response, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                LocalDiagnostics.Log("ReportingApiError", string.Format(
                    "RegionId: {0}{6}Country: {1}{6}Operator: {2}{6}Service: {3}{6}FromDate: {4}{6}ToDate: {5}{6}{7}",
                    request == null ? 0 : request.RegionId,
                    request == null ? string.Empty : request.Country ?? string.Empty,
                    request == null ? string.Empty : request.OperatorName ?? string.Empty,
                    request == null ? string.Empty : request.ServiceName ?? string.Empty,
                    request == null || !request.FromDate.HasValue ? string.Empty : request.FromDate.Value.ToString("yyyy-MM-dd"),
                    request == null || !request.ToDate.HasValue ? string.Empty : request.ToDate.Value.ToString("yyyy-MM-dd"),
                    Environment.NewLine,
                    ex));
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { message = "Dashboard data could not be loaded.", detail = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}


