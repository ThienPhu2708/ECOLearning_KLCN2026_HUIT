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

        // GET: Coupon/QuangCao — ảnh quảng cáo chiến dịch / chương trình đang chạy cho popup trang chủ (công khai, chỉ đọc)
        [HttpGet]
        public JsonResult QuangCao()
        {
            var ds = new QuangCaoService(db).LayDangChay(CoViMa ? UserId : 0);
            return Json(new
            {
                items = ds.Select(a => new
                {
                    type = a.Loai,
                    id = a.Ma,
                    title = a.Ten,
                    image = Url.Content("~" + a.Anh),
                    url = Url.Action(a.Loai, "UuDai", new { id = a.Ma }),
                    expiresAt = a.NgayKetThuc.ToString("dd/MM/yyyy")
                }).ToList()
            }, JsonRequestBehavior.AllowGet);
        }

        // GET: Coupon/LoyaltyPopup — dữ liệu popup "Ưu đãi dành riêng cho học viên thân thiết" ở trang chủ (chỉ đọc)
        [HttpGet]
        public JsonResult LoyaltyPopup()
        {
            var khong = Json(new { show = false }, JsonRequestBehavior.AllowGet);
            if (!CoViMa) return khong;
            var service = new PromotionService(db);
            if (!service.IsInstalled()) return khong;

            var p = service.LayPopupThanThiet(UserId);
            if (p == null) return khong;
            return Json(new
            {
                show = true,
                kind = p.Loai,
                id = p.MaKhuyenMai,
                code = p.MaCode,
                title = p.TieuDe,
                discountText = p.GiaTriNgan,
                note = p.GhiChuGiaTri,
                description = p.MoTa,
                conditions = p.DieuKien,
                expiresAt = p.HanDung.HasValue ? p.HanDung.Value.ToString("dd/MM/yyyy") : null,
                progress = p.TienDo,
                walletUrl = Url.Action("Index", "Coupon", new { tab = "thuthap" })
            }, JsonRequestBehavior.AllowGet);
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

            // Nút "Lưu mã" trong danh sách: chỉ mã công khai
            var kq = service.LuuMa(UserId, code, tuDanhSach: true);
            return Json(new { success = kq.Success, alreadySaved = kq.DaCoTrongVi, message = kq.Message });
        }

        // POST: Coupon/Redeem — ô "Nhập mã" trong ví: lưu được cả mã riêng Marketing gửi (học viên đã biết mã)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public ActionResult Redeem(string code)
        {
            if (UserId <= 0) return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Index", "Coupon") });
            var service = new PromotionService(db);
            if (!service.IsInstalled()) return View("ChuaKhoiTao");
            if (!CoViMa) return View("KhongHoTro");

            var kq = service.LuuMa(UserId, code, tuDanhSach: false);
            TempData[kq.Success && !kq.DaCoTrongVi ? "CouponSuccess" : "CouponError"] = kq.Message;
            TempData["CouponInput"] = kq.Success ? null : code;
            return RedirectToAction("Index", new { tab = "codung" });
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
