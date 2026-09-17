using System;
using System.Web.Mvc;

namespace ECOLearning.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SystemReportController : Controller
    {
        // GET: Admin/SystemReport (UC16: Báo cáo thống kê toàn sàn)
        public ActionResult Index(string period = "month")
        {
            return View();
        }

        // GET: Admin/SystemReport/ExportExcel
        public ActionResult ExportExcel(string period)
        {
            // Xuất báo cáo doanh thu ra file Excel
            return Content("Excel export placeholder");
        }

        // GET: Admin/SystemReport/ExportPdf
        public ActionResult ExportPdf(string period)
        {
            // Xuất báo cáo ra file PDF
            return Content("Pdf export placeholder");
        }
    }
}
