using System;
using System.Globalization;
using System.Text;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Areas.Admin.Controllers
{
    /// <summary>
    /// Báo cáo doanh thu tổng thể theo khoảng ngày (chỉ Admin). Quy tắc tính ở RevenueService:
    /// đơn "Đã thanh toán", lọc theo ngày đặt đơn, lấy trọn ngày kết thúc, thực thu = tổng đơn giá - tiền giảm coupon.
    /// </summary>
    public class RevenueReportController : AdminBaseController
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Admin/RevenueReport?tuNgay=yyyy-MM-dd&denNgay=yyyy-MM-dd
        public ActionResult Index(string tuNgay = null, string denNgay = null)
        {
            ViewBag.BreadcrumbCurrent = "Doanh thu theo ngày";
            return View(LapBaoCao(tuNgay, denNgay));
        }

        // GET: Admin/RevenueReport/ExportCsv
        public ActionResult ExportCsv(string tuNgay = null, string denNgay = null)
        {
            var m = LapBaoCao(tuNgay, denNgay);
            if (m.ThongBaoLoi != null)
            {
                return RedirectToAction("Index");
            }

            var sb = new StringBuilder();
            sb.Append('﻿');
            sb.AppendLine("BÁO CÁO DOANH THU THEO KHOẢNG NGÀY");
            sb.AppendLine("Từ ngày " + m.TuNgay.ToString("dd/MM/yyyy") + " đến ngày " + m.DenNgay.ToString("dd/MM/yyyy") + " (theo ngày đặt đơn, chỉ đơn đã thanh toán)");
            sb.AppendLine("Thời gian xuất: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("Mã đơn,Ngày đặt,Học viên,Email,Số khóa,Trước giảm (VNĐ),Giảm (VNĐ),Thực thu (VNĐ),Mã coupon");
            foreach (var d in m.DonHang)
            {
                sb.AppendLine(string.Join(",", d.MaDonHang, d.NgayDat.ToString("dd/MM/yyyy HH:mm"), Csv(d.HoTen), Csv(d.Email), d.SoKhoaHoc,
                    d.TienTruocGiam.ToString("F0"), d.TienGiam.ToString("F0"), d.ThucThu.ToString("F0"), Csv(d.MaCode)));
            }
            sb.AppendLine();
            sb.AppendLine(string.Join(",", "Tổng cộng", "", "", "", m.SoDonHang + " đơn",
                m.TienTruocGiam.ToString("F0"), m.TienGiam.ToString("F0"), m.ThucThu.ToString("F0"), ""));

            string fileName = "DoanhThu_" + m.TuNgay.ToString("yyyyMMdd") + "_" + m.DenNgay.ToString("yyyyMMdd") + ".csv";
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", fileName);
        }

        private static string Csv(string s)
        {
            return "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        }

        private RevenueReportViewModel LapBaoCao(string tuNgay, string denNgay)
        {
            var today = DateTime.Today;
            DateTime tu = DocNgay(tuNgay) ?? new DateTime(today.Year, today.Month, 1);
            DateTime den = DocNgay(denNgay) ?? today;
            if (tu > den)
            {
                return new RevenueReportViewModel { TuNgay = tu, DenNgay = den, ThongBaoLoi = "Ngày bắt đầu không được lớn hơn ngày kết thúc." };
            }
            if ((den - tu).TotalDays > 366)
            {
                return new RevenueReportViewModel { TuNgay = tu, DenNgay = den, ThongBaoLoi = "Khoảng ngày tối đa là 1 năm." };
            }
            return new RevenueService(db).LapBaoCao(tu, den);
        }

        private static DateTime? DocNgay(string value)
        {
            DateTime d;
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d : (DateTime?)null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
