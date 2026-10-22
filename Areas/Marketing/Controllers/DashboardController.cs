using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Marketing.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Areas.Marketing.Controllers
{
    public class DashboardController : MarketingBaseController
    {
        // GET: Marketing/Dashboard — số liệu thật từ chương trình khuyến mãi và SuDungUuDai (đơn đã thanh toán), đặc tả mục 11
        public ActionResult Index()
        {
            var model = new MarketingDashboardViewModel();
            ViewBag.BreadcrumbCurrent = "Dashboard Marketing";
            model.ChartLabels = Enumerable.Range(1, 12).Select(m => "T" + m).ToList();

            var promotions = new PromotionService(db);
            if (!promotions.IsInstalled())
            {
                model.CouponUsageChartData = Enumerable.Repeat(0, 12).ToList();
                model.RevenueChartData = Enumerable.Repeat(0m, 12).ToList();
                ViewBag.ChuaKhoiTao = true;
                return View(model);
            }

            var now = DateTime.Now;
            var chuongTrinh = promotions.LayDanhSach();
            var dangChay = chuongTrinh.Where(p => p.TrangThaiHienThi(now) == "Đang chạy").ToList();
            model.ActiveCampaignsCount = dangChay.Count;

            var chienDich = new CampaignService(db).LayDanhSach(trangThai: TrangThaiChienDich.DangTrienKhai);
            model.ChienDichDangTrienKhai = chienDich.Count;
            model.ChienDichSapKetThuc = chienDich.OrderBy(c => c.NgayKetThuc).Take(5).ToList();

            // Năm nay: lượt dùng và doanh thu theo tháng (ngày thanh toán); tháng này lấy từ cùng dữ liệu
            var dauNam = new DateTime(now.Year, 1, 1);
            var luot = CampaignReportController.LayLuotDung(db, dauNam, dauNam.AddYears(1));
            model.CouponUsageChartData = Enumerable.Range(1, 12).Select(m => luot.Count(l => l.NgaySuDung.Month == m)).ToList();
            model.RevenueChartData = Enumerable.Range(1, 12).Select(m => luot.Where(l => l.NgaySuDung.Month == m).Sum(l => l.TongDonHang)).ToList();
            var thangNay = luot.Where(l => l.NgaySuDung.Month == now.Month).ToList();
            model.TotalCouponsUsed = thangNay.Count;
            model.DrivenRevenue = thangNay.Sum(l => l.TongDonHang);
            model.TienGiamThangNay = thangNay.Sum(l => l.TienGiam);

            model.TopCoupons = chuongTrinh
                .Where(p => p.SoLuotDaDung > 0)
                .OrderByDescending(p => p.SoLuotDaDung)
                .ThenByDescending(p => p.DoanhThuSauGiam)
                .Take(5)
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

            // Mã đang chạy cần chú ý: sắp hết hạn (≤ 7 ngày) hoặc sắp hết lượt (≥ 80%)
            model.MaCanChuY = dangChay
                .Select(p => new
                {
                    p,
                    conNgay = (p.NgayKetThuc.Date - now.Date).Days,
                    tyLe = p.GioiHanLuotDung.HasValue && p.GioiHanLuotDung.Value > 0 ? p.SoLuotDaDung * 100 / p.GioiHanLuotDung.Value : 0
                })
                .Where(x => x.conNgay <= 7 || x.tyLe >= 80)
                .OrderBy(x => Math.Min(x.conNgay, 100 - x.tyLe))
                .Take(5)
                .Select(x => new CanhBaoMa
                {
                    MaKhuyenMai = x.p.MaKhuyenMai,
                    MaCode = x.p.MaCode,
                    TenKhuyenMai = x.p.TenKhuyenMai,
                    LyDo = string.Join(" · ", new List<string>
                    {
                        x.conNgay <= 7 ? (x.conNgay <= 0 ? "Hết hạn hôm nay" : "Hết hạn sau " + x.conNgay + " ngày") : null,
                        x.tyLe >= 80 ? "Đã dùng " + x.p.SoLuotDaDung + "/" + x.p.GioiHanLuotDung + " lượt (" + x.tyLe + "%)" : null
                    }.Where(s => s != null))
                })
                .ToList();

            return View(model);
        }
    }
}
