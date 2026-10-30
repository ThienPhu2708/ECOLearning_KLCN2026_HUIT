using System;
using System.Globalization;
using System.Web.Mvc;
using ECOLearning.Services;

namespace ECOLearning.Areas.Marketing.Controllers
{
    /// <summary>Thống kê tương tác của học viên với khóa học (lượt xem, nhấp, yêu thích), chỉ đọc dữ liệu.</summary>
    public class CourseEngagementController : MarketingBaseController
    {
        // GET: Marketing/CourseEngagement
        public ActionResult Index(string tuNgay = null, string denNgay = null, string q = null, string danhMuc = null, string uuDai = null, string sapXep = null, int trang = 1)
        {
            if (!new PromotionService(db).IsInstalled()) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");

            var den = Parse(denNgay) ?? DateTime.Today;
            var tu = Parse(tuNgay) ?? den.AddDays(-29);
            ViewBag.BreadcrumbCurrent = "Tương tác khóa học";
            var dichVu = new CourseEngagementService(db);
            var model = dichVu.LapBaoCao(tu, den);
            dichVu.ApDungBoLoc(model, q, danhMuc, uuDai, sapXep, trang);
            return View(model);
        }

        private static DateTime? Parse(string s)
        {
            DateTime d;
            return DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d : (DateTime?)null;
        }
    }
}
