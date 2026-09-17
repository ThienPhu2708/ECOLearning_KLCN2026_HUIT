using System;
using System.Web.Mvc;

namespace ECOLearning.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        // GET: Admin/Dashboard
        // Thống kê 4 card số liệu (doanh thu, người dùng, khóa học đang chạy, khóa chờ duyệt)
        public ActionResult Index()
        {
            return View();
        }
    }
}
