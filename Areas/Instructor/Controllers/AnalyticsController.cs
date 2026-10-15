using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Models;
using ECOLearning.Models;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class AnalyticsController : InstructorBaseController
    {
        // GET: Instructor/Analytics (UC12: Báo cáo thống kê khóa học)
        public ActionResult Index(string period = "month")
        {
            var model = BuildAnalyticsModel(period);
            ViewBag.BreadcrumbCurrent = "Báo cáo thống kê";
            return View(model);
        }

        private InstructorAnalyticsViewModel BuildAnalyticsModel(string period)
        {
            int teacherId = CurrentTeacherId;
            var model = new InstructorAnalyticsViewModel();
            model.Period = (period ?? "month").ToLowerInvariant();
            if (model.Period != "week" && model.Period != "year") model.Period = "month";
            DateTime today = DateTime.Now.Date;
            DateTime start = model.Period == "week" ? today.AddDays(-6)
                : model.Period == "year" ? new DateTime(today.Year, 1, 1)
                : new DateTime(today.Year, today.Month, 1);
            DateTime end = today.AddDays(1);
            model.StartDate = start;
            model.EndDate = today;
            var courses = db.KhoaHoc.Where(k => k.maGiaoVien == teacherId).ToList();
            var courseIds = courses.Select(k => k.maKhoaHoc).ToList();
            var paidDetails = db.ChiTietDonHang.Where(ct => courseIds.Contains(ct.maKhoaHoc)
                && ct.DonHang != null && ct.DonHang.ngayDat >= start && ct.DonHang.ngayDat < end
                && (ct.DonHang.trangThai == "Đã thanh toán" || ct.DonHang.trangThai == "Hoàn tất"
                    || ct.DonHang.trangThai == "Success")).ToList();
            model.TotalRevenue = paidDetails.Sum(ct => (decimal?)ct.donGia) ?? 0;
            var registrations = db.DangKyKhoaHoc.Where(dk => courseIds.Contains(dk.maKhoaHoc)
                && dk.ngayDangKy >= start && dk.ngayDangKy < end).ToList();
            model.TotalRegistrations = registrations.Count;
            model.TotalStudents = registrations.Select(r => r.maHocVien).Distinct().Count();
            if (model.Period == "year")
            {
                for (int month = 1; month <= 12; month++)
                {
                    model.ChartLabels.Add("T" + month);
                    model.RegistrationData.Add(registrations.Count(r => r.ngayDangKy.Value.Month == month));
                    decimal revenue = paidDetails.Where(ct => ct.DonHang.ngayDat.Value.Month == month)
                        .Sum(ct => (decimal?)ct.donGia) ?? 0;
                    model.RevenueData.Add(revenue / 1000000m);
                }
            }
            else
            {
                for (DateTime day = start; day < end; day = day.AddDays(1))
                {
                    model.ChartLabels.Add(day.ToString("dd/MM"));
                    model.RegistrationData.Add(registrations.Count(r => r.ngayDangKy.Value.Date == day));
                    decimal revenue = paidDetails.Where(ct => ct.DonHang.ngayDat.Value.Date == day)
                        .Sum(ct => (decimal?)ct.donGia) ?? 0;
                    model.RevenueData.Add(revenue / 1000000m);
                }
            }
            var reviews = db.DanhGiaKhoaHoc.Where(dg => courseIds.Contains(dg.maKhoaHoc)
                && dg.ngayDanhGia >= start && dg.ngayDanhGia < end && dg.soSao >= 1 && dg.soSao <= 5).ToList();
            model.TotalReviews = reviews.Count;
            model.AverageRating = reviews.Any() ? Math.Round(reviews.Average(dg => dg.soSao), 1) : 0;
            for (int star = 1; star <= 5; star++)
                model.RatingStarCounts[star - 1] = reviews.Count(dg => dg.soSao == star);

            var progress = db.TienDoHocTap.Where(p => courseIds.Contains(p.maKhoaHoc)).ToList();
            model.Courses = courses.Select(course => {
                var courseReviews = reviews.Where(r => r.maKhoaHoc == course.maKhoaHoc).ToList();
                var students = registrations.Where(r => r.maKhoaHoc == course.maKhoaHoc)
                    .Select(r => r.maHocVien).Distinct().ToList();
                double? averageProgress = students.Any() ? (double?)Math.Round(students.Average(student => {
                    var latest = progress.Where(p => p.maKhoaHoc == course.maKhoaHoc && p.maHocVien == student)
                        .OrderByDescending(p => p.lanTruyCapGanNhat).FirstOrDefault();
                    return Math.Max(0, Math.Min(100, latest == null ? 0 : latest.phanTramHoanThanh ?? 0));
                }), 1) : null;
                return new CourseAnalyticsItem {
                    Id = course.maKhoaHoc, Name = course.tenKhoaHoc, Status = course.trangThai,
                    Revenue = paidDetails.Where(d => d.maKhoaHoc == course.maKhoaHoc).Sum(d => (decimal?)d.donGia) ?? 0,
                    Registrations = registrations.Count(r => r.maKhoaHoc == course.maKhoaHoc),
                    Reviews = courseReviews.Count,
                    AverageProgress = averageProgress
                };
            }).OrderByDescending(c => c.Revenue).ThenByDescending(c => c.Registrations).ToList();
            return model;
        }

        // GET: Instructor/Analytics/ExportCsv
        public ActionResult ExportCsv(string period = "month")
        {
            var model = BuildAnalyticsModel(period);
            var sb = new StringBuilder();
            sb.AppendLine("BÁO CÁO THỐNG KÊ DOANH THU GIẢNG VIÊN - E-Learning");
            sb.AppendLine(string.Format("Kỳ báo cáo: {0} | Ngày trích xuất: {1:dd/MM/yyyy HH:mm}", model.Period.ToUpper(), DateTime.Now));
            sb.AppendLine();
            sb.AppendLine("Thoi Gian,Doanh Thu (Trieu VND),Luot ghi danh");

            for (int i = 0; i < model.ChartLabels.Count; i++)
            {
                sb.AppendLine(string.Format("\"{0}\",{1},{2}", model.ChartLabels[i], model.RevenueData[i].ToString(System.Globalization.CultureInfo.InvariantCulture), model.RegistrationData[i]));
            }

            sb.AppendLine();
            sb.AppendLine(string.Format("Tổng doanh thu,{0}", model.TotalRevenue));
            sb.AppendLine(string.Format("Tổng lượt ghi danh,{0}", model.TotalRegistrations));
            sb.AppendLine(string.Format("Điểm đánh giá trung bình,{0}", model.AverageRating.ToString(System.Globalization.CultureInfo.InvariantCulture)));

            sb.AppendLine();
            sb.AppendLine("Khóa học,Doanh thu (VND),Lượt ghi danh,Số nhận xét,Tiến độ học tập TB (%)");
            foreach (var course in model.Courses)
            {
                string name = course.Name ?? "";
                if (name.Length > 0 && "=+-@".Contains(name[0])) name = "'" + name;
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "\"{0}\",{1},{2},{3},{4}", name.Replace("\"", "\"\""), course.Revenue,
                    course.Registrations, course.Reviews, course.AverageProgress.HasValue ? course.AverageProgress.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : ""));
            }
            byte[] preamble = Encoding.UTF8.GetPreamble();
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] fileBytes = preamble.Concat(bytes).ToArray();

            return File(fileBytes, "text/csv", string.Format("BaoCaoGiangVien_{0:yyyyMMdd}.csv", DateTime.Now));
        }
    }
}
