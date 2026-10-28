using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Báo giá và tạo đơn hàng cho API ứng dụng di động (giỏ hàng nằm trên máy, app gửi danh sách mã khóa học).
    /// Cùng các bước với CartController.Checkout của website: kiểm tra khóa (của chính mình, ngừng bán, đã sở hữu),
    /// giá lấy lại từ CSDL, khóa mã giảm giá trong transaction, mỗi học viên một đơn chờ, đơn 0đ ghi danh ngay.
    /// </summary>
    public class OrderService
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public OrderService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public class DongBaoGia
        {
            public string MaKhoaHoc { get; set; }
            public string TenKhoaHoc { get; set; }
            public string AnhBia { get; set; }

            /// <summary>Giá thanh toán của khóa (đã trừ giảm trực tiếp, chưa trừ coupon).</summary>
            public decimal Gia { get; set; }

            /// <summary>Giá gốc trong CSDL (KhoaHoc.giaBan).</summary>
            public decimal GiaGoc { get; set; }

            /// <summary>Chương trình giảm trực tiếp đã áp dụng cho khóa; null = không giảm.</summary>
            public string MaKhuyenMaiTrucTiep { get; set; }
        }

        public class KhoaKhongHopLe
        {
            public string MaKhoaHoc { get; set; }
            public string LyDo { get; set; }
        }

        public class BaoGia
        {
            public List<DongBaoGia> Dong { get; set; } = new List<DongBaoGia>();
            public List<KhoaKhongHopLe> KhongHopLe { get; set; } = new List<KhoaKhongHopLe>();

            /// <summary>Tạm tính theo giá đã giảm trực tiếp; coupon và đơn tối thiểu của coupon tính trên số này.</summary>
            public decimal TamTinh => Dong.Sum(d => d.Gia);
            public decimal TienGiamTrucTiep => Dong.Sum(d => d.GiaGoc - d.Gia);
            public KetQuaApDungUuDai UuDai { get; set; }
            public decimal TienGiam => UuDai != null && UuDai.Success ? UuDai.TienGiam : 0;
            public decimal TongTien => Math.Max(0, TamTinh - TienGiam);

            public List<DongGioHang> DongGioHang() => Dong.Select(d => new DongGioHang { MaKhoaHoc = d.MaKhoaHoc, DonGia = d.Gia }).ToList();
        }

        /// <summary>
        /// Giá mới nhất của các khóa trong giỏ; khóa không mua được (không tồn tại, ngừng bán, của chính mình, đã sở hữu)
        /// tách sang KhongHopLe kèm lý do. Có mã giảm giá thì kiểm tra và tính số tiền giảm.
        /// </summary>
        public BaoGia TinhBaoGia(int userId, IEnumerable<string> courseIds, string couponCode)
        {
            var ids = (courseIds ?? Enumerable.Empty<string>()).Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).Distinct().ToList();
            var kq = new BaoGia();
            var khoa = db.KhoaHoc.Where(k => ids.Contains(k.maKhoaHoc)).ToList().ToDictionary(k => k.maKhoaHoc);
            var daSoHuu = new HashSet<string>(db.DangKyKhoaHoc.Where(d => d.maHocVien == userId && ids.Contains(d.maKhoaHoc)).Select(d => d.maKhoaHoc));

            foreach (var id in ids)
            {
                KhoaHoc k;
                if (!khoa.TryGetValue(id, out k)) kq.KhongHopLe.Add(new KhoaKhongHopLe { MaKhoaHoc = id, LyDo = "Không tìm thấy khóa học" });
                else if (!CourseStatus.IsPublished(k.trangThai)) kq.KhongHopLe.Add(new KhoaKhongHopLe { MaKhoaHoc = id, LyDo = "Khóa học hiện không còn mở bán" });
                else if (k.maGiaoVien == userId) kq.KhongHopLe.Add(new KhoaKhongHopLe { MaKhoaHoc = id, LyDo = "Bạn không thể tự mua khóa học của chính mình" });
                else if (daSoHuu.Contains(id)) kq.KhongHopLe.Add(new KhoaKhongHopLe { MaKhoaHoc = id, LyDo = "Bạn đã sở hữu khóa học này" });
                else
                {
                    var gia = CourseDiscountService.Tinh(k);
                    kq.Dong.Add(new DongBaoGia
                    {
                        MaKhoaHoc = k.maKhoaHoc,
                        TenKhoaHoc = k.tenKhoaHoc,
                        AnhBia = k.anhBia,
                        Gia = gia.Gia,
                        GiaGoc = gia.GiaGoc,
                        MaKhuyenMaiTrucTiep = gia.MaKhuyenMai
                    });
                }
            }

            string ma = string.IsNullOrWhiteSpace(couponCode) ? null : couponCode.Trim().ToUpperInvariant();
            if (ma != null)
            {
                var promotions = new PromotionService(db);
                kq.UuDai = !promotions.IsInstalled()
                    ? KetQuaApDungUuDai.Loi("Chức năng mã giảm giá chưa sẵn sàng.")
                    : kq.Dong.Count == 0 ? KetQuaApDungUuDai.Loi("Giỏ hàng đang trống.")
                    : promotions.KiemTraCoupon(userId, ma, kq.DongGioHang());
                if (kq.UuDai.MaCode == null) kq.UuDai.MaCode = ma;
            }
            return kq;
        }

        public class KetQuaTaoDon
        {
            public bool Success { get; set; }
            public int Status { get; set; } = 200;
            public string Code { get; set; }
            public string Message { get; set; }
            public string MaDonHang { get; set; }
            public decimal TongTien { get; set; }
            public bool CanThanhToan { get; set; }

            public static KetQuaTaoDon Loi(int status, string code, string message)
                => new KetQuaTaoDon { Success = false, Status = status, Code = code, Message = message };
        }

        /// <summary>Tạo đơn "Chờ thanh toán" (hoặc ghi danh ngay nếu tổng 0đ).</summary>
        public KetQuaTaoDon TaoDon(int userId, IEnumerable<string> courseIds, string couponCode, string phuongThuc)
        {
            var payment = new PaymentService(db);
            if (!payment.IsInstalled())
                return KetQuaTaoDon.Loi(500, "PAYMENT_NOT_INSTALLED", "Chức năng thanh toán chưa được khởi tạo (chạy Database/Migrations/014_ThanhToan_ThuNghiem.sql).");

            var bg = TinhBaoGia(userId, courseIds, null);
            if (bg.Dong.Count == 0 && bg.KhongHopLe.Count == 0)
                return KetQuaTaoDon.Loi(400, "CART_EMPTY", "Giỏ hàng của bạn đang rỗng!");
            var soHuu = bg.KhongHopLe.FirstOrDefault(k => k.LyDo == "Bạn đã sở hữu khóa học này");
            if (soHuu != null)
                return KetQuaTaoDon.Loi(409, "COURSE_OWNED", "Bạn đã sở hữu khóa học " + soHuu.MaKhoaHoc + ". Vui lòng xóa khỏi giỏ hàng!");
            if (bg.KhongHopLe.Count > 0)
                return KetQuaTaoDon.Loi(400, "COURSE_UNAVAILABLE", bg.KhongHopLe[0].LyDo + " (" + bg.KhongHopLe[0].MaKhoaHoc + "). Vui lòng xóa khỏi giỏ hàng!");

            if (!PaymentService.PhuongThucHopLe.Contains(phuongThuc) || (VnPayService.DangBat && !VnPayService.HoTroPhuongThuc(phuongThuc)))
                return KetQuaTaoDon.Loi(400, "METHOD_NOT_SUPPORTED", VnPayService.ThongBaoChuaHoTro);

            string ma = string.IsNullOrWhiteSpace(couponCode) ? null : couponCode.Trim().ToUpperInvariant();
            var promotions = new PromotionService(db);
            string orderId = "DH" + DateTime.Now.ToString("yyMMddHHmmss") + new Random().Next(1000, 9999);
            decimal total;

            // Mỗi học viên chỉ giữ một đơn chờ; đơn quá hạn chuyển "Hết hạn"
            payment.XuLyDonHetHan();
            payment.HuyDonChoCu(userId);
            KetQuaApDungUuDai uuDai = null;

            using (var tx = db.Database.BeginTransaction())
            {
                if (ma != null)
                {
                    // Khóa theo mã coupon trong transaction: hai đơn cùng lúc không vượt giới hạn lượt dùng
                    db.Database.ExecuteSqlCommand("EXEC sp_getapplock @Resource = @r, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000",
                        new SqlParameter("@r", "Coupon_" + ma));
                    uuDai = promotions.IsInstalled()
                        ? promotions.KiemTraCoupon(userId, ma, bg.DongGioHang())
                        : KetQuaApDungUuDai.Loi("Chức năng mã giảm giá chưa sẵn sàng.");
                    if (!uuDai.Success)
                    {
                        tx.Rollback();
                        return KetQuaTaoDon.Loi(400, "COUPON_INVALID", "Mã " + ma + " không áp dụng được: " + uuDai.Message);
                    }
                }

                total = Math.Max(0, bg.TamTinh - (uuDai != null ? uuDai.TienGiam : 0));
                db.DonHang.Add(new DonHang
                {
                    maDonHang = orderId,
                    ngayDat = DateTime.Now,
                    tongTien = total,
                    trangThai = PaymentService.ChoThanhToan,
                    maHocVien = userId,
                    maGiamGia = uuDai != null ? uuDai.MaGiamGia : null
                });
                foreach (var d in bg.Dong)
                {
                    db.ChiTietDonHang.Add(new ChiTietDonHang
                    {
                        maChiTiet = "CT" + Guid.NewGuid().ToString("N").Substring(0, 18),
                        maDonHang = orderId,
                        maKhoaHoc = d.MaKhoaHoc,
                        donGia = d.Gia
                    });
                }
                db.SaveChanges();

                // Giá gốc và chương trình giảm trực tiếp của từng dòng (cột migration 021, chưa có trong mô hình EF)
                CourseDiscountService.GhiGiaGocDong(db, orderId, bg.Dong);
                if (uuDai != null) promotions.GhiNhanSuDung(orderId, userId, uuDai);
                payment.DatThongTinDonCho(orderId, total > 0 ? phuongThuc : "Miễn phí");
                // Trigger chi tiết đơn hàng tính lại tổng tiền; đặt lại số tiền thực trả để chắc chắn khớp
                db.Database.ExecuteSqlCommand("UPDATE dbo.DonHang SET tongTien = @t WHERE maDonHang = @dh",
                    new SqlParameter("@t", total), new SqlParameter("@dh", orderId));
                tx.Commit();
            }

            payment.GhiNhatKy(orderId, "TAO_DON", null, null, null,
                "Tạo đơn (app) " + total.ToString("#,##0") + "đ, phương thức " + (total > 0 ? phuongThuc : "Miễn phí"));

            if (total <= 0)
            {
                payment.XacNhanDonMienPhi(orderId);
            }
            return new KetQuaTaoDon { Success = true, MaDonHang = orderId, TongTien = total, CanThanhToan = total > 0 };
        }
    }
}
