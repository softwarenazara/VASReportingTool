using System.Web.Mvc;
using VASReportingTool.Filters;
using VASReportingTool.Models;
using VASReportingTool.Repositories;

namespace VASReportingTool.Controllers{
    [SessionAuthorize]
    public class RegionUrlsApiController : Controller
    {
        private readonly IReportingRepository _repository;

        public RegionUrlsApiController()
            : this(new SqlReportingRepository())
        {
        }

        public RegionUrlsApiController(IReportingRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public JsonResult Index()
        {
            var userId = (int)Session["UserId"];
            if ((string)Session["Role"] == "Admin")
            {
                return Json(_repository.GetRegionUrlsForAdmin(), JsonRequestBehavior.AllowGet);
            }

            return Json(_repository.GetRegionUrlsForUser(userId), JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [SessionAuthorize(Roles = "Admin")]
        public JsonResult Save(RegionUrlViewModel model)
        {
            if (!ModelState.IsValid)
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "Invalid URL payload." });
            }

            _repository.SaveRegionUrl(model);
            return Json(new { success = true });
        }

        [HttpPost]
        [SessionAuthorize(Roles = "Admin")]
        public JsonResult Delete(int id)
        {
            _repository.DeleteRegionUrl(id);
            return Json(new { success = true });
        }
    }
}


