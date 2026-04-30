using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using VASReportingTool.Filters;
using VASReportingTool.Models;
using VASReportingTool.Repositories;

namespace VASReportingTool.Controllers
{
    [SessionAuthorize]
    public class ToolSummaryController : Controller
    {
        private readonly IReportingRepository _repository;

        public ToolSummaryController()
            : this(new SqlReportingRepository())
        {
        }

        public ToolSummaryController(IReportingRepository repository)
        {
            _repository = repository;
        }

        public ActionResult Index()
        {
            if (Session["UserId"] == null)
                return RedirectToAction("Login", "Account");
            return View();
        }

        [HttpGet]
        public JsonResult Data()
        {
            var userId = (int)Session["UserId"];
            var user = _repository.GetUserById(userId);
            var isAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);

            var today      = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var yesterday  = today.AddDays(-1);

            // First day of month — nothing to show yet
            if (yesterday < monthStart)
            {
                return Json(new
                {
                    MonthStart = monthStart.ToString("yyyy-MM-dd"),
                    Yesterday  = (string)null,
                    Days       = new int[0],
                    DayDates   = new string[0],
                    Rows       = new object[0],
                    Message    = "No data yet for the current month."
                }, JsonRequestBehavior.AllowGet);
            }

            // Build list of all days in range
            var days = new List<DateTime>();
            for (var d = monthStart; d <= yesterday; d = d.AddDays(1))
                days.Add(d);

            // Single bulk fetch for the entire month
            var monthRows = _repository.GetReportRows(userId,
                new DashboardRequest { FromDate = monthStart, ToDate = yesterday }, isAdmin);

            // Presence map: "region|country|operator|service" → set of date strings
            var presence = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in monthRows)
            {
                var hasData = row.TotalRevenue > 0 || row.ActivationCount > 0
                           || row.RenewalCount  > 0 || row.UserChurn > 0 || row.SystemChurn > 0;
                if (!hasData) continue;

                var k = Key(row.RegionName, row.Country, row.OperatorName, row.ServiceName);
                if (!presence.ContainsKey(k))
                    presence[k] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                presence[k].Add(row.ReportDate.Date.ToString("yyyy-MM-dd"));
            }

            // Master combination list — respects region access
            var regions = isAdmin ? _repository.GetAllRegions() : _repository.GetRegionsByUser(userId);
            var rows2   = new List<object>();

            foreach (var region in regions)
            {
                IList<string> countries;
                try { countries = _repository.GetCountries(userId, region.RegionId, isAdmin); }
                catch { continue; }

                foreach (var country in countries)
                {
                    IList<string> operators;
                    try { operators = _repository.GetOperators(userId, region.RegionId, country, isAdmin); }
                    catch { continue; }

                    foreach (var op in operators)
                    {
                        IList<string> services;
                        try { services = _repository.GetServices(userId, region.RegionId, country, op, isAdmin); }
                        catch { continue; }

                        foreach (var svc in services)
                        {
                            var k = Key(region.Name, country, op, svc);
                            HashSet<string> datesWithData;
                            presence.TryGetValue(k, out datesWithData);

                            var dayStatuses = days
                                .Select(d => datesWithData != null
                                    && datesWithData.Contains(d.ToString("yyyy-MM-dd")))
                                .ToArray();

                            // Only include combinations that have at least one missing day
                            if (dayStatuses.Any(s => !s))
                            {
                                rows2.Add(new
                                {
                                    Region       = region.Name,
                                    Country      = country,
                                    Operator     = op,
                                    Service      = svc,
                                    DayStatuses  = dayStatuses,
                                    MissingCount = dayStatuses.Count(s => !s)
                                });
                            }
                        }
                    }
                }
            }

            return Json(new
            {
                MonthStart = monthStart.ToString("yyyy-MM-dd"),
                Yesterday  = yesterday.ToString("yyyy-MM-dd"),
                Days       = days.Select(d => d.Day).ToArray(),
                DayDates   = days.Select(d => d.ToString("yyyy-MM-dd")).ToArray(),
                Rows       = rows2
            }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Hourly()
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

        [HttpGet]
        public JsonResult HourlyData(int? regionId, string country, string operatorName, string serviceName)
        {
            if (Session["UserId"] == null)
                return Json(new { error = "Unauthorized" }, JsonRequestBehavior.AllowGet);

            try
            {
                var userId = (int)Session["UserId"];
                var user = _repository.GetUserById(userId);
                var isAdmin = string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase);

                var today       = DateTime.Today;
                var yesterday   = today.AddDays(-1);
                var sevenDaysAgo = today.AddDays(-7);

                var baseReq = new DashboardRequest
                {
                    RegionId     = regionId ?? 0,
                    Country      = country,
                    OperatorName = operatorName,
                    ServiceName  = serviceName,
                    ViewMode     = "Hourly"
                };

                var todayRows     = _repository.GetReportRows(userId, WithDates(baseReq, today,       today),     isAdmin);
                var yesterdayRows = _repository.GetReportRows(userId, WithDates(baseReq, yesterday,   yesterday), isAdmin);
                var last7Rows     = _repository.GetReportRows(userId, WithDates(baseReq, sevenDaysAgo, yesterday), isAdmin);

                var todayMap     = BuildHourlyMap(todayRows);
                var yesterdayMap = BuildHourlyMap(yesterdayRows);
                var last7Map     = BuildHourlyMap(last7Rows);

                int distinctDays = last7Rows.Select(r => r.ReportDate.Date).Distinct().Count();
                if (distinctDays < 1) distinctDays = 1;

                var allKeys = todayMap.Keys
                    .Union(yesterdayMap.Keys)
                    .Union(last7Map.Keys)
                    .Distinct()
                    .OrderBy(k => k)
                    .ToList();

                var availableHours = allKeys
                    .Select(k => int.Parse(k.Split('|')[3]))
                    .Distinct()
                    .OrderBy(h => h)
                    .ToList();

                var currentHour = todayRows
                    .Select(r => r.Hour)
                    .Distinct()
                    .DefaultIfEmpty(-1)
                    .Max();

                if (currentHour < 0)
                {
                    currentHour = availableHours.DefaultIfEmpty(0).Max();
                }

                var rows = new List<object>();
                foreach (var k in allKeys)
                {
                    HourlyAgg t, y, s;
                    todayMap.TryGetValue(k, out t);
                    yesterdayMap.TryGetValue(k, out y);
                    last7Map.TryGetValue(k, out s);

                    var parts = k.Split('|');
                    rows.Add(new
                    {
                        Country  = parts[0],
                        Operator = parts[1],
                        Service  = parts[2],
                        Hour     = int.Parse(parts[3]),
                        // Revenue (Total)
                        TodayRev  = t != null ? (long)Math.Round(t.Revenue)   : 0L,
                        YestRev   = y != null ? (long)Math.Round(y.Revenue)   : 0L,
                        Avg7Rev   = s != null ? (long)Math.Round(s.Revenue / distinctDays) : 0L,
                        // Activation (Free + Paid)
                        TodayActiv = t != null ? t.Activation : 0,
                        YestActiv  = y != null ? y.Activation  : 0,
                        Avg7Activ  = s != null ? (int)Math.Round((double)s.Activation / distinctDays) : 0,
                        // Renewal Count
                        TodayRenewal = t != null ? t.Renewal : 0,
                        YestRenewal  = y != null ? y.Renewal  : 0,
                        Avg7Renewal  = s != null ? (int)Math.Round((double)s.Renewal / distinctDays) : 0,
                        // Churn (User + System)
                        TodayChurn = t != null ? t.Churn      : 0,
                        YestChurn  = y != null ? y.Churn       : 0,
                        Avg7Churn  = s != null ? (int)Math.Round((double)s.Churn / distinctDays) : 0,
                        // Good Base
                        TodayGood  = t != null ? t.GoodBase   : 0,
                        YestGood   = y != null ? y.GoodBase    : 0,
                        Avg7Good   = s != null ? (int)Math.Round((double)s.GoodBase / distinctDays) : 0,
                        // Bad Base
                        TodayBad   = t != null ? t.BadBase    : 0,
                        YestBad    = y != null ? y.BadBase     : 0,
                        Avg7Bad    = s != null ? (int)Math.Round((double)s.BadBase / distinctDays) : 0
                    });
                }

                return Json(new
                {
                    Date      = today.ToString("yyyy-MM-dd"),
                    Yesterday = yesterday.ToString("yyyy-MM-dd"),
                    Days7From = sevenDaysAgo.ToString("yyyy-MM-dd"),
                    CurrentHour = currentHour,
                    AvailableHours = availableHours,
                    Rows      = rows
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        public ActionResult ArpuData()
        {
            ViewBag.Title = "ARPU Data";
            ViewBag.Feature = "ARPU Data";
            return View("WorkInProgress");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static DashboardRequest WithDates(DashboardRequest src, DateTime from, DateTime to)
        {
            return new DashboardRequest
            {
                RegionId     = src.RegionId,
                Country      = src.Country,
                OperatorName = src.OperatorName,
                ServiceName  = src.ServiceName,
                ViewMode     = src.ViewMode,
                FromDate     = from,
                ToDate       = to
            };
        }

        private static Dictionary<string, HourlyAgg> BuildHourlyMap(IList<ReportRow> rows)
        {
            var map = new Dictionary<string, HourlyAgg>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
            {
                var h = row.Hour != 0 ? row.Hour : row.ReportDate.Hour;
                var key = string.Join("|",
                    (row.Country      ?? "").Trim(),
                    (row.OperatorName ?? "").Trim(),
                    (row.ServiceName  ?? "").Trim(),
                    h.ToString("D2"));

                HourlyAgg agg;
                if (!map.TryGetValue(key, out agg))
                {
                    agg = new HourlyAgg();
                    map[key] = agg;
                }
                agg.Revenue    += row.TotalRevenue;
                agg.Activation += row.FreeTrials + row.ActivationCount;
                agg.Renewal    += row.RenewalCount;
                agg.Churn      += row.UserChurn + row.SystemChurn;
                agg.GoodBase   += row.GoodBase;
                agg.BadBase    += row.BadBase;
            }
            return map;
        }

        private class HourlyAgg
        {
            public decimal Revenue    { get; set; }
            public int     Activation { get; set; }
            public int     Renewal    { get; set; }
            public int     Churn      { get; set; }
            public int     GoodBase   { get; set; }
            public int     BadBase    { get; set; }
        }

        private static string Key(string region, string country, string op, string service)
        {
            return string.Join("|",
                (region  ?? "").Trim().ToUpperInvariant(),
                (country ?? "").Trim().ToUpperInvariant(),
                (op      ?? "").Trim().ToUpperInvariant(),
                (service ?? "").Trim().ToUpperInvariant());
        }
    }
}
