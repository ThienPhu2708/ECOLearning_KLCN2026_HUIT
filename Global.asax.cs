using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace ECOLearning
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
            ECOLearning.Services.AppLog.KhoiDong();
            // Chạy tiếp các video đang chờ nén / đẩy lên R2 trước khi ứng dụng khởi động lại
            ECOLearning.Services.VideoStorageService.KhoiDong();
        }

        // CORS cho API ứng dụng (/api/...): app Flutter chạy trên Chrome gọi khác nguồn. Xác thực bằng header Authorization
        // (không dùng cookie) nên cho mọi nguồn; yêu cầu preflight OPTIONS trả 204 ngay, không vào controller
        protected void Application_BeginRequest()
        {
            if (!Request.Path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)) return;
            Response.Headers["Access-Control-Allow-Origin"] = "*";
            Response.Headers["Access-Control-Allow-Headers"] = "Authorization, Content-Type, Accept";
            Response.Headers["Access-Control-Allow-Methods"] = "GET, POST, PUT, DELETE, OPTIONS";
            Response.Headers["Access-Control-Max-Age"] = "86400";
            if (string.Equals(Request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                Response.StatusCode = 204;
                CompleteRequest();
            }
        }

        // Session vừa có (trước mọi controller): Admin / Marketing mất Session vì để trang lâu hoặc ứng dụng khởi động lại
        // thì khôi phục từ cookie ECO_Phien nếu mã phiên vẫn còn hiệu lực (PhienDocQuyenService.KhoiPhuc)
        protected void Application_PostAcquireRequestState()
        {
            ECOLearning.Services.PhienDocQuyenService.KhoiPhuc(new HttpContextWrapper(Context));
        }

        // Ứng dụng tắt = mọi phiên đăng nhập (lưu trong bộ nhớ) mất; ghi lý do vào App_Data/logs
        protected void Application_End()
        {
            ECOLearning.Services.AppLog.Tat();
        }

        // Lỗi ngoài action (action ném lỗi đã được GhiLogLoiAttribute ghi). Không xóa lỗi: hành vi trang lỗi giữ nguyên
        protected void Application_Error()
        {
            ECOLearning.Services.AppLog.GhiLoi("ASP.NET", Server.GetLastError(), new HttpContextWrapper(Context));
        }
    }
}
