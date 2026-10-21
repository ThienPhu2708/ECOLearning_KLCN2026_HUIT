using System.Linq;
using System.Web.Mvc;
using ECOLearning.Services;

namespace ECOLearning
{
    /// <summary>
    /// Gắn lên action của người học có ghi dữ liệu (giỏ hàng, thanh toán, thả tim, lưu mã, đánh giá, báo cáo...).
    /// Tài khoản Admin / Marketing gọi tới (kể cả gửi thẳng yêu cầu, bỏ qua nút đã làm mờ) bị từ chối:
    /// AJAX / POST nhận JSON { success: false, staffReadOnly: true, message }, trang GET được đưa về Trang chủ.
    /// </summary>
    public class StaffReadOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (!StaffViewMode.LaChiXem(filterContext.HttpContext.Session)) return;

            var req = filterContext.HttpContext.Request;
            bool traJson = req.HttpMethod != "GET" || req.IsAjaxRequest()
                || (req.AcceptTypes != null && req.AcceptTypes.Any(t => t.Contains("json")));
            if (traJson)
            {
                filterContext.Result = new JsonResult
                {
                    Data = new { success = false, staffReadOnly = true, message = StaffViewMode.ThongBao },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }
            else
            {
                filterContext.Result = new RedirectResult(new UrlHelper(filterContext.RequestContext).Action("Index", "Home", new { area = "" }));
            }
        }
    }
}
