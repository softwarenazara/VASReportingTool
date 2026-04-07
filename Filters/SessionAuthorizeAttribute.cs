using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace VASReportingTool.Filters
{
    public class SessionAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException("httpContext");
            }

            var session = httpContext.Session;
            if (session == null || session["UserId"] == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(Roles))
            {
                return true;
            }

            var currentRole = Convert.ToString(session["Role"]);
            var allowedRoles = Roles.Split(',').Select(role => role.Trim()).Where(role => role.Length > 0);
            return allowedRoles.Any(role => string.Equals(role, currentRole, StringComparison.OrdinalIgnoreCase));
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            var request = filterContext.HttpContext.Request;
            var requestPath = request.Path ?? string.Empty;
            var appRelativePath = request.AppRelativeCurrentExecutionFilePath ?? string.Empty;
            var isApiRequest = requestPath.IndexOf("/api/", StringComparison.OrdinalIgnoreCase) >= 0
                               || appRelativePath.StartsWith("~/api/", StringComparison.OrdinalIgnoreCase);

            if (isApiRequest || request.IsAjaxRequest())
            {
                filterContext.HttpContext.Response.StatusCode = 403;
                filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
                filterContext.Result = new JsonResult
                {
                    Data = new { message = "Authentication required.", requiresLogin = true, loginUrl = "/Account/Login" },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
                return;
            }

            filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new
            {
                controller = "Account",
                action = "Login"
            }));
        }
    }
}
