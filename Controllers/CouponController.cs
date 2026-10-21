using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    /// <summary>
    /// Mã giảm giá phía học viên: ví "Mã giảm giá của tôi" (/ma-giam-gia-cua-toi), chi tiết mã kèm chính sách
    /// (/ma-giam-gia/{code}) và lưu mã vào ví. Mã đã lưu không xóa được.
    /// </summary>
    public class CouponController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private int UserId => (Session["UserId"] as int?) ?? 0;

        /// <summary>Học viên và giáo viên (giáo viên vẫn mua khóa của người khác) mới có ví mã.</summary>
        private bool CoViMa
        {
            get
            {
                string role = Session["Role"]?.ToString();
                return UserId > 0 && (role == "HocVien" || role == "GiaoVien");
            }
        }

        private bool GioHangCoKhoaHoc
        {
            get
            {
                var cart = Session["Cart"] as List<CartItemViewModel>;
                return cart != null && cart.Count > 0;
            }
        }

        // GET: /ma-giam-gia-cua-toi?tab=codung|dadung|hethan|thuthap
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public ActionResult Index(string tab)
        {
            if (UserId <= 0) return RedirectToAction("Login", "Account", new { returnUrl = Request.RawUrl });
            var service = new PromotionService(db);
            if (!service.IsInstalled()) return View("ChuaKhoiTao");
            if (!CoViMa) return View("KhongHoTro");

            var vm = service.LayViCuaToi(UserId, tab);
            vm.GioHangCoKhoaHoc = GioHangCoKhoaHoc;
            return View(vm);
        }

        // GET: /ma-giam-gia/{code}
        public ActionResult Details(string code)
        {
            if (UserId <= 0) return RedirectToAction("Login", "Account", new { returnUrl = Request.RawUrl });
            var service = new PromotionService(db);
            if (!service.IsInstalled()) return View("ChuaKhoiTao");
            if (!CoViMa) return View("KhongHoTro");

            var vm = service.LayChiTiet(UserId, code);
            if (vm == null) return HttpNotFound();
            vm.GioHangCoKhoaHoc = GioHangCoKhoaHoc;
            return View(vm);
        }

        // POST: Coupon/Collect — lưu mã vào ví (đã xác nhận ở hộp thoại; không hoàn tác được)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public JsonResult Collect(string code)
        {
            if (UserId <= 0)
            {
                return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để lưu mã giảm giá." });
            }
            if (!CoViMa)
            {
                return Json(new { success = false, message = "Tài khoản của bạn không dùng mã giảm giá." });
            }
            var service = new PromotionService(db);
            if (!service.IsInstalled())
            {
                return Json(new { success = false, message = "Chức năng mã giảm giá chưa sẵn sàng." });
            }

            var kq = service.LuuMa(UserId, code);
            return Json(new { success = kq.Success, alreadySaved = kq.DaCoTrongVi, message = kq.Message });
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
