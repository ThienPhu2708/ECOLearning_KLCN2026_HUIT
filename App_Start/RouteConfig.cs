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

            // API cho ứng dụng di động: /api/v1/... khai báo bằng [Route] trong Controllers/Api (Tuan10/Task_Tuan10.docx)
            routes.MapMvcAttributeRoutes();
            routes.MapRoute(
                name: "ApiKhongTonTai",
                url: "api/{*path}",
                defaults: new { controller = "ApiNotFound", action = "Index" },
                namespaces: new[] { "ECOLearning.Controllers.Api" }
            );

            // Tất cả lĩnh vực (nút "Tất cả các lĩnh vực" ở trang chủ), /danh-muc không có slug
            // Trang danh mục: một trang cho mọi danh mục, slug sinh từ tên danh mục (Services/SlugHelper)
            routes.MapRoute(
                name: "TatCaDanhMuc",
                url: "danh-muc",
                defaults: new { controller = "Category", action = "All" },
                namespaces: new[] { "ECOLearning.Controllers" }
            );
            routes.MapRoute(
                name: "DanhMuc",
                url: "danh-muc/{slug}",
                defaults: new { controller = "Category", action = "Index" },
                namespaces: new[] { "ECOLearning.Controllers" }
            );

            // Trang kết quả tìm kiếm (SearchController), mở cho cả khách
            routes.MapRoute(
                name: "TimKiem",
                url: "tim-kiem",
                defaults: new { controller = "Search", action = "Index" },
                namespaces: new[] { "ECOLearning.Controllers" }
            );

            // Mã giảm giá của học viên (CouponController)
            routes.MapRoute(
                name: "ViMaGiamGia",
                url: "ma-giam-gia-cua-toi",
                defaults: new { controller = "Coupon", action = "Index" },
                namespaces: new[] { "ECOLearning.Controllers" }
            );
            routes.MapRoute(
                name: "ChiTietMaGiamGia",
                url: "ma-giam-gia/{code}",
                defaults: new { controller = "Coupon", action = "Details" },
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
