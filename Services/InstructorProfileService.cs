using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Hồ sơ và số liệu của một giáo viên tính từ CSDL: khóa đang bán, tổng học viên (không trùng), tổng lượt đánh giá,
    /// điểm trung bình trên mọi lượt đánh giá. Dùng chung cho trang hồ sơ (/User) và mục "Giảng viên" ở trang chi tiết khóa học
    /// để hai nơi luôn cùng một con số.
    /// </summary>
    public class InstructorProfileService
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public InstructorProfileService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public InstructorProfileViewModel LayHoSo(int targetUserId)
        {
            var user = db.NguoiDung.Find(targetUserId);
            if (user == null)
            {
                return null;
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

            // Truy vấn danh sách khóa học của giáo viên. Không Include(DanhGiaKhoaHoc / DangKyKhoaHoc): nạp cùng lúc
            // 2 danh sách con sinh truy vấn UNION ALL cần nhiều bộ nhớ, SQL Server có thể bắt chờ tới 25 giây.
            var courses = db.KhoaHoc
                .Include(k => k.DanhMuc)
                .Where(k => k.maGiaoVien == targetUserId &&
                           (k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active"))
                .OrderByDescending(k => k.maKhoaHoc)
                .ToList();

            // Tính thống kê thực tế từ CSDL theo yêu cầu người dùng
            var courseIds = courses.Select(k => k.maKhoaHoc).ToList();
            int totalStudents = 0;
            int totalReviews = 0;
            double avgRating = 0.0;
            var enrollCounts = new Dictionary<string, int>();
            var ratingStats = new Dictionary<string, Tuple<int, double>>();

            if (courseIds.Any())
            {
                // 1. Tổng số học viên đã đăng ký khóa học của giảng viên này
                totalStudents = db.DangKyKhoaHoc
                    .Where(dk => courseIds.Contains(dk.maKhoaHoc))
                    .Select(dk => dk.maHocVien)
                    .Distinct()
                    .Count();

                // Số học viên và lượt / điểm đánh giá của từng khóa: GROUP BY
                enrollCounts = db.DangKyKhoaHoc
                    .Where(dk => courseIds.Contains(dk.maKhoaHoc))
                    .GroupBy(dk => dk.maKhoaHoc)
                    .Select(g => new { MaKhoaHoc = g.Key, SoHocVien = g.Count() })
                    .ToDictionary(x => x.MaKhoaHoc, x => x.SoHocVien);

                ratingStats = db.DanhGiaKhoaHoc
                    .Where(dg => courseIds.Contains(dg.maKhoaHoc))
                    .GroupBy(dg => dg.maKhoaHoc)
                    .Select(g => new { MaKhoaHoc = g.Key, SoDanhGia = g.Count(), Diem = g.Average(dg => (double)dg.soSao) })
                    .ToList()
                    .ToDictionary(x => x.MaKhoaHoc, x => Tuple.Create(x.SoDanhGia, x.Diem));

                // 2. Tổng lượt đánh giá từ học viên và điểm trung bình trên mọi lượt đánh giá
                totalReviews = ratingStats.Values.Sum(r => r.Item1);
                if (totalReviews > 0)
                {
                    avgRating = Math.Round(ratingStats.Values.Sum(r => r.Item1 * r.Item2) / totalReviews, 1);
                }
            }

            var courseItems = courses.Select(k =>
            {
                int soHocVien;
                enrollCounts.TryGetValue(k.maKhoaHoc, out soHocVien);
                Tuple<int, double> danhGia;
                ratingStats.TryGetValue(k.maKhoaHoc, out danhGia);
                return new KhoaHocCuaGiaoVienItem
                {
                    Course = k,
                    SoHocVien = soHocVien,
                    SoDanhGia = danhGia != null ? danhGia.Item1 : 0,
                    DiemDanhGia = danhGia != null ? Math.Round(danhGia.Item2, 1) : 0
                };
            }).ToList();

            return new InstructorProfileViewModel
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
                // Dữ liệu gốc (null khi giáo viên chưa khai); câu mặc định do trang hồ sơ tự thêm (UserController.BuildProfile)
                ChuyenMon = !string.IsNullOrWhiteSpace(app?.Specialization) ? app.Specialization.Trim() : null,
                KinhNghiem = !string.IsNullOrWhiteSpace(app?.BioExperience) ? app.BioExperience.Trim() : null,
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
                TongSoKhoaHoc = courses.Count,

                // Danh sách khóa học của giáo viên
                DanhSachKhoaHoc = courseItems
            };
        }
    }
}
