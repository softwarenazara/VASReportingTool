using System;
using System.Web.Mvc;
using VASReportingTool.Filters;
using VASReportingTool.Repositories;
using VASReportingTool.Services;

namespace VASReportingTool.Controllers{
    [SessionAuthorize]
    public class RegionsApiController : Controller
    {
        private readonly IReportingRepository _repository;

        public RegionsApiController()
            : this(new SqlReportingRepository())
        {
        }

        public RegionsApiController(IReportingRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public JsonResult Index()
        {
            try
            {
                var userId = (int)Session["UserId"];
                var isAdmin = string.Equals(Convert.ToString(Session["Role"]), "Admin", StringComparison.OrdinalIgnoreCase);
                var regions = isAdmin ? _repository.GetAllRegions() : _repository.GetRegionsByUser(userId);
                return Json(regions, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                LocalDiagnostics.Log("RegionsApiError", "Index" + Environment.NewLine + ex);
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { message = "Regions could not be loaded.", detail = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult Countries(int regionId)
        {
            try
            {
                var userId = (int)Session["UserId"];
                var isAdmin = string.Equals(Convert.ToString(Session["Role"]), "Admin", StringComparison.OrdinalIgnoreCase);
                return Json(_repository.GetCountries(userId, regionId, isAdmin), JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                LocalDiagnostics.Log("RegionsApiError", string.Format("Countries regionId={0}{1}{2}", regionId, Environment.NewLine, ex));
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { message = "Countries could not be loaded.", detail = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult Operators(int regionId, string country)
        {
            try
            {
                var userId = (int)Session["UserId"];
                var isAdmin = string.Equals(Convert.ToString(Session["Role"]), "Admin", StringComparison.OrdinalIgnoreCase);
                return Json(_repository.GetOperators(userId, regionId, country, isAdmin), JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                LocalDiagnostics.Log("RegionsApiError", string.Format("Operators regionId={0}, country={1}{2}{3}", regionId, country ?? string.Empty, Environment.NewLine, ex));
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { message = "Operators could not be loaded.", detail = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult Services(int regionId, string country, string operatorName)
        {
            try
            {
                var userId = (int)Session["UserId"];
                var isAdmin = string.Equals(Convert.ToString(Session["Role"]), "Admin", StringComparison.OrdinalIgnoreCase);
                return Json(_repository.GetServices(userId, regionId, country, operatorName, isAdmin), JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                LocalDiagnostics.Log("RegionsApiError", string.Format("Services regionId={0}, country={1}, operator={2}{3}{4}", regionId, country ?? string.Empty, operatorName ?? string.Empty, Environment.NewLine, ex));
                Response.StatusCode = 500;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { message = "Services could not be loaded.", detail = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }
    }
}
