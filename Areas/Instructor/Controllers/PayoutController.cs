using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Models;
using ECOLearning.Models;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class PayoutController : InstructorBaseController
    {
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

            var paidDetails = db.ChiTietDonHang
                .Where(ct => courseIds.Contains(ct.maKhoaHoc) && ct.DonHang != null && (ct.DonHang.trangThai == "Đã thanh toán" || ct.DonHang.trangThai == "Hoàn tất" || ct.DonHang.trangThai == "Success"))
                .ToList();

            decimal grossRevenue = paidDetails.Sum(ct => (decimal?)ct.donGia) ?? 0;

            // Tỷ lệ chia sẻ doanh thu cho giảng viên là 80% (sàn thu phí 20%)
            decimal teacherShare = Math.Round(grossRevenue * 0.8m, 0);

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

            model.PayoutRequests = requests.Select(p => new PayoutItem
            {
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
            // Ngưỡng tối thiểu rút tiền 500,000 VNĐ
            if (!ModelState.IsValid || soTien < 500000m || soTien != decimal.Truncate(soTien))
            {
                TempData["ErrorMessage"] = "Số tiền yêu cầu rút tối thiểu là 500.000 VNĐ!";
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
            var courseIds = db.KhoaHoc.Where(k => k.maGiaoVien == teacherId).Select(k => k.maKhoaHoc).ToList();
            var paidDetails = db.ChiTietDonHang.Where(ct => courseIds.Contains(ct.maKhoaHoc) && ct.DonHang != null && (ct.DonHang.trangThai == "Đã thanh toán" || ct.DonHang.trangThai == "Hoàn tất" || ct.DonHang.trangThai == "Success")).ToList();
            decimal grossRevenue = paidDetails.Sum(ct => (decimal?)ct.donGia) ?? 0;
            decimal teacherShare = Math.Round(grossRevenue * 0.8m, 0);

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
                tiLeChietKhau = 0.2, // 20% phí nền tảng
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
    }
}
