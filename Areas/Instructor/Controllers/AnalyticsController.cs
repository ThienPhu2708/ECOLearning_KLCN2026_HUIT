using System;
using System.Web.Mvc;

namespace ECOLearning.Areas.Instructor.Controllers
{
    [Authorize(Roles = "GiaoVien")]
    public class AnalyticsController : Controller
    {
        // GET: Instructor/Analytics (UC12: Thống kê doanh thu & phân tích rating)
        public ActionResult Index(string period = "7days")
        {
            return View();
        }

        // GET: Instructor/Analytics/Export
        public ActionResult Export(string period)
        {
            return Content("CSV export placeholder");
        }
    }
}
