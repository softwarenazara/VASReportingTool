using System.Web.Mvc;
using System.Web.Routing;

namespace VASReportingTool.App_Start
{
    public static class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute("ApiAuthLogin", "api/auth/login", new { controller = "Account", action = "ApiLogin" });
            routes.MapRoute("ApiUsers", "api/users/{action}/{id}", new { controller = "UsersApi", action = "Index", id = UrlParameter.Optional });
            routes.MapRoute("ApiRegions", "api/regions/{action}/{id}", new { controller = "RegionsApi", action = "Index", id = UrlParameter.Optional });
            routes.MapRoute("ApiRegionUrls", "api/region-urls/{action}/{id}", new { controller = "RegionUrlsApi", action = "Index", id = UrlParameter.Optional });
            routes.MapRoute("ApiReporting", "api/reporting/{action}/{id}", new { controller = "ReportingApi", action = "Index", id = UrlParameter.Optional });
            routes.MapRoute("Default", "{controller}/{action}/{id}", new { controller = "Account", action = "Login", id = UrlParameter.Optional });
        }
    }
}
