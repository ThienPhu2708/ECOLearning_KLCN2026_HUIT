using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Areas.Admin.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Admin.Controllers
{
    public class SystemReportController : AdminBaseController
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private static readonly string[] PaidOrderStatuses = { "Đã thanh toán", "Hoàn tất", "Success" };
        private static readonly string[] ChartColors = { "#434CE8", "#3B82F6", "#10B981", "#F59E0B", "#8B5CF6", "#EC4899", "#06B6D4" };

        // Một dòng bán hàng = một khóa học trong một đơn hàng đã thanh toán
        private class SaleLine
        {
            public string OrderId;
            public DateTime Date;
            public int StudentId;
            public decimal Amount;
            public string CourseId;
            public string CategoryId;
            public int TeacherId;
        }

        // GET: Admin/SystemReport (UC16: Báo cáo thống kê toàn sàn)
        // category / teacher: phân tích dữ liệu theo danh mục hoặc giáo viên (bỏ trống = toàn hệ thống)
        public ActionResult Index(string period = "quarter", string category = "", int? teacher = null)
        {
            var model = BuildSystemReportModel(period, category, teacher);
            ViewBag.BreadcrumbCurrent = "Báo cáo hệ thống";
            return View(model);
        }

        private SystemReportViewModel BuildSystemReportModel(string period, string category, int? teacherId)
        {
            var model = new SystemReportViewModel();
            model.Period = string.IsNullOrEmpty(period) ? "quarter" : period.ToLower();
            if (model.Period != "month" && model.Period != "year") model.Period = "quarter";

            // ---- Bộ lọc phân tích ----
            var categories = db.DanhMuc.OrderBy(c => c.tenDanhMuc).ToList();
            var teachers = db.NguoiDung
                .Where(u => u.vaiTro == "GiaoVien" || u.vaiTro == "Giáo viên" || u.vaiTro == "Instructor")
                .OrderBy(u => u.hoTen)
                .ToList();

            model.FilterCategory = categories.Any(c => c.maDanhMuc == category) ? category : "";
            model.FilterTeacherId = teacherId.HasValue && teachers.Any(t => t.maNguoiDung == teacherId.Value) ? teacherId : null;
            model.CategoryOptions = categories.Select(c => new SelectListItem { Value = c.maDanhMuc, Text = c.tenDanhMuc, Selected = c.maDanhMuc == model.FilterCategory }).ToList();
            model.TeacherOptions = teachers.Select(t => new SelectListItem { Value = t.maNguoiDung.ToString(), Text = t.hoTen, Selected = t.maNguoiDung == model.FilterTeacherId }).ToList();

            var labels = new List<string>();
            if (model.FilterCategory != "") labels.Add("Danh mục: " + categories.First(c => c.maDanhMuc == model.FilterCategory).tenDanhMuc);
            if (model.FilterTeacherId.HasValue) labels.Add("Giáo viên: " + teachers.First(t => t.maNguoiDung == model.FilterTeacherId.Value).hoTen);
            model.FilterLabel = labels.Any() ? string.Join(" • ", labels) : "Toàn bộ hệ thống";
            bool isFiltered = labels.Any();

            // Khóa học trong phạm vi phân tích
            var scopeCourses = db.KhoaHoc.ToList()
                .Where(k => (model.FilterCategory == "" || k.maDanhMuc == model.FilterCategory)
                         && (!model.FilterTeacherId.HasValue || k.maGiaoVien == model.FilterTeacherId.Value))
                .ToList();
            var scopeCourseIds = new HashSet<string>(scopeCourses.Select(k => k.maKhoaHoc));

            // ---- 1. Dữ liệu bán hàng (chi tiết đơn hàng đã thanh toán) ----
            var lines = (from ct in db.ChiTietDonHang
                         join dh in db.DonHang on ct.maDonHang equals dh.maDonHang
                         join kh in db.KhoaHoc on ct.maKhoaHoc equals kh.maKhoaHoc
                         where PaidOrderStatuses.Contains(dh.trangThai)
                         select new { dh.maDonHang, dh.ngayDat, dh.maHocVien, ct.donGia, kh.maKhoaHoc, kh.maDanhMuc, kh.maGiaoVien })
                .ToList()
                .Where(x => scopeCourseIds.Contains(x.maKhoaHoc))
                .Select(x => new SaleLine
                {
                    OrderId = x.maDonHang,
                    Date = x.ngayDat ?? DateTime.Now,
                    StudentId = x.maHocVien,
                    Amount = x.donGia,
                    CourseId = x.maKhoaHoc,
                    CategoryId = x.maDanhMuc,
                    TeacherId = x.maGiaoVien
                })
                .ToList();

            model.TotalNetRevenue = lines.Sum(l => l.Amount);
            model.TotalOrders = lines.Select(l => l.OrderId).Distinct().Count();
            model.AvgOrderValue = model.TotalOrders > 0 ? Math.Round(model.TotalNetRevenue / model.TotalOrders, 0) : 0;
            model.NewStudentsCount = isFiltered
                ? lines.Select(l => l.StudentId).Distinct().Count()
                : db.NguoiDung.Count(u => u.vaiTro.Contains("HocVien") || u.vaiTro.Contains("Học viên") || u.vaiTro == "Student");

            int currentYear = DateTime.Now.Year;

            // ---- 2. Biểu đồ 2 trục: doanh thu & số đơn qua 12 tháng năm hiện tại ----
            model.ChartLabels = Enumerable.Range(1, 12).Select(m => "T" + m).ToList();
            for (int m = 1; m <= 12; m++)
            {
                var monthLines = lines.Where(l => l.Date.Year == currentYear && l.Date.Month == m).ToList();
                model.RevenueChartData.Add(Math.Round(monthLines.Sum(l => l.Amount) / 1000000m, 2));
                model.OrderChartData.Add(monthLines.Select(l => l.OrderId).Distinct().Count());
            }

            // ---- 3. Cơ cấu doanh thu: theo danh mục (hoặc theo khóa học khi đã lọc 1 danh mục) ----
            bool byCourse = model.FilterCategory != "";
            model.CategorySegmentsByCourse = byCourse;
            var groups = byCourse
                ? scopeCourses.Select(k => new { Label = k.tenKhoaHoc, Revenue = lines.Where(l => l.CourseId == k.maKhoaHoc).Sum(l => l.Amount), Count = 1 }).ToList()
                : categories.Select(c => new
                {
                    Label = c.tenDanhMuc,
                    Revenue = lines.Where(l => l.CategoryId == c.maDanhMuc).Sum(l => l.Amount),
                    Count = scopeCourses.Count(k => k.maDanhMuc == c.maDanhMuc)
                }).ToList();

            decimal totalGroupRevenue = groups.Sum(g => g.Revenue);
            int totalGroupCount = groups.Sum(g => g.Count);
            model.CategorySegmentsByRevenue = totalGroupRevenue > 0;
            int colorIndex = 0;
            foreach (var g in groups)
            {
                // Chưa có doanh thu thì tính tỷ trọng theo số khóa học
                double percent = totalGroupRevenue > 0
                    ? Math.Round((double)(g.Revenue / totalGroupRevenue * 100), 1)
                    : (totalGroupCount > 0 ? Math.Round((double)g.Count / totalGroupCount * 100, 1) : 0);
                if (percent <= 0) continue;
                model.CategoryRevenueSegments.Add(new DonutSegmentItem
                {
                    Label = g.Label,
                    Value = percent,
                    Color = ChartColors[colorIndex++ % ChartColors.Length]
                });
            }

            // ---- 4. Bảng số liệu theo kỳ (Tháng / Quý / Năm) ----
            if (model.Period == "month")
            {
                decimal prev = 0;
                for (int m = 1; m <= 12; m++)
                {
                    var rows = lines.Where(l => l.Date.Year == currentYear && l.Date.Month == m).ToList();
                    bool future = m > DateTime.Now.Month && !rows.Any();
                    model.PeriodReports.Add(BuildPeriodRow($"Tháng {m} / {currentYear}" + (m == DateTime.Now.Month ? " (Hiện tại)" : ""), rows, ref prev, future));
                }
            }
            else if (model.Period == "year")
            {
                var years = lines.Select(l => l.Date.Year).Distinct().ToList();
                if (!years.Contains(currentYear)) years.Add(currentYear);
                decimal prev = 0;
                foreach (int y in years.OrderBy(y => y))
                {
                    var rows = lines.Where(l => l.Date.Year == y).ToList();
                    model.PeriodReports.Add(BuildPeriodRow($"Năm {y}" + (y == currentYear ? " (Hiện tại)" : ""), rows, ref prev, false));
                }
            }
            else
            {
                int currentQuarter = (DateTime.Now.Month - 1) / 3 + 1;
                decimal prev = 0;
                for (int q = 1; q <= 4; q++)
                {
                    var rows = lines.Where(l => l.Date.Year == currentYear && (l.Date.Month - 1) / 3 + 1 == q).ToList();
                    model.PeriodReports.Add(BuildPeriodRow($"Quý {q} / {currentYear}" + (q == currentQuarter ? " (Hiện tại)" : ""), rows, ref prev, false));
                    // Dừng sau quý hiện tại nếu quý đó chưa có đơn
                    if (q >= currentQuarter && !rows.Any() && q > 1) break;
                }
            }

            // Tăng trưởng chung: kỳ gần nhất so với kỳ liền trước
            model.GrowthPercentage = 0.0;
            if (model.PeriodReports.Count >= 2)
            {
                var latest = model.PeriodReports[model.PeriodReports.Count - 1];
                var previous = model.PeriodReports[model.PeriodReports.Count - 2];
                if (previous.DoanhThu > 0)
                    model.GrowthPercentage = Math.Round((double)((latest.DoanhThu - previous.DoanhThu) / previous.DoanhThu * 100), 1);
                else if (latest.DoanhThu > 0)
                    model.GrowthPercentage = 100.0;
            }

            // ---- 5. Doanh thu theo giáo viên ----
            var teacherById = teachers.ToDictionary(t => t.maNguoiDung, t => t.hoTen);
            model.TeacherRevenueRows = scopeCourses
                .GroupBy(k => k.maGiaoVien)
                .Select(g => new TeacherRevenueRow
                {
                    TeacherId = g.Key,
                    TeacherName = teacherById.ContainsKey(g.Key) ? teacherById[g.Key] : (db.NguoiDung.Where(u => u.maNguoiDung == g.Key).Select(u => u.hoTen).FirstOrDefault() ?? "N/A"),
                    CourseCount = g.Count(),
                    PublishedCourseCount = g.Count(k => CourseStatus.IsPublished(k.trangThai)),
                    Orders = lines.Where(l => l.TeacherId == g.Key).Select(l => l.OrderId).Distinct().Count(),
                    Revenue = lines.Where(l => l.TeacherId == g.Key).Sum(l => l.Amount)
                })
                .OrderByDescending(r => r.Revenue)
                .ThenBy(r => r.TeacherName)
                .ToList();

            // ---- 6. Thống kê giáo viên (trong phạm vi lọc) ----
            var scopeTeacherIds = model.FilterTeacherId.HasValue
                ? new List<int> { model.FilterTeacherId.Value }
                : (model.FilterCategory != "" ? scopeCourses.Select(k => k.maGiaoVien).Distinct().ToList() : teachers.Select(t => t.maNguoiDung).ToList());
            model.TotalInstructors = scopeTeacherIds.Count;
            model.InstructorsWithPublishedCourse = scopeCourses
                .Where(k => CourseStatus.IsPublished(k.trangThai) && scopeTeacherIds.Contains(k.maGiaoVien))
                .Select(k => k.maGiaoVien)
                .Distinct()
                .Count();
            model.PendingTeacherApplications = TeacherApplicationService.GetAll().Count(a => a.Status == "Pending");

            // ---- 7. Khóa học theo trạng thái (trong phạm vi lọc) ----
            model.TotalCourses = scopeCourses.Count;
            model.CourseStatusStats = scopeCourses
                .GroupBy(k => CourseStatus.GroupLabel(k.trangThai))
                .Select(g => new CourseStatusStatItem
                {
                    Label = g.Key,
                    Count = g.Count(),
                    Percent = model.TotalCourses > 0 ? Math.Round((double)g.Count() / model.TotalCourses * 100, 1) : 0,
                    Color = CourseStatus.GroupColor(g.Key)
                })
                .OrderBy(s => CourseStatus.GroupOrder(s.Label))
                .ToList();

            return model;
        }

        private static PeriodReportRow BuildPeriodRow(string label, List<SaleLine> rows, ref decimal previousRevenue, bool isFuture)
        {
            decimal revenue = rows.Sum(l => l.Amount);
            int orders = rows.Select(l => l.OrderId).Distinct().Count();

            string growth = "+0.0%";
            if (isFuture)
                growth = "-";
            else if (previousRevenue > 0)
            {
                double g = (double)((revenue - previousRevenue) / previousRevenue * 100);
                growth = (g >= 0 ? "+" : "") + g.ToString("0.0") + "%";
            }
            else if (revenue > 0)
                growth = "+100.0%";
            previousRevenue = revenue;

            return new PeriodReportRow
            {
                GiaiDoan = label,
                DoanhThu = revenue,
                SoDonHang = orders,
                GiaTriDonTrungBinh = orders > 0 ? Math.Round(revenue / orders, 0) : 0,
                HocVienMoi = rows.Select(l => l.StudentId).Distinct().Count(),
                TangTruong = growth
            };
        }

        // GET: Admin/SystemReport/ExportExcel (cùng kỳ báo cáo và bộ lọc với màn hình)
        public ActionResult ExportExcel(string period = "quarter", string category = "", int? teacher = null)
        {
            var model = BuildSystemReportModel(period, category, teacher);

            var sb = new StringBuilder();
            sb.AppendLine("BÁO CÁO DOANH THU VÀ VẬN HÀNH HỆ THỐNG E-Learning");
            sb.AppendLine($"Kỳ báo cáo: {model.Period.ToUpper()} | Phạm vi: {model.FilterLabel} | Ngày trích xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine();
            sb.AppendLine("Giai Doan,Doanh Thu Thuan (VND),So Don Hang,Gia Tri Don TB (VND),Hoc Vien Moi,Tang Truong");

            foreach (var row in model.PeriodReports)
            {
                sb.AppendLine($"\"{row.GiaiDoan}\",{row.DoanhThu},{row.SoDonHang},{row.GiaTriDonTrungBinh},{row.HocVienMoi},\"{row.TangTruong}\"");
            }

            sb.AppendLine();
            sb.AppendLine("DOANH THU THEO GIAO VIEN");
            sb.AppendLine("Giao Vien,So Khoa Hoc,Khoa Hoc Dang Ban,So Don,Doanh Thu (VND)");
            foreach (var r in model.TeacherRevenueRows)
            {
                sb.AppendLine($"\"{r.TeacherName}\",{r.CourseCount},{r.PublishedCourseCount},{r.Orders},{r.Revenue}");
            }

            sb.AppendLine();
            sb.AppendLine("THONG KE GIAO VIEN");
            sb.AppendLine($"Tong so giao vien,{model.TotalInstructors}");
            sb.AppendLine($"Giao vien co khoa hoc dang ban,{model.InstructorsWithPublishedCourse}");
            sb.AppendLine($"Ho so giao vien cho duyet,{model.PendingTeacherApplications}");

            sb.AppendLine();
            sb.AppendLine("THONG KE KHOA HOC THEO TRANG THAI");
            sb.AppendLine("Trang Thai,So Khoa Hoc,Ty Le (%)");
            foreach (var s in model.CourseStatusStats)
            {
                sb.AppendLine($"\"{s.Label}\",{s.Count},{s.Percent}");
            }
            sb.AppendLine($"\"Tong cong\",{model.TotalCourses},100");

            byte[] preamble = Encoding.UTF8.GetPreamble();
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] fileBytes = preamble.Concat(bytes).ToArray();

            return File(fileBytes, "text/csv", $"BaoCaoHeThong_E-Learning_{DateTime.Now:yyyyMMdd}.csv");
        }

        // GET: Admin/SystemReport/ExportPdf (xuất file PDF thật, cùng kỳ báo cáo và bộ lọc với màn hình)
        public ActionResult ExportPdf(string period = "quarter", string category = "", int? teacher = null)
        {
            var model = BuildSystemReportModel(period, category, teacher);
            try
            {
                byte[] pdf = SystemReportPdfService.Build(model);
                return File(pdf, "application/pdf", $"BaoCaoHeThong_E-Learning_{model.Period}_{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ExportPdf error: " + ex);
                return RedirectToAction("Index", new { period = model.Period, category = model.FilterCategory, teacher = model.FilterTeacherId, pdfError = 1 });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
