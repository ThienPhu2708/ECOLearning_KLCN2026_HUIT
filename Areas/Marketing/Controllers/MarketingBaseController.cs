using System;
using System.Web.Mvc;
using System.Web.Routing;
using ECOLearning.Models;

namespace ECOLearning.Areas.Marketing.Controllers
{
    public class MarketingBaseController : Controller
    {
        protected QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var role = Session["Role"]?.ToString();
            var userId = Session["UserId"];

            // Chỉ nhân viên Marketing (phân tách vai trò): Admin không tạo / sửa khuyến mãi, chiến dịch, giá bán mà chỉ giám sát
            // và tạm dừng ở Admin / Giám sát Marketing (Areas/Admin/Controllers/MarketingController.cs)
            bool isAllowed = userId != null && string.Equals(role, "Marketing", StringComparison.OrdinalIgnoreCase);

            if (!isAllowed && userId != null && string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                const string thongBao = "Khu Marketing chỉ dành cho nhân viên Marketing. Quản trị viên theo dõi và tạm dừng khuyến mãi tại trang Giám sát Marketing.";
                var urlGiamSat = new UrlHelper(filterContext.RequestContext).Action("Index", "Marketing", new { area = "Admin" });
                if (filterContext.HttpContext.Request.IsAjaxRequest())
                {
                    filterContext.Result = new JsonResult { Data = new { success = false, message = thongBao, redirectUrl = urlGiamSat }, JsonRequestBehavior = JsonRequestBehavior.AllowGet };
                }
                else
                {
                    TempData["InfoMessage"] = thongBao;
                    filterContext.Result = new RedirectResult(urlGiamSat);
                }
                return;
            }

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

        /// <summary>Mã người dùng đang đăng nhập (đã được OnActionExecuting bảo đảm có).</summary>
        protected int MaNguoiDungHienTai
        {
            get
            {
                int id;
                return Session["UserId"] != null && int.TryParse(Session["UserId"].ToString(), out id) ? id : 0;
            }
        }

        /// <summary>Ngày từ chuỗi yyyy-MM-dd của ô input type="date" (null nếu trống / sai định dạng).</summary>
        protected static DateTime? DocNgay(string value)
        {
            DateTime d;
            return DateTime.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out d) ? d : (DateTime?)null;
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
