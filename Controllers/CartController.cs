using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    public class CartController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Cart (UC4: Giỏ hàng & Thanh toán)
        [HttpGet]
        public ActionResult Index()
        {
            var cart = GetCartSession();

            decimal subtotal = cart.Sum(i => i.Price);
            string couponCode = Session["Applied_Coupon"] as string;
            decimal discount = CalculateDiscount(subtotal, couponCode);
            decimal total = Math.Max(0, subtotal - discount);

            var model = new CartViewModel
            {
                Items = cart,
                SubTotal = subtotal,
                DiscountAmount = discount,
                AppliedCouponCode = couponCode,
                TotalAmount = total
            };

            return View(model);
        }

        // POST: Cart/AddToCart (Thêm khóa học vào giỏ)
        [HttpPost]
        public JsonResult AddToCart(string courseId)
        {
            if (string.IsNullOrWhiteSpace(courseId))
            {
                return Json(new { success = false, message = "Vui lòng chọn khóa học!" });
            }

            var cart = GetCartSession();

            if (cart.Any(i => i.CourseId == courseId))
            {
                return Json(new { success = false, message = "Khóa học này đã có trong giỏ hàng của bạn!", cartCount = cart.Count });
            }

            try
            {
                var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == courseId && (k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active"));
                if (course == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy khóa học hợp lệ hoặc khóa học chưa được phê duyệt!" });
                }

                cart.Add(new CartItemViewModel
                {
                    CourseId = course.maKhoaHoc,
                    CourseName = course.tenKhoaHoc,
                    InstructorName = course.NguoiDung != null ? course.NguoiDung.hoTen : "Giảng viên E-Learning",
                    DurationText = "30 giờ học",
                    Price = course.giaKhuyenMai.HasValue && course.giaKhuyenMai.Value > 0 ? course.giaKhuyenMai.Value : course.giaBan,
                    OriginalPrice = course.giaBan,
                    ThumbnailUrl = string.IsNullOrEmpty(course.anhBia) ? "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80" : course.anhBia
                });

                SaveCartSession(cart);
                return Json(new { success = true, message = "Đã thêm khóa học vào giỏ hàng thành công!", cartCount = cart.Count });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("AddToCart Error: " + ex.Message);
                return Json(new { success = false, message = "Có lỗi xảy ra khi thêm vào giỏ hàng!" });
            }
        }

        // POST: Cart/RemoveFromCart (Xóa khóa học khỏi giỏ)
        [HttpPost]
        public JsonResult RemoveFromCart(string courseId)
        {
            var cart = GetCartSession();
            var item = cart.FirstOrDefault(i => i.CourseId == courseId);
            if (item != null)
            {
                cart.Remove(item);
                SaveCartSession(cart);
            }

            decimal subtotal = cart.Sum(i => i.Price);
            string couponCode = Session["Applied_Coupon"] as string;
            decimal discount = CalculateDiscount(subtotal, couponCode);
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
                rawTotal = total
            });
        }

        // POST: Cart/ApplyCoupon (Kiểm tra & áp dụng mã giảm giá từ CSDL MaGiamGia)
        [HttpPost]
        public JsonResult ApplyCoupon(string couponCode)
        {
            if (string.IsNullOrWhiteSpace(couponCode))
            {
                return Json(new { success = false, message = "Vui lòng nhập mã khuyến mãi!" });
            }

            string code = couponCode.Trim().ToUpper();
            var cart = GetCartSession();
            decimal subtotal = cart.Sum(i => i.Price);

            // Kiểm tra mã giảm giá trong CSDL
            var mgg = db.MaGiamGia.FirstOrDefault(m => m.maCode.ToUpper() == code && (m.trangThai == true || m.trangThai == null));
            if (mgg == null)
            {
                return Json(new { success = false, message = "Mã khuyến mãi không hợp lệ hoặc không tồn tại!" });
            }

            // Kiểm tra thời hạn khuyến mãi nếu thuộc về chương trình KhuyenMai
            if (mgg.KhuyenMai != null)
            {
                var now = DateTime.Now;
                if (mgg.KhuyenMai.ngayBatDau > now)
                {
                    return Json(new { success = false, message = "Chương trình khuyến mãi chưa bắt đầu!" });
                }
                if (mgg.KhuyenMai.ngayKetThuc < now)
                {
                    return Json(new { success = false, message = "Mã khuyến mãi này đã hết hạn!" });
                }
            }

            decimal discount = CalculateDiscountAmount(subtotal, mgg);
            decimal total = Math.Max(0, subtotal - discount);

            Session["Applied_Coupon"] = mgg.maCode;

            string messageText = "Đã áp dụng mã ưu đãi thành công!";
            if (mgg.KhuyenMai != null && mgg.KhuyenMai.mucUuDai > 0)
            {
                messageText = string.Format("Đã áp dụng mã ưu đãi giảm {0}%!", mgg.KhuyenMai.mucUuDai);
            }
            else if (discount > 0)
            {
                messageText = string.Format("Đã áp dụng mã ưu đãi giảm {0:N0} đ!", discount);
            }

            return Json(new
            {
                success = true,
                message = messageText,
                couponCode = mgg.maCode,
                discount = "- " + discount.ToString("N0") + " đ",
                total = total.ToString("N0") + " đ"
            });
        }

        // POST: Cart/Checkout (Xác nhận thanh toán đơn hàng & Lưu vào CSDL)
        [HttpPost]
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

            try
            {
                string appliedCoupon = Session["Applied_Coupon"] as string ?? couponCode;

                decimal subtotal = cart.Sum(i => i.Price);
                decimal discount = CalculateDiscount(subtotal, appliedCoupon);
                decimal total = Math.Max(0, subtotal - discount);

                var mgg = string.IsNullOrEmpty(appliedCoupon) ? null : db.MaGiamGia.FirstOrDefault(m => m.maCode.ToUpper() == appliedCoupon.Trim().ToUpper());

                string orderId = "DH_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + new Random().Next(100, 999);

                DonHang donHang = new DonHang
                {
                    maDonHang = orderId,
                    ngayDat = DateTime.Now,
                    tongTien = total,
                    trangThai = "Đã thanh toán",
                    maHocVien = userId,
                    maGiamGia = mgg != null ? mgg.maGiamGia1 : null
                };
                db.DonHang.Add(donHang);

                int ctIndex = 1;
                foreach (var item in cart)
                {
                    ChiTietDonHang ct = new ChiTietDonHang
                    {
                        maChiTiet = "CT_" + orderId + "_" + ctIndex++,
                        maDonHang = orderId,
                        maKhoaHoc = item.CourseId,
                        donGia = item.Price
                    };
                    db.ChiTietDonHang.Add(ct);

                    // Kiểm tra và tự động thêm vào Đăng ký khóa học
                    var existingDK = db.DangKyKhoaHoc.FirstOrDefault(d => d.maHocVien == userId && d.maKhoaHoc == item.CourseId);
                    if (existingDK == null)
                    {
                        DangKyKhoaHoc dk = new DangKyKhoaHoc
                        {
                            maDangKy = "DK_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + ctIndex,
                            ngayDangKy = DateTime.Now,
                            trangThaiDK = "Đã thanh toán",
                            maHocVien = userId,
                            maKhoaHoc = item.CourseId
                        };
                        db.DangKyKhoaHoc.Add(dk);
                    }
                }

                db.SaveChanges();

                // Chỉ xóa giỏ hàng và mã giảm giá SAU KHI đã lưu đơn hàng thành công vào CSDL
                Session.Remove("Cart");
                Session.Remove("Applied_Coupon");

                return Json(new
                {
                    success = true,
                    message = "Thanh toán Thành công! Hệ thống đã tự động ghi nhận đơn hàng và kích hoạt khóa học vào hồ sơ học tập của bạn.",
                    redirectUrl = Url.Action("Index", "Home")
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Checkout Error: " + ex.Message);
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

        private decimal CalculateDiscount(decimal subtotal, string couponCode)
        {
            if (string.IsNullOrWhiteSpace(couponCode) || subtotal <= 0) return 0;

            string code = couponCode.Trim().ToUpper();
            var mgg = db.MaGiamGia.FirstOrDefault(m => m.maCode.ToUpper() == code && (m.trangThai == true || m.trangThai == null));
            if (mgg == null) return 0;

            if (mgg.KhuyenMai != null)
            {
                var now = DateTime.Now;
                if (now < mgg.KhuyenMai.ngayBatDau || now > mgg.KhuyenMai.ngayKetThuc) return 0;
            }

            return CalculateDiscountAmount(subtotal, mgg);
        }

        private decimal CalculateDiscountAmount(decimal subtotal, MaGiamGia mgg)
        {
            if (mgg == null || subtotal <= 0) return 0;

            if (mgg.KhuyenMai != null && mgg.KhuyenMai.mucUuDai > 0)
            {
                double rate = mgg.KhuyenMai.mucUuDai;
                if (rate <= 1.0)
                {
                    return subtotal * (decimal)rate;
                }
                else if (rate <= 100.0)
                {
                    return subtotal * ((decimal)rate / 100m);
                }
            }

            if (mgg.giaTriGiam > 0)
            {
                if (mgg.giaTriGiam <= 1.0m)
                {
                    return subtotal * mgg.giaTriGiam;
                }
                else if (mgg.giaTriGiam < 100.0m)
                {
                    return subtotal * (mgg.giaTriGiam / 100m);
                }
                else
                {
                    return Math.Min(subtotal, mgg.giaTriGiam);
                }
            }

            return 0;
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
