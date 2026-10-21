using System.Web.Mvc;
using ECOLearning.Services;

namespace ECOLearning
{
    /// <summary>
    /// Ghi lỗi của action vào App_Data/logs (AppLog). Chỉ ghi, không xử lý: không đặt ExceptionHandled nên HandleErrorAttribute
    /// vẫn hiện trang lỗi như cũ. Đăng ký sau HandleErrorAttribute (FilterConfig) vì bộ lọc lỗi đăng ký sau chạy trước;
    /// HandleError đã xử lý thì lỗi không còn tới Application_Error, nên nếu không có bộ lọc này sẽ không có dấu vết nào.
    /// </summary>
    public class GhiLogLoiAttribute : IExceptionFilter
    {
        public void OnException(ExceptionContext filterContext)
        {
            AppLog.GhiLoi("MVC " + filterContext.RouteData.Values["controller"] + "/" + filterContext.RouteData.Values["action"],
                filterContext.Exception, filterContext.HttpContext);
        }
    }
}
