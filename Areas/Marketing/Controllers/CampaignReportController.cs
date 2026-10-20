using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using ECOLearning.Areas.Marketing.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Marketing.Controllers
{
    /// <summary>
    /// UC18: Báo cáo hiệu quả Marketing theo khoảng ngày. Marketing chỉ xem doanh thu của các đơn có dùng ưu đãi
    /// (không xem doanh thu tổng thể hệ thống; doanh thu tổng thể ở Admin / Báo cáo doanh thu theo ngày).
    /// Quy tắc: chỉ đơn "Đã thanh toán"; mỗi đơn một ưu đãi nên không đếm trùng; phân biệt doanh thu trước giảm,
    /// tiền giảm và doanh thu sau giảm (= DonHang.tongTien).
    /// </summary>
    public class CampaignReportController : MarketingBaseController
    {
        // GET: Marketing/CampaignReport
        public ActionResult Index(string tuNgay = null, string denNgay = null, string khuyenMai = null, string chienDich = null)
        {
            if (!new PromotionService(db).IsInstalled()) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            var model = LapBaoCao(tuNgay, denNgay, khuyenMai, chienDich);
            ViewBag.ChuongTrinh = new PromotionService(db).LayDanhSach();
            ViewBag.ChienDich = new CampaignService(db).LayDanhSach();
            ViewBag.BreadcrumbCurrent = "Báo cáo Marketing";
            return View(model);
        }

        // GET: Marketing/CampaignReport/ExportCsv (cùng bộ lọc với trang báo cáo)
        public ActionResult ExportCsv(string tuNgay = null, string denNgay = null, string khuyenMai = null, string chienDich = null)
        {
            if (!new PromotionService(db).IsInstalled()) return View("~/Areas/Marketing/Views/Shared/_ChuaKhoiTao.cshtml");
            var m = LapBaoCao(tuNgay, denNgay, khuyenMai, chienDich);
            if (m.ThongBaoLoi != null)
            {
                TempData["Error"] = m.ThongBaoLoi;
                return RedirectToAction("Index");
            }

            var sb = new StringBuilder();
            sb.Append('﻿');
            sb.AppendLine("BÁO CÁO HIỆU QUẢ MARKETING");
            sb.AppendLine("Khoảng thời gian: " + m.TuNgay.ToString("dd/MM/yyyy") + " - " + m.DenNgay.ToString("dd/MM/yyyy"));
            sb.AppendLine("Thời gian xuất: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("Chương trình,Mã coupon,Chiến dịch,Ưu đãi,Trạng thái,Lượt dùng,Doanh thu trước giảm (VNĐ),Tiền giảm (VNĐ),Doanh thu sau giảm (VNĐ)");
            foreach (var r in m.Rows)
            {
                sb.AppendLine(string.Join(",", Csv(r.TenKhuyenMai), Csv(r.MaCode), Csv(r.TenChienDich), Csv(r.MoTaUuDai), Csv(r.TrangThai),
                    r.SoLuotDung, r.DoanhThuTruocGiam.ToString("F0"), r.TienGiam.ToString("F0"), r.DoanhThuSauGiam.ToString("F0")));
            }
            sb.AppendLine();
            sb.AppendLine(string.Join(",", "Tổng cộng", "", "", "", "", m.SoLuotDung,
                m.DoanhThuTruocGiam.ToString("F0"), m.TienGiam.ToString("F0"), m.DoanhThuSauGiam.ToString("F0")));

            string fileName = "BaoCaoMarketing_" + m.TuNgay.ToString("yyyyMMdd") + "_" + m.DenNgay.ToString("yyyyMMdd") + ".csv";
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", fileName);
        }

        private static string Csv(string s)
        {
            return "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        }

        private MarketingReportViewModel LapBaoCao(string tuNgay, string denNgay, string khuyenMai, string chienDich)
        {
            var today = DateTime.Today;
            var model = new MarketingReportViewModel
            {
                TuNgay = DocNgay(tuNgay) ?? new DateTime(today.Year, today.Month, 1),
                DenNgay = DocNgay(denNgay) ?? today,
                MaKhuyenMai = string.IsNullOrEmpty(khuyenMai) ? null : khuyenMai,
                MaChienDich = string.IsNullOrEmpty(chienDich) ? null : chienDich
            };
            if (model.DenNgay < model.TuNgay)
            {
                model.ThongBaoLoi = "Ngày bắt đầu không được lớn hơn ngày kết thúc.";
                return model;
            }

            var promotions = new PromotionService(db);
            var now = DateTime.Now;
            var chuongTrinh = promotions.LayDanhSach(tuNgay: model.TuNgay, denNgay: model.DenNgay, maChienDich: model.MaChienDich);
            if (model.MaKhuyenMai != null) chuongTrinh = chuongTrinh.Where(p => p.MaKhuyenMai == model.MaKhuyenMai).ToList();

            // Lượt dùng trong khoảng ngày (trọn ngày kết thúc), chỉ đơn đã thanh toán
            var thamSo = new List<SqlParameter>
            {
                new SqlParameter("@tu", model.TuNgay),
                new SqlParameter("@denSau", model.DenNgay.AddDays(1))
            };
            string loc = "";
            if (model.MaKhuyenMai != null) { loc += " AND s.maKhuyenMai = @km"; thamSo.Add(new SqlParameter("@km", model.MaKhuyenMai)); }
            if (model.MaChienDich != null) { loc += " AND km.maChienDich = @cd"; thamSo.Add(new SqlParameter("@cd", model.MaChienDich)); }
            var luot = db.Database.SqlQuery<MarketingUsageRow>(@"
                SELECT s.maKhuyenMai AS MaKhuyenMai, s.ngaySuDung AS NgaySuDung, s.tienGiam AS TienGiam, d.tongTien AS TongDonHang
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai = N'Đã thanh toán'
                INNER JOIN dbo.KhuyenMai km ON km.maKhuyenMai = s.maKhuyenMai
                WHERE s.ngaySuDung >= @tu AND s.ngaySuDung < @denSau" + loc, thamSo.ToArray()).ToList();

            // Chương trình có lượt dùng trong kỳ nhưng thời gian áp dụng nằm ngoài khoảng lọc vẫn phải có trong bảng
            foreach (var ma in luot.Select(l => l.MaKhuyenMai).Distinct().Where(ma => chuongTrinh.All(p => p.MaKhuyenMai != ma)))
            {
                var p = promotions.LayTheoMa(ma);
                if (p != null) chuongTrinh.Add(p);
            }

            var theoChuongTrinh = luot.GroupBy(l => l.MaKhuyenMai).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var p in chuongTrinh)
            {
                List<MarketingUsageRow> ds;
                theoChuongTrinh.TryGetValue(p.MaKhuyenMai, out ds);
                ds = ds ?? new List<MarketingUsageRow>();
                model.Rows.Add(new MarketingReportRow
                {
                    MaKhuyenMai = p.MaKhuyenMai,
                    TenKhuyenMai = p.TenKhuyenMai,
                    MaCode = p.MaCode,
                    TenChienDich = p.TenChienDich,
                    MoTaUuDai = p.MoTaUuDai,
                    TrangThai = p.TrangThaiHienThi(now),
                    SoLuotDung = ds.Count,
                    TienGiam = ds.Sum(l => l.TienGiam),
                    DoanhThuSauGiam = ds.Sum(l => l.TongDonHang),
                    DoanhThuTruocGiam = ds.Sum(l => l.TongDonHang + l.TienGiam)
                });
            }
            model.Rows = model.Rows.OrderByDescending(r => r.SoLuotDung).ThenBy(r => r.TenKhuyenMai).ToList();

            model.SoChuongTrinh = model.Rows.Count;
            model.SoLuotDung = luot.Count;
            model.SoDonHang = luot.Count; // mỗi đơn chỉ áp dụng một ưu đãi
            model.TienGiam = luot.Sum(l => l.TienGiam);
            model.DoanhThuSauGiam = luot.Sum(l => l.TongDonHang);
            model.DoanhThuTruocGiam = model.DoanhThuSauGiam + model.TienGiam;

            // Biểu đồ lượt dùng: theo ngày, hoặc theo tháng khi khoảng dài
            model.ChartTheoThang = (model.DenNgay - model.TuNgay).TotalDays > 62;
            if (model.ChartTheoThang)
            {
                for (var t = new DateTime(model.TuNgay.Year, model.TuNgay.Month, 1); t <= model.DenNgay; t = t.AddMonths(1))
                {
                    model.ChartLabels.Add(t.ToString("MM/yyyy"));
                    model.ChartData.Add(luot.Count(l => l.NgaySuDung.Year == t.Year && l.NgaySuDung.Month == t.Month));
                }
            }
            else
            {
                for (var d = model.TuNgay; d <= model.DenNgay; d = d.AddDays(1))
                {
                    model.ChartLabels.Add(d.ToString("dd/MM"));
                    model.ChartData.Add(luot.Count(l => l.NgaySuDung.Date == d));
                }
            }
            return model;
        }
    }
}
