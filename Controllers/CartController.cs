using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    public class CartController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private const string SessionCoupon = "Applied_Coupon";

        // GET: Cart (UC4: Giỏ hàng & Thanh toán)
        [HttpGet]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public ActionResult Index()
        {
            var cart = GetCartSession();
            CapNhatGia(cart);

            decimal subtotal = cart.Sum(i => i.Price);
            decimal discount = 0;
            string couponCode = Session[SessionCoupon] as string;

            // Mã đã áp dụng có thể hết hạn / hết lượt / bị tạm ngừng sau đó: kiểm tra lại mỗi lần mở giỏ
            if (!string.IsNullOrEmpty(couponCode) && cart.Count > 0)
            {
                var kq = KiemTraCoupon(couponCode, cart);
                if (kq.Success)
                {
                    discount = kq.TienGiam;
                    ViewBag.CouponMessage = kq.Message;
                }
                else
                {
                    Session.Remove(SessionCoupon);
                    ViewBag.CouponWarning = "Mã " + couponCode + " không còn áp dụng: " + kq.Message;
                    couponCode = null;
                }
            }

            var model = new CartViewModel
            {
                Items = cart,
                SubTotal = subtotal,
                DiscountAmount = discount,
                AppliedCouponCode = couponCode,
                TotalAmount = Math.Max(0, subtotal - discount),
                AvailableCoupons = LayMaGoiY(cart),
                IsLoggedIn = ((Session["UserId"] as int?) ?? 0) > 0
            };

            return View(model);
        }

        // POST: Cart/AddToCart (Thêm khóa học vào giỏ)
        [HttpPost]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public JsonResult AddToCart(string courseId)
        {
            var cart = GetCartSession();
            try
            {
                string message;
                bool ok = ThemKhoaVaoGio(courseId, cart, out message);
                if (ok) SaveCartSession(cart);
                return Json(new { success = ok, message, cartCount = cart.Count });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AddToCart Error: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi xảy ra khi thêm vào giỏ hàng!" });
            }
        }

        // POST: Cart/AddManyToCart — nút "Thêm tất cả vào giỏ hàng" của mục "Thường xuyên được mua cùng nhau".
        // Kiểm tra từng khóa như AddToCart; khóa đã có trong giỏ tính là thành công (không thêm lần hai).
        [HttpPost]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public JsonResult AddManyToCart(string[] courseIds)
        {
            var ids = (courseIds ?? new string[0]).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Take(10).ToList();
            if (ids.Count == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn khóa học!" });
            }

            var cart = GetCartSession();
            var daThem = new List<string>();
            var trongGio = new List<string>();
            var loi = new List<object>();
            try
            {
                foreach (var id in ids)
                {
                    if (cart.Any(i => i.CourseId == id)) { trongGio.Add(id); continue; }
                    string message;
                    if (ThemKhoaVaoGio(id, cart, out message)) daThem.Add(id);
                    else loi.Add(new { courseId = id, message });
                }
                SaveCartSession(cart);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AddManyToCart Error: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi xảy ra khi thêm vào giỏ hàng!", cartCount = cart.Count });
            }

            int coTrongGio = daThem.Count + trongGio.Count;
            return Json(new
            {
                success = coTrongGio > 0,
                message = loi.Count == 0
                    ? "Giỏ hàng đã có đủ " + coTrongGio + " khóa học."
                    : coTrongGio + "/" + ids.Count + " khóa học đã có trong giỏ; " + loi.Count + " khóa không thêm được.",
                added = daThem,
                inCart = daThem.Concat(trongGio).ToList(),
                errors = loi,
                cartCount = cart.Count
            });
        }

        /// <summary>
        /// Thêm một khóa vào giỏ (session) sau khi kiểm tra: đang bán, chưa có trong giỏ, không phải khóa mình dạy,
        /// chưa đăng ký. Giá lấy từ CSDL (PromotionService.GiaBanHienTai) và còn được tính lại khi mở giỏ / thanh toán.
        /// </summary>
        private bool ThemKhoaVaoGio(string courseId, List<CartItemViewModel> cart, out string message)
        {
            if (string.IsNullOrWhiteSpace(courseId))
            {
                message = "Vui lòng chọn khóa học!";
                return false;
            }
            if (cart.Any(i => i.CourseId == courseId))
            {
                message = "Khóa học này đã có trong giỏ hàng của bạn!";
                return false;
            }

            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == courseId && (k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active"));
            if (course == null)
            {
                message = "Không tìm thấy khóa học hợp lệ hoặc khóa học chưa được phê duyệt!";
                return false;
            }

            int userId = (Session["UserId"] as int?) ?? 0;
            if (userId > 0 && course.maGiaoVien == userId)
            {
                message = "Bạn là giáo viên của khóa học này nên không thể tự mua!";
                return false;
            }
            if (userId > 0 && db.DangKyKhoaHoc.Any(d => d.maHocVien == userId && d.maKhoaHoc == courseId))
            {
                message = "Bạn đã sở hữu khóa học này.";
                return false;
            }

            cart.Add(new CartItemViewModel
            {
                CourseId = course.maKhoaHoc,
                CourseName = course.tenKhoaHoc,
                InstructorName = course.NguoiDung != null ? course.NguoiDung.hoTen : "Giảng viên E-Learning",
                DurationText = "30 giờ học",
                Price = PromotionService.GiaBanHienTai(course),
                OriginalPrice = course.giaBan,
                ThumbnailUrl = string.IsNullOrEmpty(course.anhBia) ? "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80" : course.anhBia
            });
            message = "Đã thêm khóa học vào giỏ hàng thành công!";
            return true;
        }

        // POST: Cart/RemoveFromCart (Xóa khóa học khỏi giỏ)
        [HttpPost]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public JsonResult RemoveFromCart(string courseId)
        {
            var cart = GetCartSession();
            var item = cart.FirstOrDefault(i => i.CourseId == courseId);
            if (item != null)
            {
                cart.Remove(item);
                SaveCartSession(cart);
            }
            CapNhatGia(cart);

            decimal subtotal = cart.Sum(i => i.Price);
            decimal discount = 0;
            string couponMessage = null;
            string couponCode = Session[SessionCoupon] as string;
            if (!string.IsNullOrEmpty(couponCode))
            {
                var kq = cart.Count > 0 ? KiemTraCoupon(couponCode, cart) : KetQuaApDungUuDai.Loi("Giỏ hàng đang trống.");
                if (kq.Success)
                {
                    discount = kq.TienGiam;
                }
                else
                {
                    // Bỏ khóa khỏi giỏ có thể làm mã không còn đủ điều kiện (phạm vi khóa học, đơn tối thiểu)
                    Session.Remove(SessionCoupon);
                    couponMessage = "Mã " + couponCode + " không còn áp dụng: " + kq.Message;
                }
            }
            decimal total = Math.Max(0, subtotal - discount);

            return Json(new
            {
                success = true,
                message = "Đã xóa khóa học khỏi giỏ hàng!",
                cartCount = cart.Count,
                subtotal = subtotal.ToString("N0") + " đ",
                discount = (discount > 0 ? "- " + discount.ToString("N0") + " đ" : "- 0 đ"),
                total = total.ToString("N0") + " đ",
                rawSubtotal = subtotal,
                rawTotal = total,
                couponRemoved = couponMessage != null,
                couponMessage
            });
        }

        // POST: Cart/ApplyCoupon — kiểm tra & tính giảm giá hoàn toàn ở server (PromotionService)
        [HttpPost]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public JsonResult ApplyCoupon(string couponCode)
        {
            if (string.IsNullOrWhiteSpace(couponCode))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã khuyến mãi!" });
            }

            var cart = GetCartSession();
            CapNhatGia(cart);
            var kq = KiemTraCoupon(couponCode, cart);
            if (!kq.Success)
            {
                return Json(new { success = false, message = kq.Message });
            }

            decimal subtotal = cart.Sum(i => i.Price);
            decimal total = Math.Max(0, subtotal - kq.TienGiam);
            Session[SessionCoupon] = kq.MaCode;

            return Json(new
            {
                success = true,
                message = kq.Message,
                couponCode = kq.MaCode,
                discount = "- " + kq.TienGiam.ToString("N0") + " đ",
                total = total.ToString("N0") + " đ"
            });
        }

        // POST: Cart/Checkout (Xác nhận thanh toán đơn hàng & Lưu vào CSDL)
        [HttpPost]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public JsonResult Checkout(string paymentMethod, string couponCode)
        {
            var cart = GetCartSession();
            if (cart.Count == 0)
            {
                return Json(new { success = false, message = "Giỏ hàng của bạn đang rỗng!" });
            }

            int userId = (Session["UserId"] as int?) ?? 0;
            if (userId == 0)
            {
                return Json(new { success = false, requireLogin = true, message = "Vui lòng đăng nhập để hoàn tất thanh toán!" });
            }

            var user = db.NguoiDung.FirstOrDefault(u => u.maNguoiDung == userId);
            if (user == null)
            {
                return Json(new { success = false, requireLogin = true, message = "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại!" });
            }

            // Giỏ hàng có thể được thêm trước khi đăng nhập, nên kiểm tra lại khóa học do chính người mua tạo
            var cartCourseIds = cart.Select(i => i.CourseId).ToList();
            var ownCourseNames = db.KhoaHoc
                .Where(k => cartCourseIds.Contains(k.maKhoaHoc) && k.maGiaoVien == userId)
                .Select(k => k.tenKhoaHoc)
                .ToList();
            if (ownCourseNames.Any())
            {
                return Json(new { success = false, message = "Bạn không thể tự mua khóa học của chính mình: " + string.Join(", ", ownCourseNames) + ". Vui lòng xóa khỏi giỏ hàng!" });
            }

            // Khóa học có thể bị tạm khóa / ngừng bán sau khi đã thêm vào giỏ
            var unavailableNames = db.KhoaHoc
                .Where(k => cartCourseIds.Contains(k.maKhoaHoc))
                .Select(k => new { k.tenKhoaHoc, k.trangThai })
                .ToList()
                .Where(k => !CourseStatus.IsPublished(k.trangThai))
                .Select(k => k.tenKhoaHoc)
                .ToList();
            if (unavailableNames.Any())
            {
                return Json(new { success = false, message = "Các khóa học sau hiện không còn mở bán: " + string.Join(", ", unavailableNames) + ". Vui lòng xóa khỏi giỏ hàng!" });
            }

            var alreadyOwned = db.DangKyKhoaHoc
                .Where(d => d.maHocVien == userId && cartCourseIds.Contains(d.maKhoaHoc))
                .Select(d => d.KhoaHoc.tenKhoaHoc)
                .ToList();
            if (alreadyOwned.Any())
            {
                return Json(new { success = false, message = "Bạn đã sở hữu các khóa học: " + string.Join(", ", alreadyOwned) + ". Vui lòng xóa khỏi giỏ hàng!" });
            }

            // Giá luôn lấy lại từ CSDL ở server, không dùng giá cũ trong phiên làm việc
            CapNhatGia(cart);
            decimal subtotal = cart.Sum(i => i.Price);
            string appliedCoupon = Session[SessionCoupon] as string;
            if (string.IsNullOrWhiteSpace(appliedCoupon)) appliedCoupon = couponCode;
            appliedCoupon = string.IsNullOrWhiteSpace(appliedCoupon) ? null : appliedCoupon.Trim().ToUpperInvariant();

            var payment = new PaymentService(db);
            if (!payment.IsInstalled())
            {
                return Json(new { success = false, message = "Chức năng thanh toán chưa được khởi tạo (chạy Database/Migrations/014_ThanhToan_ThuNghiem.sql)." });
            }
            string phuongThuc = PaymentService.PhuongThucHopLe.Contains(paymentMethod) ? paymentMethod : PaymentService.PhuongThucHopLe[0];
            if (VnPayService.DangBat && !VnPayService.HoTroPhuongThuc(phuongThuc))
            {
                return Json(new { success = false, unsupportedMethod = true, message = VnPayService.ThongBaoChuaHoTro });
            }

            try
            {
                var promotions = new PromotionService(db);
                string orderId = "DH" + DateTime.Now.ToString("yyMMddHHmmss") + new Random().Next(1000, 9999);
                decimal total;
                // Mỗi học viên chỉ giữ một đơn chờ; đơn quá hạn chuyển "Hết hạn"
                payment.XuLyDonHetHan();
                payment.HuyDonChoCu(userId);
                KetQuaApDungUuDai uuDai = null;

                using (var tx = db.Database.BeginTransaction())
                {
                    if (appliedCoupon != null)
                    {
                        // Khóa theo mã coupon trong transaction: hai đơn cùng lúc không vượt giới hạn lượt dùng
                        db.Database.ExecuteSqlCommand("EXEC sp_getapplock @Resource = @r, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000",
                            new SqlParameter("@r", "Coupon_" + appliedCoupon));
                        uuDai = promotions.KiemTraCoupon(userId, appliedCoupon, ToLines(cart));
                        if (!uuDai.Success)
                        {
                            tx.Rollback();
                            Session.Remove(SessionCoupon);
                            return Json(new { success = false, couponInvalid = true, message = "Mã " + appliedCoupon + " không áp dụng được: " + uuDai.Message });
                        }
                    }

                    decimal discount = uuDai != null ? uuDai.TienGiam : 0;
                    total = Math.Max(0, subtotal - discount);

                    db.DonHang.Add(new DonHang
                    {
                        maDonHang = orderId,
                        ngayDat = DateTime.Now,
                        tongTien = total,
                        // Đơn chờ thanh toán: chỉ ghi danh khi cổng báo thanh toán thành công (PaymentService.XuLyKetQua)
                        trangThai = PaymentService.ChoThanhToan,
                        maHocVien = userId,
                        maGiamGia = uuDai != null ? uuDai.MaGiamGia : null
                    });

                    foreach (var item in cart)
                    {
                        // Đơn giá lưu giá bán của khóa (trước coupon); tiền giảm của coupon lưu riêng ở SuDungUuDai
                        db.ChiTietDonHang.Add(new ChiTietDonHang
                        {
                            maChiTiet = "CT" + Guid.NewGuid().ToString("N").Substring(0, 18),
                            maDonHang = orderId,
                            maKhoaHoc = item.CourseId,
                            donGia = item.Price
                        });

                    }

                    db.SaveChanges();

                    if (uuDai != null)
                    {
                        // Lượt dùng gắn với đơn; giới hạn lượt chỉ tính đơn "Đã thanh toán" nên đơn chờ / hết hạn không chiếm lượt
                        promotions.GhiNhanSuDung(orderId, userId, uuDai);
                    }
                    payment.DatThongTinDonCho(orderId, total > 0 ? phuongThuc : "Miễn phí");
                    // Trigger chi tiết đơn hàng tính lại tổng tiền; đặt lại số tiền thực trả để chắc chắn khớp
                    db.Database.ExecuteSqlCommand("UPDATE dbo.DonHang SET tongTien = @t WHERE maDonHang = @dh",
                        new SqlParameter("@t", total), new SqlParameter("@dh", orderId));

                    tx.Commit();
                }

                payment.GhiNhatKy(orderId, "TAO_DON", null, null, null,
                    "Tạo đơn " + total.ToString("#,##0") + "đ, phương thức " + (total > 0 ? phuongThuc : "Miễn phí"));

                if (total <= 0)
                {
                    // Đơn 0đ: không qua cổng, ghi danh ngay
                    payment.XacNhanDonMienPhi(orderId);
                    Session.Remove("Cart");
                    Session.Remove(SessionCoupon);
                    return Json(new
                    {
                        success = true,
                        message = "Đơn hàng 0đ đã được xác nhận. Khóa học đã được thêm vào hồ sơ học tập của bạn.",
                        orderId,
                        redirectUrl = Url.Action("Result", "Payment", new { orderId })
                    });
                }

                // Giỏ hàng giữ nguyên tới khi cổng báo thanh toán thành công (xóa ở PaymentController.Return)
                return Json(new
                {
                    success = true,
                    requirePayment = true,
                    message = "Đã tạo đơn hàng, đang chuyển sang cổng thanh toán...",
                    orderId,
                    redirectUrl = Url.Action("Gateway", "Payment", new { orderId })
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Checkout Error: " + ex);
                // KHÔNG xóa giỏ hàng và KHÔNG báo thành công khi lưu đơn hàng thất bại thật sự
                return Json(new { success = false, message = "Có lỗi xảy ra khi xử lý đơn hàng. Vui lòng thử lại hoặc liên hệ hỗ trợ!" });
            }
        }

        // GET: Cart/GetCartCount (AJAX lấy số lượng giỏ hàng cho Header Badge)
        [HttpGet]
        public JsonResult GetCartCount()
        {
            var cart = GetCartSession();
            return Json(new { count = cart.Count }, JsonRequestBehavior.AllowGet);
        }

        #region Helper Methods
        private List<CartItemViewModel> GetCartSession()
        {
            var cart = Session["Cart"] as List<CartItemViewModel>;
            if (cart == null)
            {
                cart = new List<CartItemViewModel>();
                Session["Cart"] = cart;
            }
            return cart;
        }

        private void SaveCartSession(List<CartItemViewModel> cart)
        {
            Session["Cart"] = cart;
        }

        /// <summary>Lấy lại giá bán hiện tại từ CSDL (Marketing có thể đổi giá giảm sau khi khóa đã nằm trong giỏ).</summary>
        private void CapNhatGia(List<CartItemViewModel> cart)
        {
            if (cart.Count == 0) return;
            var ids = cart.Select(i => i.CourseId).ToList();
            var courses = db.KhoaHoc.Where(k => ids.Contains(k.maKhoaHoc)).ToDictionary(k => k.maKhoaHoc);
            foreach (var item in cart)
            {
                KhoaHoc k;
                if (courses.TryGetValue(item.CourseId, out k))
                {
                    item.Price = PromotionService.GiaBanHienTai(k);
                    item.OriginalPrice = k.giaBan;
                }
            }
        }

        private static List<DongGioHang> ToLines(IEnumerable<CartItemViewModel> cart)
        {
            return cart.Select(i => new DongGioHang { MaKhoaHoc = i.CourseId, DonGia = i.Price }).ToList();
        }

        private KetQuaApDungUuDai KiemTraCoupon(string couponCode, List<CartItemViewModel> cart)
        {
            var promotions = new PromotionService(db);
            if (!promotions.IsInstalled())
            {
                return KetQuaApDungUuDai.Loi("Chức năng mã giảm giá chưa sẵn sàng.");
            }
            int userId = (Session["UserId"] as int?) ?? 0;
            return promotions.KiemTraCoupon(userId, couponCode, ToLines(cart));
        }

        /// <summary>
        /// Mã trong ví "Mã giảm giá của tôi" cho giỏ hiện tại (kèm số tiền giảm / lý do chưa dùng được).
        /// Mã chưa lưu không hiện ở giỏ; học viên thu thập ở trang chủ / trang Mã giảm giá của tôi, hoặc nhập tay mã kín.
        /// </summary>
        private List<MaGiamGiaHienThi> LayMaGoiY(List<CartItemViewModel> cart)
        {
            var promotions = new PromotionService(db);
            if (!promotions.IsInstalled()) return new List<MaGiamGiaHienThi>();
            int userId = (Session["UserId"] as int?) ?? 0;
            return promotions.LayMaChoHocVien(userId, cart.Count > 0 ? ToLines(cart) : null, null, chiTrongVi: true);
        }

        // GET: Cart/AvailableCoupons — danh sách mã trong hộp thoại "Chọn mã khuyến mãi" (tải lại mỗi lần mở / sau khi giỏ đổi)
        [HttpGet]
        public ActionResult AvailableCoupons()
        {
            var cart = GetCartSession();
            CapNhatGia(cart);
            return PartialView("_CouponList", new CartViewModel
            {
                Items = cart,
                AppliedCouponCode = Session[SessionCoupon] as string,
                AvailableCoupons = LayMaGoiY(cart),
                IsLoggedIn = ((Session["UserId"] as int?) ?? 0) > 0
            });
        }

        // POST: Cart/RemoveCoupon — bỏ mã đang áp dụng (mã vẫn nằm trong ví, chưa tính lượt dùng)
        [HttpPost]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public JsonResult RemoveCoupon()
        {
            Session.Remove(SessionCoupon);
            var cart = GetCartSession();
            CapNhatGia(cart);
            decimal subtotal = cart.Sum(i => i.Price);
            return Json(new { success = true, discount = "- 0 đ", total = subtotal.ToString("N0") + " đ" });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && db != null)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
        #endregion
    }
}
