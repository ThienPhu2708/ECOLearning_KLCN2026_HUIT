using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using ECOLearning.Models;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class InstructorBaseController : Controller
    {
        protected QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        public int CurrentTeacherId
        {
            get
            {
                if (Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out int id))
                {
                    return id;
                }

                // Fallback nếu tài khoản admin đang test hoặc chưa gán ID
                var defaultTeacher = db.NguoiDung.FirstOrDefault(u => u.vaiTro.Contains("GiaoVien") || u.vaiTro.Contains("Giáo viên") || u.vaiTro == "Instructor");
                return defaultTeacher != null ? defaultTeacher.maNguoiDung : 1;
            }
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var role = Session["Role"]?.ToString();
            var userId = Session["UserId"];

            // Cho phép Giáo viên và Quản trị viên (Admin) truy cập để kiểm thử
            bool isAllowed = userId != null && !string.IsNullOrEmpty(role) &&
                (role.Equals("GiaoVien", StringComparison.OrdinalIgnoreCase) ||
                 role.Equals("Giáo viên", StringComparison.OrdinalIgnoreCase) ||
                 role.Equals("Instructor", StringComparison.OrdinalIgnoreCase) ||
                 role.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
            {
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

        protected override void Dispose(bool disposing)
        {
            if (disposing && db != null)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
