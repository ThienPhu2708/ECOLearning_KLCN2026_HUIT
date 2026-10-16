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

            // Cho phép Marketing và Admin kiểm thử
            bool isAllowed = userId != null && !string.IsNullOrEmpty(role) &&
                (role.Equals("Marketing", StringComparison.OrdinalIgnoreCase) ||
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
