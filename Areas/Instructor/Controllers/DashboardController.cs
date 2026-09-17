using System;
using System.Web.Mvc;

namespace ECOLearning.Areas.Instructor.Controllers
{
    [Authorize(Roles = "GiaoVien")]
    public class DashboardController : Controller
    {
        // GET: Instructor/Dashboard
        // 4 Thẻ số liệu tổng quan doanh thu, học viên, đánh giá, top 3 khóa học
        public ActionResult Index()
        {
            return View();
        }
    }
}
