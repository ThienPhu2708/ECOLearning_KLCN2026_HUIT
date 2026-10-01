using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Areas.Admin.Models;

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
                    GiaKhuyenMai = k.giaKhuyenMai,
                    SoVideo = videoCount,
                    SoTaiLieu = documentCount,
                    TrangThai = k.trangThai
                };
            }).ToList();

            // 6. Dữ liệu biểu đồ Tăng trưởng học viên mới thực tế qua 12 tháng năm hiện tại
            int currentYear = DateTime.Now.Year;
            model.MonthlyGrowthLabels = new List<string> { "T1", "T2", "T3", "T4", "T5", "T6", "T7", "T8", "T9", "T10", "T11", "T12" };
            model.MonthlyGrowthData = new List<int>();

            var dangKys = db.DangKyKhoaHoc.Where(dk => dk.ngayDangKy.HasValue && dk.ngayDangKy.Value.Year == currentYear).ToList();
            for (int m = 1; m <= 12; m++)
            {
                model.MonthlyGrowthData.Add(dangKys.Count(dk => dk.ngayDangKy.Value.Month == m));
            }

            // 7. Dữ liệu biểu đồ Donut Tỷ trọng danh mục khóa học thực tế từ CSDL
            var categories = db.DanhMuc.ToList();
            int totalCourses = db.KhoaHoc.Count();

            string[] chartColors = new string[] { "#434CE8", "#3B82F6", "#10B981", "#F59E0B", "#8B5CF6", "#EC4899" };
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
