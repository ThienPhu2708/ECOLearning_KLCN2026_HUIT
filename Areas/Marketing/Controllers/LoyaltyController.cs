using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Services;

namespace ECOLearning.Areas.Marketing.Controllers
{
    /// <summary>
    /// Ưu đãi dành cho học viên thân thiết: chương trình khuyến mãi có đối tượng ThanThiet và tiêu chí
    /// (số khóa đã đăng ký / tổng chi tiêu). Học viên nhận ưu đãi bằng coupon riêng; server kiểm tra tiêu chí khi áp mã.
    /// Tạo / sửa dùng chung form của PromotionController.
    /// </summary>
    public class LoyaltyController : MarketingBaseController
    {
        // GET: Marketing/Loyalty
        public ActionResult Index(string search = null, string status = null)
        {
            var service = new PromotionService(db);
            if (!service.IsInstalled()) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentStatus = string.IsNullOrEmpty(status) ? "all" : status;
            ViewBag.BreadcrumbCurrent = "Ưu đãi học viên thân thiết";
            return View(service.LayDanhSach(search, status, "ThanThiet"));
        }

        // GET: Marketing/Loyalty/Eligible/KM... — học viên đạt / chưa đạt tiêu chí theo dữ liệu hiện có
        public ActionResult Eligible(string id, bool chiDat = true)
        {
            var service = new PromotionService(db);
            if (!service.IsInstalled()) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            var p = service.LayTheoMa(id);
            if (p == null || !p.LaThanThiet) return HttpNotFound();

            var hocVien = service.LayHocVienTheoTieuChi(p);
            ViewBag.SoDat = hocVien.Count(h => PromotionService.DatTieuChi(p, h));
            ViewBag.TongHocVien = hocVien.Count;
            ViewBag.ChiDat = chiDat;
            ViewBag.HocVien = chiDat ? hocVien.Where(h => PromotionService.DatTieuChi(p, h)).ToList() : hocVien;
            ViewBag.BreadcrumbCurrent = "Học viên đủ điều kiện";
            return View(p);
        }
    }
}
