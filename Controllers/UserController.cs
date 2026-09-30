using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    public class UserController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: /User or /User/Index/{id} or /User?id={id}
        public ActionResult Index(int? id)
        {
            if (!User.Identity.IsAuthenticated && Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.RawUrl });
            }

            int targetUserId = id ?? (Session["UserId"] as int? ?? 0);
            if (targetUserId <= 0)
            {
                // Nếu không truyền id và không có session, lấy giảng viên đầu tiên có khóa học
                var firstTeacher = db.KhoaHoc
                    .Where(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active")
                    .Select(k => k.maGiaoVien)
                    .FirstOrDefault();

                if (firstTeacher > 0)
                {
                    targetUserId = firstTeacher;
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }

            var user = db.NguoiDung.Find(targetUserId);
            if (user == null)
            {
                return HttpNotFound("Không tìm thấy thông tin giảng viên.");
            }

            // Truy vấn thông tin chuyên môn từ bảng DangKyGiaoVien (chỉ lấy các trường trong CSDL)
            TeacherApplication app = null;
            try
            {
                string sql = @"
                    SELECT TOP 1 
                        d.maDon AS ApplicationId,
                        d.maNguoiDung AS UserId,
                        u.hoTen AS FullName,
                        u.email AS Email,
                        u.soDienThoai AS Phone,
                        d.chuyenMon AS Specialization,
                        d.kinhNghiem AS BioExperience,
                        d.tenNganHang AS BankName,
                        d.soTaiKhoan AS AccountNumber,
                        d.tenChuTaiKhoan AS AccountName,
                        d.tepChungChi AS CertificateFilePath,
                        d.tenTepChungChi AS CertificateFileName,
                        d.ngayGui AS CreatedAt,
                        d.ngayDuyet AS ReviewedAt,
                        d.trangThai AS Status,
                        d.lyDoTuChoi AS RejectionReason,
                        d.maAdmin AS AdminId
                    FROM DangKyGiaoVien d
                    INNER JOIN NguoiDung u ON d.maNguoiDung = u.maNguoiDung
                    WHERE d.maNguoiDung = @userId
                    ORDER BY 
                        CASE WHEN d.trangThai = 'Approved' THEN 1 
                             WHEN d.trangThai = N'Đã duyệt' THEN 1 
                             ELSE 2 END,
                        d.ngayGui DESC";

                app = db.Database.SqlQuery<TeacherApplication>(sql, new SqlParameter("@userId", targetUserId)).FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Fetch DangKyGiaoVien Error: " + ex.Message);
            }

            // Truy vấn danh sách khóa học của giáo viên
            var courses = db.KhoaHoc
                .Include(k => k.DanhMuc)
                .Include(k => k.NguoiDung)
                .Include(k => k.DanhGiaKhoaHoc)
                .Include(k => k.DangKyKhoaHoc)
                .Where(k => k.maGiaoVien == targetUserId && 
                           (k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active"))
                .OrderByDescending(k => k.maKhoaHoc)
                .ToList();

            // Tính thống kê thực tế từ CSDL theo yêu cầu người dùng
            var courseIds = courses.Select(k => k.maKhoaHoc).ToList();
            int totalStudents = 0;
            int totalReviews = 0;
            double avgRating = 0.0;

            if (courseIds.Any())
            {
                // 1. Tổng số học viên đã đăng ký khóa học của giảng viên này
                totalStudents = db.DangKyKhoaHoc
                    .Where(dk => courseIds.Contains(dk.maKhoaHoc))
                    .Select(dk => dk.maHocVien)
                    .Distinct()
                    .Count();

                // 2. Tổng lượt đánh giá từ học viên
                var reviews = db.DanhGiaKhoaHoc
                    .Where(dg => courseIds.Contains(dg.maKhoaHoc))
                    .ToList();

                totalReviews = reviews.Count;
                if (totalReviews > 0)
                {
                    avgRating = Math.Round(reviews.Average(dg => (double)dg.soSao), 1);
                }
                else
                {
                    avgRating = 0.0;
                }
            }

            var viewModel = new InstructorProfileViewModel
            {
                MaNguoiDung = user.maNguoiDung,
                HoTen = user.hoTen,
                Email = user.email,
                SoDienThoai = user.soDienThoai,
                AnhDaiDien = string.IsNullOrEmpty(user.anhDaiDien) ? "assets/images/avatars/avatar_default.jpg" : user.anhDaiDien,
                VaiTro = user.vaiTro,
                NgaySinh = user.ngaySinh,
                GioiTinh = user.gioiTinh,

                // Thông tin từ bảng DangKyGiaoVien (chỉ lấy các trường trong CSDL)
                MaDon = app != null ? app.ApplicationId : null,
                ChuyenMon = !string.IsNullOrWhiteSpace(app?.Specialization) ? app.Specialization : "Giảng viên chuyên môn E-Learning",
                KinhNghiem = !string.IsNullOrWhiteSpace(app?.BioExperience) ? app.BioExperience : "Chưa cập nhật tiểu sử kinh nghiệm giảng dạy.",
                TenNganHang = app != null ? app.BankName : null,
                SoTaiKhoan = app != null ? app.AccountNumber : null,
                TenChuTaiKhoan = app != null ? app.AccountName : null,
                TepChungChi = app != null ? app.CertificateFilePath : null,
                TenTepChungChi = app != null ? app.CertificateFileName : null,
                NgayGui = app != null ? (DateTime?)app.CreatedAt : null,
                NgayDuyet = app != null ? app.ReviewedAt : null,
                TrangThaiGiaoVien = app != null ? app.Status : (user.vaiTro == "GiaoVien" || user.vaiTro == "Giáo viên" ? "Approved" : "Active"),

                // Thống kê theo yêu cầu người dùng
                TongSoHocVien = totalStudents,
                TongLuotDanhGia = totalReviews,
                DiemDanhGiaTrungBinh = avgRating,
                TongSoKhoaHoc = courses != null ? courses.Count() : 0,

                // Danh sách khóa học của giáo viên
                DanhSachKhoaHoc = courses
            };

            return View(viewModel);
        }

        // GET: /User/Details/{id} (Alias)
        public ActionResult Details(int? id)
        {
            return Index(id);
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
