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
