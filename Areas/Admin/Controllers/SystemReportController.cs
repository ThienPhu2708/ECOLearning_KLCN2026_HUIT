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
        private static readonly string[] ChartColors = { "#434CE8", "#10B981", "#F59E0B", "#EC4899", "#8B5CF6", "#3B82F6",
                                                          "#14B8A6", "#EF4444", "#84CC16", "#F97316", "#06B6D4", "#A855F7" };

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
        // Mỗi card có bộ lọc riêng và lấy số liệu qua các action JSON bên dưới; trang chỉ dựng khung, thẻ thống kê giáo viên
        // và card Lưu trữ video (trước là trang Admin/VideoStorage riêng).
        public ActionResult Index()
        {
            var model = new SystemReportPageViewModel
            {
                Years = NamCoDuLieu(),
                Categories = db.DanhMuc.OrderBy(c => c.tenDanhMuc).ToList()
                    .Select(c => new KeyValuePair<string, string>(c.maDanhMuc, c.tenDanhMuc)).ToList()
            };
            var giaoVien = BuildSystemReportModel("year", "", null, null);
            model.TotalInstructors = giaoVien.TotalInstructors;
            model.InstructorsWithPublishedCourse = giaoVien.InstructorsWithPublishedCourse;
            model.PendingTeacherApplications = giaoVien.PendingTeacherApplications;
            model.Video = LuuTruVideo();
            ViewBag.BreadcrumbCurrent = "Báo cáo hệ thống";
            return View(model);
        }

        private List<int> NamCoDuLieu()
        {
            var namDon = db.DonHang.Where(d => d.ngayDat.HasValue).Select(d => d.ngayDat.Value.Year).Distinct().ToList();
            var namDangKy = db.DangKyKhoaHoc.Where(d => d.ngayDangKy.HasValue).Select(d => d.ngayDangKy.Value.Year).Distinct().ToList();
            int namDau = Math.Min(namDon.Concat(namDangKy).DefaultIfEmpty(DateTime.Now.Year).Min(), DateTime.Now.Year);
            var nam = new List<int>();
            for (int y = DateTime.Now.Year; y >= namDau; y--) nam.Add(y);
            return nam;
        }

        private VideoStorageSummary LuuTruVideo()
        {
            string ffmpeg;
            bool coFfmpeg = VideoStorageService.KiemTraFfmpeg(out ffmpeg);
            var dangCho = VideoStorageService.DanhSachDangCho();
            return new VideoStorageSummary
            {
                R2DaCauHinh = R2Storage.DaCauHinh,
                TenBucket = R2Storage.TenBucket,
                FfmpegSanSang = coFfmpeg,
                FfmpegThongTin = ffmpeg,
                BaiTrenR2 = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.BaiHoc WHERE duongDanVideo LIKE N'r2:%'").Single(),
                BaiTrenMayChu = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.BaiHoc WHERE duongDanVideo LIKE N'/assets/uploads/videos/%'").Single(),
                FileTrenMayChu = db.Database.SqlQuery<int>("SELECT COUNT(DISTINCT duongDanVideo) FROM dbo.BaiHoc WHERE duongDanVideo LIKE N'/assets/uploads/videos/%'").Single(),
                CoFileSql = System.IO.File.Exists(VideoStorageService.DuongDanFileSql),
                DangCho = dangCho,
                LichSu = VideoStorageService.LichSuGanDay()
            };
        }

        // ---------------------------------------------------------------- dữ liệu cho từng card (JSON)

        // Tất cả dòng bán hàng đã thanh toán, doanh thu thực thu (tiền giảm của mã chia về từng khóa theo tỷ lệ đơn giá)
        private List<SaleLine> TatCaDongBan()
        {
            var lines = (from ct in db.ChiTietDonHang
                         join dh in db.DonHang on ct.maDonHang equals dh.maDonHang
                         join kh in db.KhoaHoc on ct.maKhoaHoc equals kh.maKhoaHoc
                         where PaidOrderStatuses.Contains(dh.trangThai)
                         select new { dh.maDonHang, dh.ngayDat, dh.maHocVien, ct.donGia, kh.maKhoaHoc, kh.maDanhMuc, kh.maGiaoVien })
                .ToList()
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
            var tyLe = new RevenueService(db).TyLeThucThuTheoDon();
            foreach (var l in lines)
            {
                decimal r;
                if (tyLe.TryGetValue(l.OrderId, out r)) l.Amount = Math.Round(l.Amount * r, 0);
            }
            return lines;
        }

        private static int NamHopLe(int? year)
        {
            return Math.Max(2000, Math.Min(year ?? DateTime.Now.Year, DateTime.Now.Year + 1));
        }

        // Một kỳ cụ thể: tháng (index 1-12) / quý (1-4) / năm của year, hoặc toàn thời gian
        private static void KhoangThoiGian(string period, int? year, int? index, out DateTime tu, out DateTime denTruoc, out string ten)
        {
            int nam = NamHopLe(year);
            if (period == "year") { tu = new DateTime(nam, 1, 1); denTruoc = tu.AddYears(1); ten = "năm " + nam; }
            else if (period == "quarter")
            {
                int q = Math.Max(1, Math.Min(index ?? ((DateTime.Now.Month - 1) / 3 + 1), 4));
                tu = new DateTime(nam, (q - 1) * 3 + 1, 1); denTruoc = tu.AddMonths(3); ten = "quý " + q + "/" + nam;
            }
            else if (period == "month")
            {
                int m = Math.Max(1, Math.Min(index ?? DateTime.Now.Month, 12));
                tu = new DateTime(nam, m, 1); denTruoc = tu.AddMonths(1); ten = "tháng " + m + "/" + nam;
            }
            else { tu = new DateTime(2000, 1, 1); denTruoc = DateTime.Now.AddDays(1); ten = "toàn thời gian"; }
        }

        private JsonResult KetQua(object data)
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            return Json(data, JsonRequestBehavior.AllowGet);
        }

        // GET: Admin/SystemReport/RevenueChart?period=month|quarter|year&year=2026
        // Tháng: 12 tháng của năm; Quý: 4 quý của năm; Năm: 5 năm kết thúc ở năm chọn. Doanh thu (đ) và số đơn đã thanh toán.
        [HttpGet]
        public JsonResult RevenueChart(string period = "month", int? year = null)
        {
            int nam = NamHopLe(year);
            List<string> nhan;
            Func<DateTime, int> viTri;
            string khoang;
            if (period == "quarter") { nhan = Enumerable.Range(1, 4).Select(q => "Quý " + q).ToList(); viTri = d => d.Year == nam ? (d.Month - 1) / 3 : -1; khoang = "4 quý năm " + nam; }
            else if (period == "year") { nhan = Enumerable.Range(nam - 4, 5).Select(y => y.ToString()).ToList(); viTri = d => d.Year >= nam - 4 && d.Year <= nam ? d.Year - (nam - 4) : -1; khoang = "năm " + (nam - 4) + " – " + nam; }
            else { period = "month"; nhan = Enumerable.Range(1, 12).Select(m => "T" + m).ToList(); viTri = d => d.Year == nam ? d.Month - 1 : -1; khoang = "12 tháng năm " + nam; }

            var doanhThu = new decimal[nhan.Count];
            var donHang = new HashSet<string>[nhan.Count];
            for (int i = 0; i < nhan.Count; i++) donHang[i] = new HashSet<string>();
            foreach (var l in TatCaDongBan())
            {
                int i = viTri(l.Date);
                if (i < 0) continue;
                doanhThu[i] += l.Amount;
                donHang[i].Add(l.OrderId);
            }
            var soDon = donHang.Select(h => h.Count).ToArray();
            return KetQua(new { success = true, period, year = nam, range = khoang, labels = nhan, revenue = doanhThu, orders = soDon, totalRevenue = doanhThu.Sum(), totalOrders = soDon.Sum() });
        }

        // GET: Admin/SystemReport/CategoryShare?metric=revenue|enrollments|courses&period=month|quarter|year|all&year=&index=
        // Cơ cấu theo danh mục. Số khóa học không có ngày tạo nên luôn tính toàn bộ, bỏ qua kỳ.
        [HttpGet]
        public JsonResult CategoryShare(string metric = "revenue", string period = "all", int? year = null, int? index = null)
        {
            DateTime tu, denTruoc;
            string ten;
            KhoangThoiGian(metric == "courses" ? "all" : period, year, index, out tu, out denTruoc, out ten);
            var danhMuc = db.DanhMuc.OrderBy(c => c.tenDanhMuc).ToList();
            Dictionary<string, decimal> giaTri;
            string donVi;
            if (metric == "courses")
            {
                giaTri = db.KhoaHoc.GroupBy(k => k.maDanhMuc).Select(g => new { g.Key, n = g.Count() }).ToList().ToDictionary(x => x.Key ?? "", x => (decimal)x.n);
                donVi = "khóa học";
            }
            else if (metric == "enrollments")
            {
                giaTri = (from dk in db.DangKyKhoaHoc
                          join kh in db.KhoaHoc on dk.maKhoaHoc equals kh.maKhoaHoc
                          where dk.ngayDangKy >= tu && dk.ngayDangKy < denTruoc
                          group dk by kh.maDanhMuc into g
                          select new { g.Key, n = g.Count() }).ToList().ToDictionary(x => x.Key ?? "", x => (decimal)x.n);
                donVi = "lượt đăng ký";
            }
            else
            {
                metric = "revenue";
                giaTri = TatCaDongBan().Where(l => l.Date >= tu && l.Date < denTruoc).GroupBy(l => l.CategoryId ?? "")
                    .ToDictionary(g => g.Key, g => g.Sum(l => l.Amount));
                donVi = "đ";
            }
            decimal tong = giaTri.Values.Sum();
            int mau = 0;
            var phan = danhMuc.Where(c => giaTri.ContainsKey(c.maDanhMuc) && giaTri[c.maDanhMuc] > 0)
                .OrderByDescending(c => giaTri[c.maDanhMuc])
                .Select(c => new
                {
                    label = c.tenDanhMuc,
                    amount = giaTri[c.maDanhMuc],
                    value = tong > 0 ? Math.Round((double)(giaTri[c.maDanhMuc] / tong * 100), 1) : 0,
                    color = ChartColors[mau++ % ChartColors.Length]
                }).ToList();
            return KetQua(new { success = true, metric, unit = donVi, range = ten, total = tong, segments = phan });
        }

        // GET: Admin/SystemReport/TeacherRevenue?period=month|quarter|year|all&year=&index=
        // Mọi giáo viên (kể cả chưa có doanh thu); trang tự tìm theo tên, sắp xếp và phân trang.
        [HttpGet]
        public JsonResult TeacherRevenue(string period = "all", int? year = null, int? index = null)
        {
            DateTime tu, denTruoc;
            string ten;
            KhoangThoiGian(period, year, index, out tu, out denTruoc, out ten);
            var dong = TatCaDongBan().Where(l => l.Date >= tu && l.Date < denTruoc).ToList();
            var khoaHoc = db.KhoaHoc.Select(k => new { k.maGiaoVien, k.trangThai }).ToList();
            var giaoVien = db.NguoiDung.Where(u => u.vaiTro == "GiaoVien" || u.vaiTro == "Giáo viên" || u.vaiTro == "Instructor")
                .Select(u => new { u.maNguoiDung, u.hoTen }).ToList();
            var ids = giaoVien.Select(g => g.maNguoiDung).Union(khoaHoc.Select(k => k.maGiaoVien)).Distinct().ToList();
            var tenNguoi = db.NguoiDung.Where(u => ids.Contains(u.maNguoiDung)).ToDictionary(u => u.maNguoiDung, u => u.hoTen);
            decimal tong = dong.Sum(l => l.Amount);
            var hang = ids.Select(id =>
            {
                var cua = dong.Where(l => l.TeacherId == id).ToList();
                decimal dt = cua.Sum(l => l.Amount);
                return new
                {
                    id,
                    name = tenNguoi.ContainsKey(id) ? tenNguoi[id] : "Giáo viên #" + id,
                    courses = khoaHoc.Count(k => k.maGiaoVien == id),
                    published = khoaHoc.Count(k => k.maGiaoVien == id && CourseStatus.IsPublished(k.trangThai)),
                    orders = cua.Select(l => l.OrderId).Distinct().Count(),
                    revenue = dt,
                    share = tong > 0 ? Math.Round((double)(dt / tong * 100), 1) : 0,
                    url = Url.Action("Details", "Teacher", new { area = "Admin", id })
                };
            }).OrderByDescending(r => r.revenue).ThenBy(r => r.name).ToList();
            return KetQua(new { success = true, range = ten, total = tong, rows = hang });
        }

        // GET: Admin/SystemReport/CourseStatusStats?category= (khóa học theo trạng thái, lọc theo danh mục)
        [HttpGet]
        public JsonResult CourseStatusStats(string category = "")
        {
            var khoa = db.KhoaHoc.Where(k => category == "" || category == null || k.maDanhMuc == category).Select(k => k.trangThai).ToList();
            int tong = khoa.Count;
            var nhom = khoa.GroupBy(t => CourseStatus.GroupLabel(t))
                .Select(g => new
                {
                    label = g.Key,
                    count = g.Count(),
                    percent = tong > 0 ? Math.Round((double)g.Count() / tong * 100, 1) : 0,
                    color = CourseStatus.GroupColor(g.Key),
                    order = CourseStatus.GroupOrder(g.Key)
                })
                .OrderBy(s => s.order).ToList();
            return KetQua(new { success = true, total = tong, rows = nhom });
        }

        // GET: Admin/SystemReport/PeriodTable?period=month|quarter|year&year=2026
        [HttpGet]
        public JsonResult PeriodTable(string period = "month", int? year = null)
        {
            int nam = NamHopLe(year);
            if (period != "quarter" && period != "year") period = "month";
            var hang = BangTheoKy(TatCaDongBan(), period, nam);
            return KetQua(new
            {
                success = true,
                period,
                year = nam,
                rows = hang.Select(r => new { label = r.GiaiDoan, revenue = r.DoanhThu, orders = r.SoDonHang, avg = r.GiaTriDonTrungBinh, students = r.HocVienMoi, growth = r.TangTruong })
            });
        }

        // Bảng số liệu theo kỳ: 12 tháng / 4 quý của năm chọn, hoặc các năm có dữ liệu (tới năm chọn)
        private static List<PeriodReportRow> BangTheoKy(List<SaleLine> lines, string period, int nam)
        {
            var ket = new List<PeriodReportRow>();
            bool namNay = nam == DateTime.Now.Year;
            decimal prev = 0;
            if (period == "month")
            {
                for (int m = 1; m <= 12; m++)
                {
                    var rows = lines.Where(l => l.Date.Year == nam && l.Date.Month == m).ToList();
                    bool future = (nam > DateTime.Now.Year || (namNay && m > DateTime.Now.Month)) && !rows.Any();
                    ket.Add(BuildPeriodRow($"Tháng {m} / {nam}" + (namNay && m == DateTime.Now.Month ? " (Hiện tại)" : ""), rows, ref prev, future));
                }
            }
            else if (period == "year")
            {
                var years = lines.Select(l => l.Date.Year).Where(y => y <= nam).Distinct().ToList();
                if (!years.Contains(nam)) years.Add(nam);
                foreach (int y in years.OrderBy(y => y))
                {
                    var rows = lines.Where(l => l.Date.Year == y).ToList();
                    ket.Add(BuildPeriodRow($"Năm {y}" + (y == DateTime.Now.Year ? " (Hiện tại)" : ""), rows, ref prev, false));
                }
            }
            else
            {
                int currentQuarter = (DateTime.Now.Month - 1) / 3 + 1;
                for (int q = 1; q <= 4; q++)
                {
                    var rows = lines.Where(l => l.Date.Year == nam && (l.Date.Month - 1) / 3 + 1 == q).ToList();
                    bool future = (nam > DateTime.Now.Year || (namNay && q > currentQuarter)) && !rows.Any();
                    ket.Add(BuildPeriodRow($"Quý {q} / {nam}" + (namNay && q == currentQuarter ? " (Hiện tại)" : ""), rows, ref prev, future));
                }
            }
            return ket;
        }

        // ---------------------------------------------------------------- dữ liệu cho file Excel / PDF

        private SystemReportViewModel BuildSystemReportModel(string period, string category, int? teacherId, int? year)
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

            // Doanh thu thực thu: đơn có coupon thì phân bổ tiền giảm (SuDungUuDai) về từng khóa theo tỷ lệ đơn giá,
            // để tổng doanh thu khớp DonHang.tongTien và báo cáo doanh thu theo khoảng ngày (RevenueService)
            var tyLeThucThu = new RevenueService(db).TyLeThucThuTheoDon();
            foreach (var l in lines)
            {
                decimal tyLe;
                if (tyLeThucThu.TryGetValue(l.OrderId, out tyLe)) l.Amount = Math.Round(l.Amount * tyLe, 0);
            }

            model.TotalNetRevenue = lines.Sum(l => l.Amount);
            model.TotalOrders = lines.Select(l => l.OrderId).Distinct().Count();
            model.AvgOrderValue = model.TotalOrders > 0 ? Math.Round(model.TotalNetRevenue / model.TotalOrders, 0) : 0;
            model.NewStudentsCount = isFiltered
                ? lines.Select(l => l.StudentId).Distinct().Count()
                : db.NguoiDung.Count(u => u.vaiTro.Contains("HocVien") || u.vaiTro.Contains("Học viên") || u.vaiTro == "Student");

            int currentYear = NamHopLe(year);

            // ---- 2. Biểu đồ 2 trục: doanh thu & số đơn qua 12 tháng của năm báo cáo ----
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

            // ---- 4. Bảng số liệu theo kỳ (Tháng / Quý / Năm) của năm báo cáo ----
            model.PeriodReports = BangTheoKy(lines, model.Period, currentYear);

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

        // GET: Admin/SystemReport/ExportExcel?period=&year= (theo kỳ và năm đang chọn ở bảng số liệu theo kỳ)
        public ActionResult ExportExcel(string period = "quarter", string category = "", int? teacher = null, int? year = null)
        {
            var model = BuildSystemReportModel(period, category, teacher, year);

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

            GhiHoatDong(db, "XuatBaoCao", "BaoCao", "HeThong", "Xuất báo cáo hệ thống CSV. Kỳ: " + model.Period);
            return File(fileBytes, "text/csv", $"BaoCaoHeThong_E-Learning_{DateTime.Now:yyyyMMdd}.csv");
        }

        // GET: Admin/SystemReport/ExportPdf?period=&year= (xuất file PDF thật, theo kỳ và năm đang chọn ở bảng số liệu theo kỳ)
        public ActionResult ExportPdf(string period = "quarter", string category = "", int? teacher = null, int? year = null)
        {
            var model = BuildSystemReportModel(period, category, teacher, year);
            try
            {
                byte[] pdf = SystemReportPdfService.Build(model);
                GhiHoatDong(db, "XuatBaoCao", "BaoCao", "HeThong", "Xuất báo cáo hệ thống PDF. Kỳ: " + model.Period);
                return File(pdf, "application/pdf", $"BaoCaoHeThong_E-Learning_{model.Period}_{DateTime.Now:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ExportPdf error: " + ex);
                return RedirectToAction("Index", new { pdfError = 1 });
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
