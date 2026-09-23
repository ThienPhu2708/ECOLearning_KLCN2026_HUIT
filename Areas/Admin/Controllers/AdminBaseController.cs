using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;

namespace ECOLearning.Areas.Admin.Controllers
{
    public class AdminBaseController : Controller
    {
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var sessionRole = Session["Role"]?.ToString();
            var sessionUserId = Session["UserId"];

            // Kiem tra trang thai dang nhap va vai tro Quan tri vien (Admin)
            if (sessionUserId == null || string.IsNullOrEmpty(sessionRole) || !sessionRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                // Kiểm tra nếu là AJAX request
                if (filterContext.HttpContext.Request.IsAjaxRequest() 
                    || filterContext.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    || (filterContext.HttpContext.Request.AcceptTypes != null && filterContext.HttpContext.Request.AcceptTypes.Any(t => t.Contains("json"))))
                {
                    filterContext.Result = new JsonResult
                    {
                        Data = new { success = false, message = "Phiên làm việc quản trị viên đã hết hạn. Vui lòng đăng nhập lại!", requireLogin = true },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                    return;
                }

                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(new { 
                        controller = "Account", 
                        action = "Login", 
                        area = "", 
                        returnUrl = filterContext.HttpContext.Request.RawUrl 
                    })
                );
                return;
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
