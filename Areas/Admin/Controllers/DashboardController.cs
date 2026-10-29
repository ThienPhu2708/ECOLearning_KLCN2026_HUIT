using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Areas.Admin.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Admin.Controllers
{
    public class DashboardController : AdminBaseController
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Admin/Dashboard
        public ActionResult Index()
        {
            var model = new AdminDashboardViewModel();

            // 1. KPI Doanh thu toàn sàn từ CSDL (chỉ tính các đơn đã thanh toán/thành công)
            model.TotalRevenue = db.DonHang
                .Where(d => d.trangThai == "Đã thanh toán" || d.trangThai == "Hoàn tất" || d.trangThai == "Success" || d.trangThai == "Đã duyệt")
                .Sum(d => (decimal?)d.tongTien) ?? 0;

            // 2. KPI Tổng số người dùng (Học viên & Giáo viên) từ CSDL
            model.TotalUsers = db.NguoiDung.Count();
            
            var allUsers = db.NguoiDung.ToList();
            model.TotalStudents = allUsers.Count(u => !string.IsNullOrEmpty(u.vaiTro) && 
                (u.vaiTro.ToLower().Contains("hocvien") || u.vaiTro.ToLower().Contains("hoc vien") || u.vaiTro.ToLower().Contains("student") || u.vaiTro.ToLower() == "hv"));
            
            model.TotalInstructors = allUsers.Count(u => !string.IsNullOrEmpty(u.vaiTro) && 
                (u.vaiTro.ToLower().Contains("giaovien") || u.vaiTro.ToLower().Contains("giao vien") || u.vaiTro.ToLower().Contains("teacher") || u.vaiTro.ToLower().Contains("instructor") || u.vaiTro.ToLower() == "gv"));

            // 3. KPI Khóa học đang hoạt động từ CSDL
            model.ActiveCoursesCount = db.KhoaHoc
                .Count(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đang hoạt động" || k.trangThai == "Active");

            // 4 & 5. Hàng đợi khóa học đang chờ kiểm duyệt thực tế từ CSDL
            var rawPending = db.KhoaHoc
                .Where(k => k.trangThai == "Chờ duyệt" || k.trangThai == "Chờ kiểm duyệt" || k.trangThai == "Pending")
                .ToList();

            model.PendingCoursesCount = rawPending.Count;

            model.PendingCourses = rawPending.Select(k => {
                var kiemDuyet = k.KiemDuyetKhoaHoc != null ? k.KiemDuyetKhoaHoc.OrderByDescending(kd => kd.ngayGui).FirstOrDefault() : null;
                int videoCount = 0;
                int documentCount = 0;

                if (k.ChuongHoc != null)
                {
                    foreach (var ch in k.ChuongHoc)
                    {
                        if (ch.BaiHoc != null)
                        {
                            videoCount += ch.BaiHoc.Count(b => !string.IsNullOrEmpty(b.duongDanVideo));
                            foreach (var bh in ch.BaiHoc)
                            {
                                if (bh.TaiNguyen != null)
                                {
                                    documentCount += bh.TaiNguyen.Count();
                                }
                            }
                        }
                    }
                }

                return new PendingCourseItem
                {
                    MaKhoaHoc = k.maKhoaHoc,
                    TenKhoaHoc = k.tenKhoaHoc,
                    TenGiaoVien = k.NguoiDung != null ? k.NguoiDung.hoTen : "Chưa cập nhật",
                    TenDanhMuc = k.DanhMuc != null ? k.DanhMuc.tenDanhMuc : "Khác",
                    NgayGui = kiemDuyet != null ? kiemDuyet.ngayGui : (DateTime?)null,
                    HocPhi = k.giaBan,
                    SoVideo = videoCount,
                    SoTaiLieu = documentCount,
                    TrangThai = k.trangThai
                };
            }).ToList();

            // 6. Doanh thu thực thu tháng này / tháng trước (thẻ KPI). Biểu đồ đường lấy số liệu qua ChartData theo bộ lọc
            var dauThang = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var banHang = LayDongBanHang(dauThang.AddMonths(-1), dauThang.AddMonths(1));
            model.RevenueThisMonth = banHang.Where(l => l.Ngay >= dauThang).Sum(l => l.TienThucThu);
            model.RevenueLastMonth = banHang.Where(l => l.Ngay < dauThang).Sum(l => l.TienThucThu);

            var namDon = db.DonHang.Where(d => d.ngayDat.HasValue).Select(d => d.ngayDat.Value.Year).Distinct().ToList();
            var namDangKy = db.DangKyKhoaHoc.Where(d => d.ngayDangKy.HasValue).Select(d => d.ngayDangKy.Value.Year).Distinct().ToList();
            int namDau = namDon.Concat(namDangKy).DefaultIfEmpty(DateTime.Now.Year).Min();
            for (int y = DateTime.Now.Year; y >= Math.Min(namDau, DateTime.Now.Year); y--) model.ChartYears.Add(y);

            // 6b. Đơn hàng mới nhất (mọi trạng thái), giới hạn 100 đơn; bảng phân trang 10 đơn mỗi trang
            model.RecentOrders = db.DonHang.OrderByDescending(d => d.ngayDat).Take(100)
                .Select(d => new RecentOrderItem
                {
                    MaDonHang = d.maDonHang,
                    NgayDat = d.ngayDat,
                    MaHocVien = d.maHocVien,
                    TenHocVien = db.NguoiDung.Where(n => n.maNguoiDung == d.maHocVien).Select(n => n.hoTen).FirstOrDefault(),
                    SoKhoaHoc = db.ChiTietDonHang.Count(c => c.maDonHang == d.maDonHang),
                    TongTien = d.tongTien,
                    TrangThai = d.trangThai
                }).ToList();

            // 7. Dữ liệu biểu đồ Donut Tỷ trọng danh mục khóa học thực tế từ CSDL
            var categories = db.DanhMuc.ToList();
            model.CategoryOptions = categories.OrderBy(c => c.tenDanhMuc)
                .Select(c => new KeyValuePair<string, string>(c.maDanhMuc, c.tenDanhMuc)).ToList();
            int totalCourses = db.KhoaHoc.Count();

            // 12 màu khác nhau rõ để các danh mục không trùng màu
            string[] chartColors = new string[] { "#434CE8", "#10B981", "#F59E0B", "#EC4899", "#8B5CF6", "#3B82F6",
                                                  "#14B8A6", "#EF4444", "#84CC16", "#F97316", "#06B6D4", "#A855F7" };
            int colorIndex = 0;

            if (totalCourses > 0)
            {
                foreach (var cat in categories)
                {
                    int count = cat.KhoaHoc != null ? cat.KhoaHoc.Count : 0;
                    if (count > 0)
                    {
                        double percentage = Math.Round((double)count / totalCourses * 100, 1);
                        model.CategoryDonutSegments.Add(new DonutSegmentItem
                        {
                            Label = cat.tenDanhMuc,
                            Value = percentage,
                            Count = count,
                            Color = chartColors[colorIndex % chartColors.Length]
                        });
                        colorIndex++;
                    }
                }
            }

            // 8. Số báo cáo vi phạm khóa học chưa xử lý (0 nếu chưa chạy script tạo bảng)
            model.PendingViolationReports = new ECOLearning.Services.ViolationReportService(db).CountPendingReports();

            ViewBag.BreadcrumbCurrent = "Dashboard hệ thống";
            return View(model);
        }

        // Một dòng bán hàng = một khóa trong đơn đã thanh toán; tiền giảm của mã được chia về từng khóa theo tỷ lệ đơn giá
        private class DongBanHang
        {
            public DateTime Ngay;
            public decimal TienThucThu;
            public string MaKhoaHoc;
            public string MaDanhMuc;
            public int MaGiaoVien;
        }

        private List<DongBanHang> LayDongBanHang(DateTime tu, DateTime denTruoc)
        {
            var trangThai = RevenueService.TrangThaiThanhCong;
            var dong = (from ct in db.ChiTietDonHang
                        join dh in db.DonHang on ct.maDonHang equals dh.maDonHang
                        join kh in db.KhoaHoc on ct.maKhoaHoc equals kh.maKhoaHoc
                        where trangThai.Contains(dh.trangThai) && dh.ngayDat >= tu && dh.ngayDat < denTruoc
                        select new { dh.maDonHang, dh.ngayDat, ct.donGia, kh.maKhoaHoc, kh.maDanhMuc, kh.maGiaoVien }).ToList();
            var tyLe = new RevenueService(db).TyLeThucThuTheoDon();
            return dong.Select(x =>
            {
                decimal r;
                return new DongBanHang
                {
                    Ngay = x.ngayDat.Value,
                    TienThucThu = tyLe.TryGetValue(x.maDonHang, out r) ? Math.Round(x.donGia * r, 0) : x.donGia,
                    MaKhoaHoc = x.maKhoaHoc,
                    MaDanhMuc = x.maDanhMuc,
                    MaGiaoVien = x.maGiaoVien
                };
            }).ToList();
        }

        // GET: Admin/Dashboard/ChartData?period=month|quarter|year&year=2026
        // Tháng: 12 tháng của năm chọn; Quý: 4 quý của năm chọn; Năm: 5 năm, kết thúc ở năm chọn.
        // Kèm top 10 khóa học theo doanh thu trong cùng khoảng thời gian (trang chọn hiện 5 hoặc 10).
        [HttpGet]
        public JsonResult ChartData(string period = "month", int? year = null)
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            int nam = Math.Max(2000, Math.Min(year ?? DateTime.Now.Year, DateTime.Now.Year + 1));
            DateTime tu, denTruoc;
            List<string> nhan;
            Func<DateTime, int> viTri;
            string khoang;
            if (period == "quarter")
            {
                tu = new DateTime(nam, 1, 1); denTruoc = tu.AddYears(1);
                nhan = Enumerable.Range(1, 4).Select(q => "Quý " + q).ToList();
                viTri = d => (d.Month - 1) / 3;
                khoang = "4 quý năm " + nam;
            }
            else if (period == "year")
            {
                tu = new DateTime(nam - 4, 1, 1); denTruoc = new DateTime(nam + 1, 1, 1);
                nhan = Enumerable.Range(nam - 4, 5).Select(y => y.ToString()).ToList();
                viTri = d => d.Year - (nam - 4);
                khoang = "năm " + (nam - 4) + " – " + nam;
            }
            else
            {
                period = "month";
                tu = new DateTime(nam, 1, 1); denTruoc = tu.AddYears(1);
                nhan = Enumerable.Range(1, 12).Select(m => "T" + m).ToList();
                viTri = d => d.Month - 1;
                khoang = "12 tháng năm " + nam;
            }

            var banHang = LayDongBanHang(tu, denTruoc);
            var dangKy = db.DangKyKhoaHoc.Where(d => d.ngayDangKy >= tu && d.ngayDangKy < denTruoc).Select(d => d.ngayDangKy.Value).ToList();
            var doanhThu = new decimal[nhan.Count];
            var luotDangKy = new int[nhan.Count];
            foreach (var l in banHang) doanhThu[viTri(l.Ngay)] += l.TienThucThu;
            foreach (var d in dangKy) luotDangKy[viTri(d)]++;

            var maKhoa = banHang.Select(l => l.MaKhoaHoc).Distinct().ToList();
            var tenKhoa = db.KhoaHoc.Where(k => maKhoa.Contains(k.maKhoaHoc)).ToDictionary(k => k.maKhoaHoc, k => k.tenKhoaHoc);
            var tenGv = TenGiaoVien(banHang);

            var topKhoaHoc = banHang.GroupBy(l => l.MaKhoaHoc)
                .Select(g => new
                {
                    id = g.Key,
                    name = tenKhoa.ContainsKey(g.Key) ? tenKhoa[g.Key] : g.Key,
                    teacher = tenGv.ContainsKey(g.First().MaGiaoVien) ? tenGv[g.First().MaGiaoVien] : "",
                    sold = g.Count(),
                    revenue = g.Sum(l => l.TienThucThu),
                    url = Url.Action("Details", "Course", new { area = "", id = g.Key })
                })
                .OrderByDescending(x => x.revenue).ThenByDescending(x => x.sold).Take(10).ToList();

            return Json(new
            {
                success = true,
                period,
                year = nam,
                range = khoang,
                labels = nhan,
                enrollments = luotDangKy,
                revenue = doanhThu,
                totalEnrollments = luotDangKy.Sum(),
                totalRevenue = doanhThu.Sum(),
                topCourses = topKhoaHoc
            }, JsonRequestBehavior.AllowGet);
        }

        private Dictionary<int, string> TenGiaoVien(List<DongBanHang> banHang)
        {
            var maGv = banHang.Select(l => l.MaGiaoVien).Distinct().ToList();
            return db.NguoiDung.Where(n => maGv.Contains(n.maNguoiDung)).ToDictionary(n => n.maNguoiDung, n => n.hoTen);
        }

        // GET: Admin/Dashboard/TopTeachers?period=month|quarter|year|all&year=2026&index=10&category=CAT-001&top=5
        // Bộ lọc riêng của card "Top giáo viên": một tháng / một quý / một năm cụ thể (hoặc toàn thời gian), theo danh mục, top 5 hoặc 10.
        [HttpGet]
        public JsonResult TopTeachers(string period = "month", int? year = null, int? index = null, string category = null, int top = 5)
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            int nam = Math.Max(2000, Math.Min(year ?? DateTime.Now.Year, DateTime.Now.Year + 1));
            DateTime tu, denTruoc;
            string khoang;
            if (period == "all")
            {
                tu = new DateTime(2000, 1, 1); denTruoc = DateTime.Now.AddDays(1); khoang = "toàn thời gian";
            }
            else if (period == "year")
            {
                tu = new DateTime(nam, 1, 1); denTruoc = tu.AddYears(1); khoang = "năm " + nam;
            }
            else if (period == "quarter")
            {
                int q = Math.Max(1, Math.Min(index ?? ((DateTime.Now.Month - 1) / 3 + 1), 4));
                tu = new DateTime(nam, (q - 1) * 3 + 1, 1); denTruoc = tu.AddMonths(3); khoang = "quý " + q + "/" + nam;
            }
            else
            {
                period = "month";
                int m = Math.Max(1, Math.Min(index ?? DateTime.Now.Month, 12));
                tu = new DateTime(nam, m, 1); denTruoc = tu.AddMonths(1); khoang = "tháng " + m + "/" + nam;
            }
            top = top == 10 ? 10 : 5;

            var banHang = LayDongBanHang(tu, denTruoc);
            string tenDanhMuc = null;
            if (!string.IsNullOrEmpty(category))
            {
                banHang = banHang.Where(l => l.MaDanhMuc == category).ToList();
                tenDanhMuc = db.DanhMuc.Where(c => c.maDanhMuc == category).Select(c => c.tenDanhMuc).FirstOrDefault();
            }
            var tenGv = TenGiaoVien(banHang);
            var ds = banHang.GroupBy(l => l.MaGiaoVien)
                .Select(g => new
                {
                    id = g.Key,
                    name = tenGv.ContainsKey(g.Key) ? tenGv[g.Key] : "Giáo viên #" + g.Key,
                    courses = g.Select(l => l.MaKhoaHoc).Distinct().Count(),
                    sold = g.Count(),
                    revenue = g.Sum(l => l.TienThucThu),
                    url = Url.Action("Details", "Teacher", new { area = "Admin", id = g.Key })
                })
                .OrderByDescending(x => x.revenue).ThenByDescending(x => x.sold).Take(top).ToList();

            return Json(new
            {
                success = true,
                range = khoang + (tenDanhMuc != null ? " · " + tenDanhMuc : ""),
                top,
                teachers = ds
            }, JsonRequestBehavior.AllowGet);
        }

        // GET: Admin/Dashboard/PendingCounts (menu tự làm mới chấm đỏ khi Admin đang mở trang)
        [HttpGet]
        public JsonResult PendingCounts()
        {
            Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
            var dem = new ECOLearning.Services.AdminPendingService(db).Dem();
            return Json(new { success = true, courses = dem.KhoaHoc, teachers = dem.GiaoVien, specialties = dem.ChuyenMon, violations = dem.ViPham, payouts = dem.ChiTra },
                JsonRequestBehavior.AllowGet);
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
