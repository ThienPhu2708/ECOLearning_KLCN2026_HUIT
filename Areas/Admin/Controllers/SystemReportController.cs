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

        // GET: Admin/SystemReport (UC16: Báo cáo thống kê toàn sàn)
        public ActionResult Index(string period = "quarter")
        {
            var model = BuildSystemReportModel(period);
            ViewBag.BreadcrumbCurrent = "Báo cáo hệ thống";
            return View(model);
        }

        private SystemReportViewModel BuildSystemReportModel(string period)
        {
            var model = new SystemReportViewModel();
            model.Period = string.IsNullOrEmpty(period) ? "quarter" : period.ToLower();

            // 1. Lấy toàn bộ đơn hàng thực tế từ CSDL DonHang
            var allOrders = db.DonHang.ToList();
            var paidOrders = allOrders
                .Where(d => d.trangThai == "Đã thanh toán" || d.trangThai == "Hoàn tất" || d.trangThai == "Success")
                .ToList();

            model.TotalNetRevenue = paidOrders.Sum(d => d.tongTien);
            model.TotalOrders = paidOrders.Count;
            model.AvgOrderValue = model.TotalOrders > 0 ? Math.Round(model.TotalNetRevenue / model.TotalOrders, 0) : 0;

            // Đếm số lượng học viên thực tế từ CSDL NguoiDung
            model.NewStudentsCount = db.NguoiDung.Count(u => u.vaiTro.Contains("HocVien") || u.vaiTro.Contains("Học viên") || u.vaiTro == "Student");

            int currentYear = DateTime.Now.Year;

            // 2. Tính toán Biểu đồ đường kép (Doanh thu & Số đơn hàng) thực tế qua 12 tháng
            model.ChartLabels = new List<string> { "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12" };
            model.RevenueChartData = new List<decimal>();
            model.OrderChartData = new List<int>();

            for (int m = 1; m <= 12; m++)
            {
                var monthOrders = paidOrders
                    .Where(d => {
                        DateTime orderDate = d.ngayDat ?? DateTime.Now;
                        return orderDate.Month == m && orderDate.Year == currentYear;
                    })
                    .ToList();

                decimal monthRev = monthOrders.Sum(d => d.tongTien);
                // Quy đổi sang triệu VNĐ để hiển thị biểu đồ
                model.RevenueChartData.Add(Math.Round(monthRev / 1000000m, 2));
                model.OrderChartData.Add(monthOrders.Count);
            }

            // 3. Cơ cấu doanh thu theo Danh mục khóa học từ CSDL
            var categories = db.DanhMuc.ToList();
            string[] chartColors = new string[] { "#434CE8", "#3B82F6", "#10B981", "#F59E0B", "#8B5CF6", "#EC4899", "#06B6D4" };
            int colorIndex = 0;

            var catStats = categories.Select(cat => {
                decimal catRevenue = 0;
                int courseCount = cat.KhoaHoc != null ? cat.KhoaHoc.Count : 0;

                if (cat.KhoaHoc != null)
                {
                    foreach (var kh in cat.KhoaHoc)
                    {
                        if (kh.ChiTietDonHang != null)
                        {
                            catRevenue += kh.ChiTietDonHang
                                .Where(ct => ct.DonHang != null && (ct.DonHang.trangThai == "Đã thanh toán" || ct.DonHang.trangThai == "Hoàn tất" || ct.DonHang.trangThai == "Success" || ct.DonHang.trangThai == null))
                                .Sum(ct => (decimal?)ct.donGia) ?? 0;
                        }
                    }
                }

                return new {
                    CategoryName = cat.tenDanhMuc,
                    Revenue = catRevenue,
                    CourseCount = courseCount
                };
            }).ToList();

            decimal totalCatRev = catStats.Sum(c => c.Revenue);

            if (totalCatRev > 0)
            {
                // Tính tỷ lệ % doanh thu thực tế của từng danh mục
                foreach (var item in catStats)
                {
                    double percent = Math.Round((double)(item.Revenue / totalCatRev * 100), 1);
                    model.CategoryRevenueSegments.Add(new DonutSegmentItem
                    {
                        Label = item.CategoryName,
                        Value = percent,
                        Color = chartColors[colorIndex % chartColors.Length]
                    });
                    colorIndex++;
                }
            }
            else
            {
                // Nếu chưa phát sinh doanh thu chi tiết, tính tỷ lệ % dựa trên số khóa học thực tế của từng danh mục trong CSDL
                int totalCourses = catStats.Sum(c => c.CourseCount);
                if (totalCourses == 0) totalCourses = 1;

                foreach (var item in catStats)
                {
                    double percent = Math.Round((double)item.CourseCount / totalCourses * 100, 1);
                    model.CategoryRevenueSegments.Add(new DonutSegmentItem
                    {
                        Label = item.CategoryName,
                        Value = percent > 0 ? percent : 0,
                        Color = chartColors[colorIndex % chartColors.Length]
                    });
                    colorIndex++;
                }
            }

            // 4. Bảng số liệu tổng hợp chi tiết theo kỳ báo cáo (Tháng / Quý / Năm) từ CSDL
            if (model.Period == "month")
            {
                decimal prevRev = 0;
                for (int m = 1; m <= 12; m++)
                {
                    var orders = paidOrders
                        .Where(d => {
                            DateTime orderDate = d.ngayDat ?? DateTime.Now;
                            return orderDate.Month == m && orderDate.Year == currentYear;
                        })
                        .ToList();

                    decimal rev = orders.Sum(d => d.tongTien);
                    int count = orders.Count;
                    decimal avg = count > 0 ? Math.Round(rev / count, 0) : 0;
                    int newStudents = orders.Select(o => o.maHocVien).Distinct().Count();

                    string growth = "+0.0%";
                    if (m > DateTime.Now.Month && rev == 0)
                    {
                        growth = "-";
                    }
                    else if (prevRev > 0)
                    {
                        double g = (double)((rev - prevRev) / prevRev * 100);
                        growth = (g >= 0 ? "+" : "") + g.ToString("0.0") + "%";
                    }
                    else if (rev > 0)
                    {
                        growth = "+100.0%";
                    }
                    prevRev = rev;

                    string label = $"Tháng {m} / {currentYear}" + (m == DateTime.Now.Month ? " (Hiện tại)" : "");
                    model.PeriodReports.Add(new PeriodReportRow
                    {
                        GiaiDoan = label,
                        DoanhThu = rev,
                        SoDonHang = count,
                        GiaTriDonTrungBinh = avg,
                        HocVienMoi = newStudents,
                        TangTruong = growth
                    });
                }
            }
            else if (model.Period == "year")
            {
                var years = paidOrders
                    .Select(d => (d.ngayDat ?? DateTime.Now).Year)
                    .Distinct()
                    .OrderBy(y => y)
                    .ToList();

                if (!years.Contains(currentYear))
                {
                    years.Add(currentYear);
                }

                decimal prevYearRev = 0;
                foreach (var y in years)
                {
                    var orders = paidOrders
                        .Where(d => (d.ngayDat ?? DateTime.Now).Year == y)
                        .ToList();

                    decimal rev = orders.Sum(d => d.tongTien);
                    int count = orders.Count;
                    decimal avg = count > 0 ? Math.Round(rev / count, 0) : 0;
                    int newStudents = orders.Select(o => o.maHocVien).Distinct().Count();

                    string growth = "+0.0%";
                    if (prevYearRev > 0)
                    {
                        double g = (double)((rev - prevYearRev) / prevYearRev * 100);
                        growth = (g >= 0 ? "+" : "") + g.ToString("0.0") + "%";
                    }
                    else if (rev > 0)
                    {
                        growth = "+100.0%";
                    }
                    prevYearRev = rev;

                    string label = $"Năm {y}" + (y == currentYear ? " (Hiện tại)" : "");
                    model.PeriodReports.Add(new PeriodReportRow
                    {
                        GiaiDoan = label,
                        DoanhThu = rev,
                        SoDonHang = count,
                        GiaTriDonTrungBinh = avg,
                        HocVienMoi = newStudents,
                        TangTruong = growth
                    });
                }
            }
            else
            {
                // Mặc định: Theo Quý trong năm hiện tại
                int currentQuarter = (DateTime.Now.Month - 1) / 3 + 1;
                decimal prevQRev = 0;

                for (int q = 1; q <= 4; q++)
                {
                    int startMonth = (q - 1) * 3 + 1;
                    int endMonth = q * 3;

                    var orders = paidOrders
                        .Where(d => {
                            DateTime orderDate = d.ngayDat ?? DateTime.Now;
                            return orderDate.Year == currentYear && orderDate.Month >= startMonth && orderDate.Month <= endMonth;
                        })
                        .ToList();

                    decimal rev = orders.Sum(d => d.tongTien);
                    int count = orders.Count;
                    decimal avg = count > 0 ? Math.Round(rev / count, 0) : 0;
                    int newStudents = orders.Select(o => o.maHocVien).Distinct().Count();

                    string growth = "+0.0%";
                    if (prevQRev > 0)
                    {
                        double g = (double)((rev - prevQRev) / prevQRev * 100);
                        growth = (g >= 0 ? "+" : "") + g.ToString("0.0") + "%";
                    }
                    else if (rev > 0)
                    {
                        growth = "+100.0%";
                    }
                    prevQRev = rev;

                    string label = $"Quý {q} / {currentYear}" + (q == currentQuarter ? " (Hiện tại)" : "");
                    model.PeriodReports.Add(new PeriodReportRow
                    {
                        GiaiDoan = label,
                        DoanhThu = rev,
                        SoDonHang = count,
                        GiaTriDonTrungBinh = avg,
                        HocVienMoi = newStudents,
                        TangTruong = growth
                    });

                    // Dừng nếu qua quý hiện tại và quý sau chưa có đơn
                    if (q >= currentQuarter && count == 0 && q > 1)
                    {
                        break;
                    }
                }
            }

            // 5. Thống kê giáo viên
            var teacherIds = db.NguoiDung
                .Where(u => u.vaiTro == "GiaoVien" || u.vaiTro == "Giáo viên" || u.vaiTro == "Instructor")
                .Select(u => u.maNguoiDung)
                .ToList();
            model.TotalInstructors = teacherIds.Count;

            var allCourses = db.KhoaHoc.Select(k => new { k.maGiaoVien, k.trangThai }).ToList();
            model.InstructorsWithPublishedCourse = allCourses
                .Where(k => CourseStatus.IsPublished(k.trangThai) && teacherIds.Contains(k.maGiaoVien))
                .Select(k => k.maGiaoVien)
                .Distinct()
                .Count();
            model.PendingTeacherApplications = TeacherApplicationService.GetAll().Count(a => a.Status == "Pending");

            // 6. Thống kê khóa học theo trạng thái (gom các cách viết khác nhau của cùng một trạng thái)
            model.TotalCourses = allCourses.Count;
            model.CourseStatusStats = allCourses
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

            // Tính % tăng trưởng chung
            if (model.PeriodReports.Count >= 2)
            {
                var latest = model.PeriodReports[model.PeriodReports.Count - 1];
                var previous = model.PeriodReports[model.PeriodReports.Count - 2];
                if (previous.DoanhThu > 0)
                {
                    model.GrowthPercentage = Math.Round((double)((latest.DoanhThu - previous.DoanhThu) / previous.DoanhThu * 100), 1);
                }
                else if (latest.DoanhThu > 0)
                {
                    model.GrowthPercentage = 100.0;
                }
                else
                {
                    model.GrowthPercentage = 0.0;
                }
            }
            else
            {
                model.GrowthPercentage = 0.0;
            }

            return model;
        }

        // GET: Admin/SystemReport/ExportExcel
        public ActionResult ExportExcel(string period = "quarter")
        {
            var model = BuildSystemReportModel(period);

            var sb = new StringBuilder();
            sb.AppendLine("BÁO CÁO DOANH THU VÀ VẬN HÀNH HỆ THỐNG ECOLearning");
            sb.AppendLine($"Kỳ báo cáo: {model.Period.ToUpper()} | Ngày trích xuất: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine();
            sb.AppendLine("Giai Doan,Doanh Thu Thuan (VND),So Don Hang,Gia Tri Don TB (VND),Hoc Vien Moi,Tang Truong");

            foreach (var row in model.PeriodReports)
            {
                sb.AppendLine($"\"{row.GiaiDoan}\",{row.DoanhThu},{row.SoDonHang},{row.GiaTriDonTrungBinh},{row.HocVienMoi},\"{row.TangTruong}\"");
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

            return File(fileBytes, "text/csv", $"BaoCaoHeThong_ECOLearning_{DateTime.Now:yyyyMMdd}.csv");
        }

        // GET: Admin/SystemReport/ExportPdf
        public ActionResult ExportPdf(string period = "quarter")
        {
            return RedirectToAction("Index", new { period = period, print = "true" });
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
