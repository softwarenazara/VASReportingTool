using System;
using System.Web.Mvc;
using VASReportingTool.Filters;
using VASReportingTool.Models;
using VASReportingTool.Repositories;
using VASReportingTool.Services;

namespace VASReportingTool.Controllers{
    [SessionAuthorize(Roles = "Admin")]
    public class UsersApiController : Controller
    {
        private readonly IReportingRepository _repository;

        public UsersApiController()
            : this(new SqlReportingRepository())
        {
        }

        public UsersApiController(IReportingRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public JsonResult Index()
        {
            return Json(_repository.GetUsers(), JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult Save(UserEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "Invalid user payload." });
            }

            _repository.SaveUser(model);
            return Json(new { success = true });
        }

        [HttpPost]
        public JsonResult ResetPassword(int id, string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                Response.StatusCode = 400;
                return Json(new { success = false, message = "Password is required." });
            }

            var hasher = new PasswordHasher();
            var salt = hasher.GenerateSalt();
            var hash = hasher.HashPassword(password, salt);
            _repository.ResetPassword(id, hash, salt);
            return Json(new { success = true });
        }

        [HttpPost]
        public JsonResult SetStatus(int id, bool isActive)
        {
            _repository.SetUserStatus(id, isActive);
            return Json(new { success = true });
        }
    }
}


