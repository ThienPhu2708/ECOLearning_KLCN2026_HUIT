using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Marketing.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Marketing.Controllers
{
    public class DashboardController : MarketingBaseController
    {
        // GET: Marketing/Dashboard — số liệu thật từ chương trình khuyến mãi và SuDungUuDai (không dùng số mô phỏng)
        public ActionResult Index()
        {
            var model = new MarketingDashboardViewModel();
            ViewBag.BreadcrumbCurrent = "Dashboard Marketing";

            var promotions = new PromotionService(db);
            if (!promotions.IsInstalled())
            {
                model.ChartLabels = Enumerable.Range(1, 12).Select(m => "T" + m).ToList();
                model.CouponUsageChartData = Enumerable.Repeat(0, 12).ToList();
                ViewBag.ChuaKhoiTao = true;
                return View(model);
            }

            var now = DateTime.Now;
            var chuongTrinh = promotions.LayDanhSach();
            model.ActiveCampaignsCount = chuongTrinh.Count(p => p.TrangThaiHienThi(now) == "Đang chạy");
            model.TotalCouponsUsed = chuongTrinh.Sum(p => p.SoLuotDaDung);
            model.DrivenRevenue = chuongTrinh.Sum(p => p.DoanhThuSauGiam);

            // Lượt dùng ưu đãi theo tháng của năm hiện tại (chỉ đơn đã thanh toán)
            var theoThang = db.Database.SqlQuery<ThangRow>(@"
                SELECT MONTH(s.ngaySuDung) AS Thang, COUNT(*) AS SoLuot
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai = N'Đã thanh toán'
                WHERE YEAR(s.ngaySuDung) = YEAR(GETDATE())
                GROUP BY MONTH(s.ngaySuDung)").ToList().ToDictionary(r => r.Thang, r => r.SoLuot);
            model.ChartLabels = Enumerable.Range(1, 12).Select(m => "T" + m).ToList();
            model.CouponUsageChartData = Enumerable.Range(1, 12).Select(m => theoThang.ContainsKey(m) ? theoThang[m] : 0).ToList();

            model.TopCoupons = chuongTrinh
                .OrderByDescending(p => p.SoLuotDaDung)
                .ThenByDescending(p => p.NgayTao)
                .Take(4)
                .Select(p => new CouponPerformanceItem
                {
                    MaCode = p.MaCode,
                    TenKhuyenMai = p.TenKhuyenMai,
                    MucUuDai = p.MucUuDai,
                    MoTaUuDai = p.MoTaUuDai,
                    SoLuongDung = p.SoLuotDaDung,
                    DoanhThu = p.DoanhThuSauGiam
                })
                .ToList();

            return View(model);
        }

        public class ThangRow
        {
            public int Thang { get; set; }
            public int SoLuot { get; set; }
        }
    }
}
