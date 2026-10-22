using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using ECOLearning.Areas.Marketing.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Areas.Marketing.Controllers
{
    /// <summary>
    /// UC18: Báo cáo hiệu quả Marketing theo khoảng ngày (đặc tả mục 11). Marketing chỉ xem doanh thu của các đơn có dùng mã
    /// (doanh thu toàn sàn ở Admin / Báo cáo doanh thu). Quy tắc: chỉ đơn đã thanh toán, theo ngày thanh toán; mỗi đơn tối đa
    /// một mã nên lượt dùng = số đơn; doanh thu = tongTien sau giảm; ROI chiến dịch = (Doanh thu − Tổng chi phí) / Tổng chi phí,
    /// Tổng chi phí = chi phí thực tế + tiền giảm trong kỳ.
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
            sb.AppendLine("Chỉ số,Giá trị");
            sb.AppendLine("Lượt dùng mã," + m.SoLuotDung);
            sb.AppendLine("Số học viên dùng mã," + m.SoHocVien);
            sb.AppendLine("Lượt dùng ưu đãi thân thiết," + m.SoLuotThanThiet);
            sb.AppendLine("Doanh thu từ đơn có mã (VNĐ)," + m.DoanhThuSauGiam.ToString("F0"));
            sb.AppendLine("Tổng tiền đã giảm (VNĐ)," + m.TienGiam.ToString("F0"));
            sb.AppendLine("Giá trị đơn trung bình (VNĐ)," + m.GiaTriDonTrungBinh.ToString("F0"));
            sb.AppendLine("Tỷ lệ đơn có dùng mã (%)," + m.TyLeDonCoMa.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            sb.AppendLine();
            sb.AppendLine("THEO CHIẾN DỊCH");
            sb.AppendLine("Chiến dịch,Trạng thái,Lượt dùng,Tiền giảm (VNĐ),Doanh thu (VNĐ),Chi phí thực tế (VNĐ),% đạt chỉ tiêu,ROI (%)");
            foreach (var c in m.ChienDichRows)
            {
                sb.AppendLine(string.Join(",", Csv(c.TenChienDich), Csv(c.TrangThai), c.SoLuotDung, c.TienGiam.ToString("F0"), c.DoanhThu.ToString("F0"),
                    c.ChiPhiThucTe.ToString("F0"), SoLe(c.PhanTramDatChiTieu), SoLe(c.Roi)));
            }
            sb.AppendLine();
            sb.AppendLine("THEO KHUYẾN MÃI");
            sb.AppendLine("Chương trình,Mã coupon,Chiến dịch,Ưu đãi,Trạng thái,Lượt dùng,Doanh thu trước giảm (VNĐ),Tiền giảm (VNĐ),Doanh thu sau giảm (VNĐ)");
            foreach (var r in m.Rows)
            {
                sb.AppendLine(string.Join(",", Csv(r.TenKhuyenMai), Csv(r.MaCode), Csv(r.TenChienDich), Csv(r.MoTaUuDai), Csv(r.TrangThai),
                    r.SoLuotDung, r.DoanhThuTruocGiam.ToString("F0"), r.TienGiam.ToString("F0"), r.DoanhThuSauGiam.ToString("F0")));
            }
            sb.AppendLine();
            sb.AppendLine("CHI TIẾT LƯỢT DÙNG");
            sb.AppendLine("Ngày thanh toán,Đơn hàng,Học viên,Email,Mã coupon,Tiền giảm (VNĐ),Tổng đơn (VNĐ)");
            foreach (var l in m.ChiTiet)
            {
                sb.AppendLine(string.Join(",", l.NgaySuDung.ToString("dd/MM/yyyy HH:mm"), Csv(l.MaDonHang), Csv(l.HoTen), Csv(l.Email), Csv(l.MaCode),
                    l.TienGiam.ToString("F0"), l.TongDonHang.ToString("F0")));
            }

            string fileName = "BaoCaoMarketing_" + m.TuNgay.ToString("yyyyMMdd") + "_" + m.DenNgay.ToString("yyyyMMdd") + ".csv";
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", fileName);
        }

        private static string Csv(string s)
        {
            return "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        }

        private static string SoLe(decimal? v)
        {
            return v.HasValue ? v.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) : "";
        }

        /// <summary>Lượt dùng mã (đơn đã thanh toán) có ngày thanh toán trong [tu, denSau).</summary>
        public static List<MarketingUsageRow> LayLuotDung(ECOLearning.Models.QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, DateTime tu, DateTime denSau,
            string maKhuyenMai = null, string maChienDich = null)
        {
            var thamSo = new List<SqlParameter> { new SqlParameter("@tu", tu), new SqlParameter("@denSau", denSau) };
            string loc = "";
            if (maKhuyenMai != null) { loc += " AND s.maKhuyenMai = @km"; thamSo.Add(new SqlParameter("@km", maKhuyenMai)); }
            if (maChienDich != null) { loc += " AND km.maChienDich = @cd"; thamSo.Add(new SqlParameter("@cd", maChienDich)); }
            return db.Database.SqlQuery<MarketingUsageRow>(@"
                SELECT s.maDonHang AS MaDonHang, s.maKhuyenMai AS MaKhuyenMai, km.maChienDich AS MaChienDich, mg.maCode AS MaCode,
                       s.maHocVien AS MaHocVien, nd.hoTen AS HoTen, nd.email AS Email,
                       CAST(CASE WHEN km.doiTuong = N'ThanThiet' THEN 1 ELSE 0 END AS BIT) AS LaThanThiet,
                       ISNULL(d.ngayThanhToan, s.ngaySuDung) AS NgaySuDung, s.tienGiam AS TienGiam, d.tongTien AS TongDonHang
                FROM dbo.SuDungUuDai s
                INNER JOIN dbo.DonHang d ON d.maDonHang = s.maDonHang AND d.trangThai IN (" + PromotionService.DonDaTraSql + @")
                INNER JOIN dbo.KhuyenMai km ON km.maKhuyenMai = s.maKhuyenMai
                LEFT JOIN dbo.MaGiamGia mg ON mg.maGiamGia = s.maGiamGia
                LEFT JOIN dbo.NguoiDung nd ON nd.maNguoiDung = s.maHocVien
                WHERE ISNULL(d.ngayThanhToan, s.ngaySuDung) >= @tu AND ISNULL(d.ngayThanhToan, s.ngaySuDung) < @denSau" + loc + @"
                ORDER BY NgaySuDung DESC", thamSo.ToArray()).ToList();
        }

        private MarketingReportViewModel LapBaoCao(string tuNgay, string denNgay, string khuyenMai, string chienDich)
        {
            var today = DateTime.Today;
            var model = new MarketingReportViewModel
            {
                TuNgay = DocNgay(tuNgay) ?? today.AddDays(-29),
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
            DateTime denSau = model.DenNgay.AddDays(1);
            var luot = LayLuotDung(db, model.TuNgay, denSau, model.MaKhuyenMai, model.MaChienDich);
            model.ChiTiet = luot;

            // KPI
            model.SoLuotDung = luot.Count;
            model.SoHocVien = luot.Select(l => l.MaHocVien).Distinct().Count();
            model.SoLuotThanThiet = luot.Count(l => l.LaThanThiet);
            model.TienGiam = luot.Sum(l => l.TienGiam);
            model.DoanhThuSauGiam = luot.Sum(l => l.TongDonHang);
            model.TongDonDaThanhToan = db.Database.SqlQuery<int>(@"
                SELECT COUNT(*) FROM dbo.DonHang d
                WHERE d.trangThai IN (" + PromotionService.DonDaTraSql + @")
                  AND ISNULL(d.ngayThanhToan, d.ngayDat) >= @tu AND ISNULL(d.ngayThanhToan, d.ngayDat) < @denSau",
                new SqlParameter("@tu", model.TuNgay), new SqlParameter("@denSau", denSau)).Single();

            // Theo khuyến mãi: chương trình có thời gian giao với kỳ + chương trình có lượt dùng trong kỳ
            var chuongTrinh = promotions.LayDanhSach(tuNgay: model.TuNgay, denNgay: model.DenNgay, maChienDich: model.MaChienDich);
            if (model.MaKhuyenMai != null) chuongTrinh = chuongTrinh.Where(p => p.MaKhuyenMai == model.MaKhuyenMai).ToList();
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
                    LaThanThiet = p.LaThanThiet,
                    SoLuotDung = ds.Count,
                    TienGiam = ds.Sum(l => l.TienGiam),
                    DoanhThuSauGiam = ds.Sum(l => l.TongDonHang),
                    DoanhThuTruocGiam = ds.Sum(l => l.TongDonHang + l.TienGiam)
                });
            }
            model.Rows = model.Rows.OrderByDescending(r => r.SoLuotDung).ThenBy(r => r.TenKhuyenMai).ToList();
            model.SoChuongTrinh = model.Rows.Count;

            // Theo chiến dịch (chỉ lọc theo chương trình thì bỏ bảng này)
            if (model.MaKhuyenMai == null)
            {
                var theoChienDich = luot.Where(l => l.MaChienDich != null).GroupBy(l => l.MaChienDich).ToDictionary(g => g.Key, g => g.ToList());
                var chienDichs = new CampaignService(db).LayDanhSach(tuNgay: model.TuNgay, denNgay: model.DenNgay);
                foreach (var ma in theoChienDich.Keys.Where(ma => chienDichs.All(c => c.MaChienDich != ma)))
                {
                    var c = new CampaignService(db).LayTheoMa(ma);
                    if (c != null) chienDichs.Add(c);
                }
                if (model.MaChienDich != null) chienDichs = chienDichs.Where(c => c.MaChienDich == model.MaChienDich).ToList();
                foreach (var c in chienDichs)
                {
                    List<MarketingUsageRow> ds;
                    theoChienDich.TryGetValue(c.MaChienDich, out ds);
                    ds = ds ?? new List<MarketingUsageRow>();
                    model.ChienDichRows.Add(new MarketingCampaignRow
                    {
                        MaChienDich = c.MaChienDich,
                        TenChienDich = c.TenChienDich,
                        TrangThai = c.TrangThaiHienThi,
                        SoLuotDung = ds.Count,
                        TienGiam = ds.Sum(l => l.TienGiam),
                        DoanhThu = ds.Sum(l => l.TongDonHang),
                        ChiPhiThucTe = c.ChiPhiThucTe,
                        ChiTieuDoanhThu = c.ChiTieuDoanhThu
                    });
                }
                model.ChienDichRows = model.ChienDichRows.OrderByDescending(r => r.DoanhThu).ThenBy(r => r.TenChienDich).ToList();
            }

            LapBieuDo(model, luot);
            return model;
        }

        /// <summary>Lượt dùng và doanh thu theo ngày (≤ 62 ngày), theo tháng (≤ 2 năm) hoặc theo năm.</summary>
        private static void LapBieuDo(MarketingReportViewModel model, List<MarketingUsageRow> luot)
        {
            double soNgay = (model.DenNgay - model.TuNgay).TotalDays;
            if (soNgay <= 62)
            {
                model.ChartDonVi = "ngày";
                for (var d = model.TuNgay; d <= model.DenNgay; d = d.AddDays(1))
                {
                    var ds = luot.Where(l => l.NgaySuDung.Date == d).ToList();
                    model.ChartLabels.Add(d.ToString("dd/MM"));
                    model.ChartData.Add(ds.Count);
                    model.ChartDoanhThu.Add(ds.Sum(l => l.TongDonHang));
                }
            }
            else if (soNgay <= 731)
            {
                model.ChartDonVi = "tháng";
                for (var t = new DateTime(model.TuNgay.Year, model.TuNgay.Month, 1); t <= model.DenNgay; t = t.AddMonths(1))
                {
                    var ds = luot.Where(l => l.NgaySuDung.Year == t.Year && l.NgaySuDung.Month == t.Month).ToList();
                    model.ChartLabels.Add(t.ToString("MM/yyyy"));
                    model.ChartData.Add(ds.Count);
                    model.ChartDoanhThu.Add(ds.Sum(l => l.TongDonHang));
                }
            }
            else
            {
                model.ChartDonVi = "năm";
                for (int y = model.TuNgay.Year; y <= model.DenNgay.Year; y++)
                {
                    var ds = luot.Where(l => l.NgaySuDung.Year == y).ToList();
                    model.ChartLabels.Add(y.ToString());
                    model.ChartData.Add(ds.Count);
                    model.ChartDoanhThu.Add(ds.Sum(l => l.TongDonHang));
                }
            }
        }
    }
}
