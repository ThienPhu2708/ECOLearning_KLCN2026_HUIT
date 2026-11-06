using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Admin.Controllers
{
    public class AdminBaseController : Controller
    {
        private static readonly HashSet<string> ControllersKiemDuyetNoiDung = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CourseApproval", "CourseRevision", "TeacherApproval", "TeacherSpecialty"
        };

        /// <summary>Đúng khi phiên hiện tại là Admin gốc, có toàn quyền trong khu vực quản trị.</summary>
        protected bool LaAdminGoc => AccountAdminService.LaAdminGoc(Session["Role"] as string);

        /// <summary>Đúng khi phiên hiện tại chỉ có quyền duyệt giáo viên, khóa học, bản sửa và chuyên môn.</summary>
        protected bool LaAdminKiemDuyetNoiDung => AccountAdminService.LaAdminKiemDuyetNoiDung(Session["Role"] as string);

        protected void GhiHoatDong(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, string action, string type, string id, string note)
        {
            var audit = new AccountAdminService(db);
            if (audit.IsInstalled()) audit.GhiNhatKy(Convert.ToInt32(Session["UserId"]), action, type, id, note);
        }
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var sessionRole = Session["Role"]?.ToString();
            var sessionUserId = Session["UserId"];

            // Admin gốc và Quản trị viên kiểm duyệt nội dung đều vào được Area Admin,
            // nhưng quyền của vai trò kiểm duyệt được chặn tiếp theo từng controller bên dưới.
            if (sessionUserId == null || string.IsNullOrEmpty(sessionRole) || !AccountAdminService.LaNhanVienQuanTri(sessionRole)
                || !AdminEmailOtpService.IsVerified(Session))
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

            if (AccountAdminService.LaAdminKiemDuyetNoiDung(sessionRole)
                && !ControllersKiemDuyetNoiDung.Contains(filterContext.ActionDescriptor.ControllerDescriptor.ControllerName))
            {
                if (filterContext.HttpContext.Request.IsAjaxRequest()
                    || filterContext.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    || (filterContext.HttpContext.Request.AcceptTypes != null && filterContext.HttpContext.Request.AcceptTypes.Any(t => t.Contains("json"))))
                {
                    filterContext.Result = new JsonResult
                    {
                        Data = new { success = false, forbidden = true, message = "Bạn chỉ có quyền kiểm duyệt nội dung." },
                        JsonRequestBehavior = JsonRequestBehavior.AllowGet
                    };
                }
                else
                {
                    filterContext.Result = new HttpStatusCodeResult(403, "Bạn chỉ có quyền kiểm duyệt nội dung.");
                }
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        /// <summary>Trang có layout Admin: đếm yêu cầu chờ xử lý để menu hiện chấm đỏ.</summary>
        protected override void OnResultExecuting(ResultExecutingContext filterContext)
        {
            if (filterContext.Result is ViewResult && !filterContext.IsChildAction && ViewBag.AdminPending == null)
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    ViewBag.AdminPending = new AdminPendingService(db).Dem();
                }
            }
            base.OnResultExecuting(filterContext);
        }
    }
}
