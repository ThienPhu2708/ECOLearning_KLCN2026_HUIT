using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Models;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class PayoutController : InstructorBaseController
    {
        /// <summary>Giáo viên nhận 80% doanh thu thực thu; nền tảng giữ 20%. Phí 20% đã trừ ở đây nên phiếu chi không trừ thêm.</summary>
        public const decimal TyLeGiaoVien = RevenueService.TyLeGiaoVien;

        /// <summary>Số tiền tối thiểu mỗi lần rút (VNĐ). Dùng chung cho kiểm tra yêu cầu rút và trang Chính sách phân chia doanh thu.</summary>
        public const decimal RutToiThieu = 500000m;

        /// <summary>
        /// Doanh thu thực thu của các khóa thuộc giáo viên: chỉ đơn đã thanh toán (đơn chờ / thất bại / hết hạn không tính),
        /// mỗi dòng đơn tính theo số tiền học viên thực trả sau mã giảm giá (RevenueService.TyLeThucThuTheoDon phân bổ tiền giảm về từng khóa).
        /// </summary>
        private decimal DoanhThuThucThu(int teacherId)
        {
            var courseIds = db.KhoaHoc.Where(k => k.maGiaoVien == teacherId).Select(k => k.maKhoaHoc).ToList();
            var paid = db.ChiTietDonHang
                .Where(ct => courseIds.Contains(ct.maKhoaHoc) && ct.DonHang != null && RevenueService.TrangThaiThanhCong.Contains(ct.DonHang.trangThai))
                .Select(ct => new { ct.maDonHang, ct.donGia })
                .ToList();
            var tyLe = new RevenueService(db).TyLeThucThuTheoDon();
            decimal tong = 0;
            foreach (var d in paid)
            {
                decimal r;
                tong += tyLe.TryGetValue(d.maDonHang, out r) ? Math.Round(d.donGia * r, 0) : d.donGia;
            }
            return tong;
        }

        // GET: Instructor/Payout (UC13: Nhận thanh toán khóa học)
        public ActionResult Index()
        {
            int teacherId = CurrentTeacherId;
            var model = new InstructorPayoutViewModel();

            // 1. Tính tổng doanh thu tích lũy của giảng viên
            var courseIds = db.KhoaHoc
                .Where(k => k.maGiaoVien == teacherId)
                .Select(k => k.maKhoaHoc)
                .ToList();

            decimal grossRevenue = DoanhThuThucThu(teacherId);
            decimal teacherShare = Math.Round(grossRevenue * TyLeGiaoVien, 0);
            model.TongDoanhThuThucThu = grossRevenue;
            model.PhanGiaoVien = teacherShare;

            // 2. Lấy danh sách yêu cầu thanh toán từ NhanThanhToan
            var requests = db.NhanThanhToan
                .Where(p => p.maGiaoVien == teacherId)
                .OrderByDescending(p => p.ngayYeuCau)
                .ToList();

            decimal totalRequested = requests.Where(p => p.trangThai != "Từ chối").Sum(p => p.soTienYeuCau);
            model.TotalPaid = requests.Where(p => p.trangThai == "Đã thanh toán").Sum(p => p.thucNhan);
            model.PendingBalance = requests.Where(p => p.trangThai == "Chờ duyệt").Sum(p => p.soTienYeuCau);

            decimal available = teacherShare - totalRequested;
            model.AvailableBalance = available > 0 ? available : 0;

            // Thông tin Admin xử lý (cột của migration 014, ngoài mô hình EF)
            var xuLy = new Dictionary<string, XuLyPhieuChi>();
            try
            {
                xuLy = db.Database.SqlQuery<XuLyPhieuChi>(@"SELECT maPhieuChi AS MaPhieuChi, ngayXuLy AS NgayXuLy,
                        maGiaoDichChi AS MaGiaoDichChi, lyDoTuChoi AS LyDoTuChoi FROM dbo.NhanThanhToan WHERE maGiaoVien = @id",
                        new System.Data.SqlClient.SqlParameter("@id", teacherId))
                    .ToDictionary(x => x.MaPhieuChi);
            }
            catch (System.Data.SqlClient.SqlException) { /* chưa chạy migration 014 */ }

            model.PayoutRequests = requests.Select(p => new PayoutItem
            {
                NgayXuLy = xuLy.ContainsKey(p.maPhieuChi) ? xuLy[p.maPhieuChi].NgayXuLy : null,
                MaGiaoDichChi = xuLy.ContainsKey(p.maPhieuChi) ? xuLy[p.maPhieuChi].MaGiaoDichChi : null,
                LyDoTuChoi = xuLy.ContainsKey(p.maPhieuChi) ? xuLy[p.maPhieuChi].LyDoTuChoi : null,
                MaPhieuChi = p.maPhieuChi,
                SoTien = p.soTienYeuCau,
                ThucNhan = p.thucNhan,
                NgayYeuCau = p.ngayYeuCau,
                TrangThai = p.trangThai ?? "Chờ duyệt",
                NganHang = p.nganHang ?? "N/A",
                SoTaiKhoan = p.soTaiKhoan ?? "N/A",
                TenChuTaiKhoan = p.tenChuTaiKhoan ?? "N/A"
            }).ToList();

            ViewBag.PaymentProfile = PaymentProfile();
            ViewBag.BreadcrumbCurrent = "Nhận thanh toán";
            return View(model);
        }

        private TeacherApplication PaymentProfile()
        {
            return db.Database.SqlQuery<TeacherApplication>(@"SELECT TOP 1 maDon AS ApplicationId,
                tenNganHang AS BankName, soTaiKhoan AS AccountNumber, tenChuTaiKhoan AS AccountName
                FROM DangKyGiaoVien WHERE maNguoiDung = @id
                ORDER BY CASE WHEN trangThai IN ('Approved', N'Đã duyệt') THEN 0 ELSE 1 END,
                ngayGui DESC, maDon DESC", new System.Data.SqlClient.SqlParameter("@id", CurrentTeacherId)).FirstOrDefault();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdatePaymentInfo(string nganHang, string soTaiKhoan, string tenChuTaiKhoan)
        {
            if (string.IsNullOrWhiteSpace(nganHang) || string.IsNullOrWhiteSpace(soTaiKhoan)
                || string.IsNullOrWhiteSpace(tenChuTaiKhoan) || nganHang.Trim().Length > 100
                || tenChuTaiKhoan.Trim().Length > 100
                || !System.Text.RegularExpressions.Regex.IsMatch(soTaiKhoan.Trim(), @"^\d{4,50}$"))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đủ thông tin ngân hàng; số tài khoản gồm 4–50 chữ số.";
                return RedirectToAction("Index");
            }
            var profile = PaymentProfile();
            if (profile == null)
            {
                TempData["ErrorMessage"] = "Chưa tìm thấy hồ sơ đăng ký giáo viên để cập nhật thông tin thanh toán.";
                return RedirectToAction("Index");
            }
            db.Database.ExecuteSqlCommand(@"UPDATE DangKyGiaoVien SET tenNganHang=@bank,
                soTaiKhoan=@number, tenChuTaiKhoan=@name WHERE maDon=@application AND maNguoiDung=@id",
                new System.Data.SqlClient.SqlParameter("@bank", nganHang.Trim()),
                new System.Data.SqlClient.SqlParameter("@number", soTaiKhoan.Trim()),
                new System.Data.SqlClient.SqlParameter("@name", tenChuTaiKhoan.Trim().ToUpperInvariant()),
                new System.Data.SqlClient.SqlParameter("@application", profile.ApplicationId),
                new System.Data.SqlClient.SqlParameter("@id", CurrentTeacherId));
            TempData["SuccessMessage"] = "Đã cập nhật thông tin thanh toán. Tài khoản mới áp dụng cho các yêu cầu rút tiền gửi sau này.";
            return RedirectToAction("Index");
        }

        // POST: Instructor/Payout/RequestPayout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestPayout(decimal soTien)
        {
            int teacherId = CurrentTeacherId;

            var profile = PaymentProfile();
            string nganHang = profile != null ? profile.BankName : null;
            string soTaiKhoan = profile != null ? profile.AccountNumber : null;
            string tenChuTaiKhoan = profile != null ? profile.AccountName : null;
            // Ngưỡng tối thiểu rút tiền (RutToiThieu)
            if (!ModelState.IsValid || soTien < RutToiThieu || soTien != decimal.Truncate(soTien))
            {
                TempData["ErrorMessage"] = "Số tiền yêu cầu rút tối thiểu là " + RutToiThieu.ToString("N0", new System.Globalization.CultureInfo("vi-VN")) + " VNĐ!";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(nganHang) || string.IsNullOrWhiteSpace(soTaiKhoan) || string.IsNullOrWhiteSpace(tenChuTaiKhoan))
            {
                TempData["ErrorMessage"] = "Vui lòng điền đầy đủ thông tin tài khoản ngân hàng nhận tiền!";
                return RedirectToAction("Index");
            }

            if (nganHang.Trim().Length > 100 || soTaiKhoan.Trim().Length > 50 || tenChuTaiKhoan.Trim().Length > 100
                || !System.Text.RegularExpressions.Regex.IsMatch(soTaiKhoan.Trim(), @"^\d{4,50}$"))
            {
                TempData["ErrorMessage"] = "Thông tin ngân hàng không hợp lệ. Số tài khoản phải gồm 4–50 chữ số.";
                return RedirectToAction("Index");
            }
            using (var transaction = db.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
            {
            // Tính số dư khả dụng
            decimal teacherShare = Math.Round(DoanhThuThucThu(teacherId) * TyLeGiaoVien, 0);

            var requests = db.NhanThanhToan.Where(p => p.maGiaoVien == teacherId && p.trangThai != "Từ chối").ToList();
            decimal totalRequested = requests.Sum(p => p.soTienYeuCau);
            decimal available = teacherShare - totalRequested;

            if (soTien > available)
            {
                TempData["ErrorMessage"] = string.Format("Số tiền yêu cầu ({0:N0} đ) vượt quá số dư khả dụng hiện tại ({1:N0} đ)!", soTien, available);
                return RedirectToAction("Index");
            }

            // Tạo mã phiếu chi
            string phieuId = "PC" + Guid.NewGuid().ToString("N").Substring(0, 18);

            var payout = new NhanThanhToan
            {
                maPhieuChi = phieuId,
                maGiaoVien = teacherId,
                soTienYeuCau = soTien,
                // Phí nền tảng 20% đã trừ khi tính số dư (TyLeGiaoVien), nên phiếu chi không trừ thêm:
                // trigger trg_NhanThanhToan_TinhThucNhan tính thucNhan = soTien x (1 - tiLeChietKhau / 100)
                tiLeChietKhau = 0,
                thucNhan = Math.Round(soTien, 0),
                ngayYeuCau = DateTime.Now,
                nganHang = nganHang.Trim(),
                soTaiKhoan = soTaiKhoan.Trim(),
                tenChuTaiKhoan = tenChuTaiKhoan.Trim().ToUpper(),
                trangThai = "Chờ duyệt"
            };

            db.NhanThanhToan.Add(payout);
            db.SaveChanges();
            transaction.Commit();

            TempData["SuccessMessage"] = string.Format("Gửi yêu cầu nhận thanh toán {0:N0} đ thành công! Mã phiếu chi: {1}.", soTien, phieuId);
            return RedirectToAction("Index");
            }
        }

        private class XuLyPhieuChi
        {
            public string MaPhieuChi { get; set; }
            public DateTime? NgayXuLy { get; set; }
            public string MaGiaoDichChi { get; set; }
            public string LyDoTuChoi { get; set; }
        }
    }
}
