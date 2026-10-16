using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace ECOLearning
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            // Trang danh mục: một trang cho mọi danh mục, slug sinh từ tên danh mục (Services/SlugHelper)
            routes.MapRoute(
                name: "DanhMuc",
                url: "danh-muc/{slug}",
                defaults: new { controller = "Category", action = "Index" },
                namespaces: new[] { "ECOLearning.Controllers" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional },
                namespaces: new[] { "ECOLearning.Controllers" }
            );
        }
    }
}
