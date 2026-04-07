using System;
using System.Linq;
using System.Web.Mvc;
using VASReportingTool.Filters;
using VASReportingTool.Models;
using VASReportingTool.Repositories;

namespace VASReportingTool.Controllers{
    [SessionAuthorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private const int LatestActivityWindowDays = 10;
        private readonly IReportingRepository _repository;

        public AdminController()
            : this(new SqlReportingRepository())
        {
        }

        public AdminController(IReportingRepository repository)
        {
            _repository = repository;
        }

        public ActionResult Index(string username, string actionName, string ipAddress, DateTime? fromUtc, DateTime? toUtc, int? top)
        {
            var filter = BuildLatestActivityFilter(username, actionName, ipAddress, fromUtc, toUtc);
            var model = BuildModel(filter, false);
            return View(model);
        }

        public ActionResult ActivityArchive(string username, string actionName, string ipAddress, DateTime? fromUtc, DateTime? toUtc)
        {
            var filter = new ActivityLogFilterViewModel
            {
                Username = username,
                ActionName = actionName,
                IpAddress = ipAddress,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                Top = int.MaxValue
            };

            return View("Index", BuildModel(filter, true));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateUser(UserEditViewModel model, int[] regionIds)
        {
            NormalizeUserModel(model);
            model.RegionIds = regionIds == null ? new System.Collections.Generic.List<int>() : regionIds.ToList();
            model.IsActive = true;

            if (string.IsNullOrWhiteSpace(model.Password))
            {
                ModelState.AddModelError("Password", "Password is required for new users.");
            }

            ValidateUserUniqueness(model);

            if (!ModelState.IsValid)
            {
                var vm = BuildModel(BuildLatestActivityFilter(null, null, null, null, null), false);
                vm.NewUser = model;
                return View("Index", vm);
            }

            _repository.SaveUser(model);
            LogAdminAction("UserCreated", "Created user " + model.Username + " with email " + model.Email + ".");
            TempData["AdminMessage"] = "User created successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveUser(UserEditViewModel model, int[] regionIds)
        {
            NormalizeUserModel(model);
            model.RegionIds = regionIds == null ? new System.Collections.Generic.List<int>() : regionIds.ToList();

            ValidateUserUniqueness(model);

            if (!ModelState.IsValid)
            {
                var vm = BuildModel(BuildLatestActivityFilter(null, null, null, null, null), false);
                ApplyEditedUser(vm, model);
                vm.EditingUserId = model.UserId;
                return View("Index", vm);
            }

            _repository.SaveUser(model);
            LogAdminAction("UserUpdated", "Updated user " + model.Username + ".");
            TempData["AdminMessage"] = "User details updated successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SaveRegionUrl(RegionUrlViewModel model)
        {
            NormalizeRegionUrlModel(model);

            if (!ModelState.IsValid)
            {
                var vm = BuildModel(BuildLatestActivityFilter(null, null, null, null, null), false);
                vm.NewRegionUrl = model;
                return View("Index", vm);
            }

            var existingRegionUrl = _repository.GetRegionUrlsForAdmin()
                .FirstOrDefault(item => item.RegionId == model.RegionId);
            var isUpdate = model.Id > 0 || existingRegionUrl != null;

            _repository.SaveRegionUrl(model);
            LogAdminAction(isUpdate ? "RegionUrlUpdated" : "RegionUrlSaved", (isUpdate ? "Updated" : "Saved") + " region URL for region id " + model.RegionId + ".");
            TempData["AdminMessage"] = isUpdate ? "Region URL updated successfully." : "Region URL saved successfully.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetUserStatus(int id, bool isActive)
        {
            _repository.SetUserStatus(id, isActive);
            LogAdminAction("UserStatusChanged", "Updated user id " + id + " active=" + isActive + ".");
            TempData["AdminMessage"] = "User status updated successfully.";
            return RedirectToAction("Index");
        }

        private void NormalizeUserModel(UserEditViewModel model)
        {
            if (model == null)
            {
                return;
            }

            model.Username = (model.Username ?? string.Empty).Trim();
            model.Email = (model.Email ?? string.Empty).Trim();
            if (model.Password != null)
            {
                model.Password = model.Password.Trim();
            }
        }

        private static void NormalizeRegionUrlModel(RegionUrlViewModel model)
        {
            if (model == null)
            {
                return;
            }

            model.Url = (model.Url ?? string.Empty).Trim();
        }

        private void ValidateUserUniqueness(UserEditViewModel model)
        {
            if (model == null)
            {
                return;
            }

            var users = _repository.GetUsers();

            if (users.Any(user =>
                user.UserId != model.UserId &&
                string.Equals(user.Username, model.Username, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("Username", "Username already exists. Please choose a different username.");
            }

            if (users.Any(user =>
                user.UserId != model.UserId &&
                string.Equals((user.Email ?? string.Empty).Trim(), model.Email, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError("Email", "Email already exists. Use a unique email address for each user so OTP goes to the correct mailbox.");
            }
        }

        private AdminDashboardViewModel BuildModel(ActivityLogFilterViewModel filter, bool isArchiveMode)
        {
            var users = _repository.GetUsers();
            var regions = _repository.GetAllRegions();
            var regionUrls = _repository.GetRegionUrlsForAdmin();
            return new AdminDashboardViewModel
            {
                Users = users,
                EditableUsers = users.Select(user => new UserEditViewModel
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    Email = user.Email,
                    Role = user.Role,
                    IsActive = user.IsActive,
                    RegionIds = _repository.GetRegionsByUser(user.UserId).Select(region => region.RegionId).ToList()
                }).ToList(),
                Regions = regions,
                RegionUrls = regionUrls,
                RecentActivities = _repository.SearchActivityLogs(filter),
                NewUser = new UserEditViewModel { IsActive = true },
                NewRegionUrl = new RegionUrlViewModel
                {
                    IsActive = true,
                    RegionId = regions.Select(region => region.RegionId).FirstOrDefault()
                },
                ActivityFilter = filter,
                IsArchiveMode = isArchiveMode,
                ActivityWindowDays = LatestActivityWindowDays
            };
        }

        private static ActivityLogFilterViewModel BuildLatestActivityFilter(string username, string actionName, string ipAddress, DateTime? fromUtc, DateTime? toUtc)
        {
            return new ActivityLogFilterViewModel
            {
                Username = username,
                ActionName = actionName,
                IpAddress = ipAddress,
                FromUtc = fromUtc ?? DateTime.UtcNow.Date.AddDays(-LatestActivityWindowDays),
                ToUtc = toUtc ?? DateTime.UtcNow,
                Top = 500
            };
        }

        private static void ApplyEditedUser(AdminDashboardViewModel viewModel, UserEditViewModel editedUser)
        {
            if (viewModel == null || viewModel.EditableUsers == null || editedUser == null)
            {
                return;
            }

            var existing = viewModel.EditableUsers.FirstOrDefault(item => item.UserId == editedUser.UserId);
            if (existing == null)
            {
                return;
            }

            existing.Username = editedUser.Username;
            existing.Email = editedUser.Email;
            existing.Password = editedUser.Password;
            existing.Role = editedUser.Role;
            existing.IsActive = editedUser.IsActive;
            existing.RegionIds = editedUser.RegionIds ?? new System.Collections.Generic.List<int>();
        }

        private void LogAdminAction(string action, string details)
        {
            var userId = (int)Session["UserId"];
            var user = _repository.GetUserById(userId);
            _repository.LogUserActivity(new UserActivityLog
            {
                UserId = user.UserId,
                Username = user.Username,
                SessionKey = Convert.ToString(Session["SessionKey"]),
                ActionName = action,
                Details = details,
                IpAddress = Convert.ToString(Session["UserIpAddress"]),
                LocationText = Convert.ToString(Session["UserLocation"]),
                UserAgent = Request.UserAgent,
                CreatedOnUtc = DateTime.UtcNow
            });
        }
    }
}




