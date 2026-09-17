using System;
using System.Web.Mvc;

namespace ECOLearning.Areas.Marketing.Controllers
{
    [Authorize(Roles = "Marketing")]
    public class DashboardController : Controller
    {
        // GET: Marketing/Dashboard
        // Thống kê chiến dịch đang chạy, số coupon đã dùng, doanh thu kích cầu
        public ActionResult Index()
        {
            return View();
        }
    }
}
