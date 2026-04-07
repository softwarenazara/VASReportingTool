using System;
using System.Security.Principal;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;
using VASReportingTool.Services;

namespace VASReportingTool
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            App_Start.FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            App_Start.RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_AuthenticateRequest()
        {
            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null || string.IsNullOrWhiteSpace(authCookie.Value))
            {
                return;
            }

            var ticket = FormsAuthentication.Decrypt(authCookie.Value);
            if (ticket == null)
            {
                return;
            }

            var roles = string.IsNullOrWhiteSpace(ticket.UserData) ? new string[0] : new[] { ticket.UserData };
            Context.User = new GenericPrincipal(new FormsIdentity(ticket), roles);
        }

        protected void Application_Error()
        {
            var exception = Server.GetLastError();
            if (exception == null)
            {
                return;
            }

            LocalDiagnostics.Log("UnhandledException", exception.ToString());

            var httpException = exception as HttpException;
            var statusCode = httpException == null ? 500 : httpException.GetHttpCode();

            if ((Request.Path ?? string.Empty).IndexOf("/api/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Response.Clear();
                Response.StatusCode = statusCode;
                Response.ContentType = "application/json";
                Response.TrySkipIisCustomErrors = true;
                Response.Write("{\"message\":\"A server error occurred while processing the API request.\"}");
                Server.ClearError();
                return;
            }

            Server.ClearError();
            Response.Clear();
            Response.StatusCode = statusCode;
            Response.TrySkipIisCustomErrors = true;

            var routeData = new RouteData();
            routeData.Values["controller"] = "Error";
            routeData.Values["action"] = statusCode == 404 ? "NotFound" : "Index";

            IController controller = new VASReportingTool.Controllers.ErrorController();
            controller.Execute(new RequestContext(new HttpContextWrapper(Context), routeData));
        }
    }
}


