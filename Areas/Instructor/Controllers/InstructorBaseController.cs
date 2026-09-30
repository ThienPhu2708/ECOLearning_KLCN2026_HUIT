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
            var userIdObj = Session["UserId"];
            if (userIdObj == null || !int.TryParse(userIdObj.ToString(), out int userId))
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

            // Đồng bộ trực tiếp vai trò mới nhất từ CSDL SQL Server (chống bypass quyền khi CSDL bị thay đổi)
            var user = db.NguoiDung.Find(userId);
            if (user == null || (user.trangThai.HasValue && !user.trangThai.Value))
            {
                Session.Clear();
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(new { controller = "Account", action = "Login", area = "" })
                );
                return;
            }

            // Cập nhật lại Session vai trò mới nhất theo CSDL
            Session["Role"] = user.vaiTro;
            var role = user.vaiTro;

            // Chỉ cho phép Giáo viên và Quản trị viên (Admin) truy cập Dashboard
            bool isAllowed = !string.IsNullOrEmpty(role) &&
                (role.Equals("GiaoVien", StringComparison.OrdinalIgnoreCase) ||
                 role.Equals("Giáo viên", StringComparison.OrdinalIgnoreCase) ||
                 role.Equals("Instructor", StringComparison.OrdinalIgnoreCase) ||
                 role.Equals("Admin", StringComparison.OrdinalIgnoreCase));

            if (!isAllowed)
            {
                // Nếu vai trò trong CSDL đã chuyển thành Học viên -> Chặn ngay và điều hướng về Trang chủ
                filterContext.Result = new RedirectToRouteResult(
                    new RouteValueDictionary(new { 
                        controller = "Home", 
                        action = "Index", 
                        area = "" 
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
