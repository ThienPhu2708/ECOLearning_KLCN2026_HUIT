using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Models;
using ECOLearning.Models;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class DashboardController : InstructorBaseController
    {
        // GET: Instructor/Dashboard
        public ActionResult Index(int coursePage = 1)
        {
            int teacherId = CurrentTeacherId;
            var model = new InstructorDashboardViewModel();

            // 1. Khóa học của giảng viên hiện tại
            var teacherCourses = db.KhoaHoc
                .Where(k => k.maGiaoVien == teacherId)
                .ToList();

            var courseIds = teacherCourses.Select(k => k.maKhoaHoc).ToList();

            // 2. Chi tiết đơn hàng của các khóa học này
            var allOrderDetails = db.ChiTietDonHang
                .Where(ct => courseIds.Contains(ct.maKhoaHoc))
                .ToList();

            var paidDetails = allOrderDetails
                .Where(ct => ct.DonHang != null && (ct.DonHang.trangThai == "Đã thanh toán" || ct.DonHang.trangThai == "Hoàn tất" || ct.DonHang.trangThai == "Success"))
                .ToList();

            model.TotalRevenue = paidDetails.Sum(ct => (decimal?)ct.donGia) ?? 0;

            var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time")).Date;
            int currentMonth = today.Month;
            int currentYear = today.Year;

            model.MonthRevenue = paidDetails
                .Where(ct => ct.DonHang != null && ct.DonHang.ngayDat.HasValue && ct.DonHang.ngayDat.Value.Month == currentMonth && ct.DonHang.ngayDat.Value.Year == currentYear)
                .Sum(ct => (decimal?)ct.donGia) ?? 0;

            // 3. Tổng số học viên ghi danh
            var enrollments = db.DangKyKhoaHoc
                .Where(dk => courseIds.Contains(dk.maKhoaHoc))
                .ToList();

            model.TotalStudents = enrollments.Select(dk => dk.maHocVien).Distinct().Count();
            if (model.TotalStudents == 0 && paidDetails.Any())
            {
                model.TotalStudents = paidDetails.Select(ct => ct.DonHang?.maHocVien).Distinct().Count();
            }

            // Học viên mới trong tháng
            model.NewStudentsThisMonth = enrollments
                .Where(dk => dk.ngayDangKy.HasValue && dk.ngayDangKy.Value.Month == currentMonth && dk.ngayDangKy.Value.Year == currentYear)
                .Select(dk => dk.maHocVien).Distinct().Count();

            // 4. Đánh giá trung bình & phân bổ sao
            var reviews = db.DanhGiaKhoaHoc
                .Where(dg => courseIds.Contains(dg.maKhoaHoc) && dg.soSao >= 1 && dg.soSao <= 5)
                .ToList();

            model.TotalReviews = reviews.Count;
            model.AverageRating = reviews.Any() ? Math.Round(reviews.Average(dg => dg.soSao), 1) : 0;

            // Phân bổ sao: index 0 = 5 sao, index 4 = 1 sao
            model.RatingDistribution[0] = reviews.Count(r => r.soSao == 5);
            model.RatingDistribution[1] = reviews.Count(r => r.soSao == 4);
            model.RatingDistribution[2] = reviews.Count(r => r.soSao == 3);
            model.RatingDistribution[3] = reviews.Count(r => r.soSao == 2);
            model.RatingDistribution[4] = reviews.Count(r => r.soSao == 1);

            // 5. Biểu đồ doanh thu 12 tháng
            model.ChartLabels = new List<string> { "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12" };
            for (int m = 1; m <= 12; m++)
            {
                decimal mRev = paidDetails
                    .Where(ct => ct.DonHang != null && ct.DonHang.ngayDat.HasValue && ct.DonHang.ngayDat.Value.Month == m && ct.DonHang.ngayDat.Value.Year == currentYear)
                    .Sum(ct => (decimal?)ct.donGia) ?? 0;
                
                model.RevenueChartData.Add(mRev);
            }

            // The three filters share the same paid orders; missing dates are zero-filled.
            var dailyRevenue = paidDetails
                .Where(ct => ct.DonHang != null && ct.DonHang.ngayDat.HasValue)
                .GroupBy(ct => ct.DonHang.ngayDat.Value.Date)
                .ToDictionary(g => g.Key, g => g.Sum(ct => ct.donGia));
            var monthDates = Enumerable.Range(1, DateTime.DaysInMonth(currentYear, currentMonth))
                .Select(day => new DateTime(currentYear, currentMonth, day)).ToList();
            var weekDates = Enumerable.Range(0, 7).Select(day => today.AddDays(day - 6)).ToList();
            ViewBag.RevenueChartFilters = new
            {
                year = new { labels = model.ChartLabels, data = model.RevenueChartData },
                month = new { labels = monthDates.Select(d => d.ToString("dd/MM")),
                    data = monthDates.Select(d => dailyRevenue.ContainsKey(d) ? dailyRevenue[d] : 0) },
                week = new { labels = weekDates.Select(d => d.ToString("dd/MM")),
                    data = weekDates.Select(d => dailyRevenue.ContainsKey(d) ? dailyRevenue[d] : 0) }
            };

            // 6. Top 3 khóa học mang lại doanh thu cao nhất
            // Khóa giáo viên đã xóa vẫn tính doanh thu ở trên nhưng không hiện trong danh sách khóa học
            var visibleCourses = teacherCourses.Where(k => k.trangThai != ECOLearning.Services.CourseStatus.DaXoa).ToList();
            var topList = visibleCourses.Select(k => {
                var kDetails = paidDetails.Where(ct => ct.maKhoaHoc == k.maKhoaHoc).ToList();
                decimal kRev = kDetails.Sum(ct => (decimal?)ct.donGia) ?? 0;
                int kStudents = enrollments.Count(dk => dk.maKhoaHoc == k.maKhoaHoc);
                if (kStudents == 0) kStudents = kDetails.Count;

                var kReviews = reviews.Where(dg => dg.maKhoaHoc == k.maKhoaHoc).ToList();
                double kRating = kReviews.Any() ? Math.Round(kReviews.Average(dg => dg.soSao), 1) : 0;

                return new TopCourseItem
                {
                    MaKhoaHoc = k.maKhoaHoc,
                    TenKhoaHoc = k.tenKhoaHoc,
                    AnhBia = k.anhBia ?? "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?w=300&auto=format&fit=crop&q=80",
                    SoHocVien = kStudents,
                    DoanhThu = kRev,
                    DiemDanhGia = kRating
                };
            })
            .OrderByDescending(t => t.DoanhThu)
            .ThenByDescending(t => t.SoHocVien)
            .Take(3)
            .ToList();

            model.TopCourses = topList;

            // 7. Danh sách tất cả khóa học để hiển thị bảng cuối trang
            var allDanhMuc = db.DanhMuc.ToList();
            int coursePages = (int)Math.Ceiling(visibleCourses.Count / 5.0);
            coursePage = Math.Max(1, Math.Min(coursePage, Math.Max(1, coursePages)));
            ViewBag.CoursePage = coursePage;
            ViewBag.CoursePages = coursePages;
            ViewBag.CourseTotal = visibleCourses.Count;
            model.RecentCourses = visibleCourses
                .OrderByDescending(k => k.maKhoaHoc)
                .Skip((coursePage - 1) * 5).Take(5)
                .Select(k =>
                {
                    int soHV = enrollments.Count(dk => dk.maKhoaHoc == k.maKhoaHoc);
                    string tenDM = allDanhMuc.FirstOrDefault(dm => dm.maDanhMuc == k.maDanhMuc)?.tenDanhMuc ?? "Khác";
                    return new InstructorCourseItem
                    {
                        MaKhoaHoc = k.maKhoaHoc,
                        TenKhoaHoc = k.tenKhoaHoc,
                        AnhBia = k.anhBia,
                        TenDanhMuc = tenDM,
                        GiaBan = k.giaBan,
                        SoHocVien = soHV,
                        TrangThai = k.trangThai ?? "BanNhap"
                    };
                }).ToList();

            // Số khóa học đang bị tạm khóa do vi phạm và chờ giáo viên xử lý
            ViewBag.ViolationWaitingCount = new ECOLearning.Services.ViolationReportService(db).CountWaitingForTeacher(teacherId);

            // Giáo viên chưa có chuyên môn được duyệt (giáo viên cũ): nhắc gửi đề xuất để tạo khóa mới
            var specialty = new ECOLearning.Services.TeacherSpecialtyService(db);
            bool needsSpecialty = specialty.NeedsSpecialty(teacherId);
            ViewBag.NeedsSpecialty = needsSpecialty;
            bool specialtyPending = needsSpecialty && specialty.HasPending(teacherId);
            ViewBag.SpecialtyPending = specialtyPending;
            // Tự mở popup (bắt đồng ý điều khoản) chỉ khi chưa gửi đề xuất và chưa tắt trong phiên này.
            // Đã gửi đề xuất (đang chờ duyệt) = đã đồng ý điều khoản: không tự hiện lại; nút "Tạo khóa học mới" vẫn mở popup "đang chờ duyệt".
            ViewBag.ShowSpecialtyPopup = needsSpecialty && !specialtyPending && Session[SpecialtyController.NoticeDismissedKey] == null;

            ViewBag.BreadcrumbCurrent = "Dashboard tổng quan";
            return View(model);
        }

    }
}
