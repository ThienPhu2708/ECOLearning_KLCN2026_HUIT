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
                .Where(ct => courseIds.Contains(ct.maKhoaHoc) && ct.DonHang != null && (ct.DonHang.trangThai == "Đã thanh toán" || ct.DonHang.trangThai == "Hoàn tất" || ct.DonHang.trangThai == "Success" || ct.DonHang.trangThai == null))
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
                NgayYeuCau = p.ngayYeuCau ?? DateTime.Now,
                TrangThai = p.trangThai ?? "Chờ duyệt",
                NganHang = p.nganHang ?? "N/A",
                SoTaiKhoan = p.soTaiKhoan ?? "N/A",
                TenChuTaiKhoan = p.tenChuTaiKhoan ?? "N/A"
            }).ToList();

            ViewBag.BreadcrumbCurrent = "Nhận thanh toán";
            return View(model);
        }

        // POST: Instructor/Payout/RequestPayout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestPayout(decimal soTien, string nganHang, string soTaiKhoan, string tenChuTaiKhoan)
        {
            int teacherId = CurrentTeacherId;

            // Ngưỡng tối thiểu rút tiền 500,000 VNĐ
            if (soTien < 500000m)
            {
                TempData["ErrorMessage"] = "Số tiền yêu cầu rút tối thiểu là 500.000 VNĐ!";
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(nganHang) || string.IsNullOrWhiteSpace(soTaiKhoan) || string.IsNullOrWhiteSpace(tenChuTaiKhoan))
            {
                TempData["ErrorMessage"] = "Vui lòng điền đầy đủ thông tin tài khoản ngân hàng nhận tiền!";
                return RedirectToAction("Index");
            }

            // Tính số dư khả dụng
            var courseIds = db.KhoaHoc.Where(k => k.maGiaoVien == teacherId).Select(k => k.maKhoaHoc).ToList();
            var paidDetails = db.ChiTietDonHang.Where(ct => courseIds.Contains(ct.maKhoaHoc) && ct.DonHang != null).ToList();
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
            string phieuId = "PC" + DateTime.Now.ToString("yyMMddHHmmss");

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

            TempData["SuccessMessage"] = string.Format("Gửi yêu cầu nhận thanh toán {0:N0} đ thành công! Mã phiếu chi: {1}.", soTien, phieuId);
            return RedirectToAction("Index");
        }
    }
}
