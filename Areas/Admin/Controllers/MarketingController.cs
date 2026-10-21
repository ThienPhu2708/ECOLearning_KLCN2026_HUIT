using System;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Admin.Controllers
{
    /// <summary>
    /// Admin / Giám sát Marketing: tổng quan khuyến mãi, cảnh báo chương trình bất thường, tạm dừng / cho chạy lại
    /// (Marketing không tự kích hoạt lại được chương trình Admin đã tạm dừng), hoạt động của nhân viên Marketing.
    /// Tạo / khóa tài khoản nhân viên Marketing ở trang Quản lý người dùng.
    /// </summary>
    public class MarketingController : AdminBaseController
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private int AdminId => Convert.ToInt32(Session["UserId"]);

        // GET: Admin/Marketing?loc=running|warning|admin-paused|expiring|ended
        public ActionResult Index(string q = null, string loc = null)
        {
            ViewBag.BreadcrumbCurrent = "Giám sát Marketing";
            var service = new AdminMarketingService(db);
            if (!service.IsInstalled())
            {
                ViewBag.ChuaKhoiTao = true;
                return View();
            }
            ViewBag.Q = q;
            ViewBag.Loc = loc;
            return View(service.TongQuan(q, loc));
        }

        // GET: Admin/Marketing/Details/KM...
        public ActionResult Details(string id)
        {
            var service = new AdminMarketingService(db);
            if (!service.IsInstalled()) return RedirectToAction("Index");
            var model = service.LayMot(id);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy chương trình khuyến mãi.";
                return RedirectToAction("Index");
            }
            ViewBag.NhatKy = new AccountAdminService(db).LayNhatKy(AccountAdminService.DoiTuongKhuyenMai, id);
            // Admin không vào khu Marketing nên xem khóa áp dụng và lịch sử dùng mã ngay tại đây
            var promotion = new PromotionService(db);
            ViewBag.KhoaHoc = promotion.LayKhoaHocApDung(id);
            ViewBag.LuotSuDung = promotion.LayLuotSuDung(id);
            ViewBag.BreadcrumbCurrent = "Giám sát Marketing / " + model.ChuongTrinh.TenKhuyenMai;
            return View(model);
        }

        // POST: Admin/Marketing/Pause
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Pause(string id, string lyDo, string returnUrl = null)
        {
            return KetQua(new AdminMarketingService(db).TamDung(AdminId, id, lyDo), id, returnUrl);
        }

        // POST: Admin/Marketing/Resume
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Resume(string id, string ghiChu, string returnUrl = null)
        {
            return KetQua(new AdminMarketingService(db).ChoChayLai(AdminId, id, ghiChu), id, returnUrl);
        }

        private ActionResult KetQua(KetQuaQuanTri kq, string id, string returnUrl)
        {
            TempData[kq.ThanhCong ? "SuccessMessage" : "ErrorMessage"] = kq.ThongBao;
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction("Details", new { id });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
