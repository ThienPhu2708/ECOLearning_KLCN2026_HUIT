using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;

namespace ECOLearning.Areas.Admin.Controllers
{
    /// <summary>
    /// Chi trả doanh thu cho giáo viên (chế độ THỬ NGHIỆM): giáo viên gửi yêu cầu rút ở Instructor/Payout → Admin duyệt
    /// "Chuyển khoản thử nghiệm" (sinh mã giao dịch, không chuyển tiền thật) hoặc từ chối kèm lý do (số tiền trở lại số dư khả dụng).
    /// Cột xử lý thuộc Database/Migrations/014_ThanhToan_ThuNghiem.sql.
    /// </summary>
    public class PayoutRequestController : AdminBaseController
    {
        public const string ChoDuyet = "Chờ duyệt";
        public const string DaChi = "Đã thanh toán";
        public const string TuChoi = "Từ chối";

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private bool DaKhoiTao()
        {
            return db.Database.SqlQuery<int>("SELECT CASE WHEN COL_LENGTH('dbo.NhanThanhToan', 'maGiaoDichChi') IS NULL THEN 0 ELSE 1 END").Single() == 1;
        }

        // GET: Admin/PayoutRequest?status=cho|da-chi|tu-choi|tat-ca
        public ActionResult Index(string status = "cho")
        {
            ViewBag.BreadcrumbCurrent = "Chi trả giáo viên";
            ViewBag.DaKhoiTao = DaKhoiTao();
            if (!(bool)ViewBag.DaKhoiTao) return View(new List<PhieuChiAdmin>());

            string trangThai = status == "da-chi" ? DaChi : status == "tu-choi" ? TuChoi : status == "tat-ca" ? null : ChoDuyet;
            ViewBag.Status = trangThai == null ? "tat-ca" : status == "da-chi" || status == "tu-choi" ? status : "cho";

            var list = db.Database.SqlQuery<PhieuChiAdmin>(@"
                SELECT p.maPhieuChi AS MaPhieuChi, p.maGiaoVien AS MaGiaoVien, nd.hoTen AS TenGiaoVien, nd.email AS Email,
                       p.soTienYeuCau AS SoTien, p.thucNhan AS ThucNhan, p.ngayYeuCau AS NgayYeuCau, ISNULL(p.trangThai, N'Chờ duyệt') AS TrangThai,
                       p.nganHang AS NganHang, p.soTaiKhoan AS SoTaiKhoan, p.tenChuTaiKhoan AS TenChuTaiKhoan,
                       p.ngayXuLy AS NgayXuLy, p.maGiaoDichChi AS MaGiaoDichChi, p.lyDoTuChoi AS LyDoTuChoi, ad.hoTen AS NguoiXuLy
                FROM dbo.NhanThanhToan p
                JOIN dbo.NguoiDung nd ON nd.maNguoiDung = p.maGiaoVien
                LEFT JOIN dbo.NguoiDung ad ON ad.maNguoiDung = p.maAdminXuLy
                WHERE (@tt IS NULL OR ISNULL(p.trangThai, N'Chờ duyệt') = @tt)
                ORDER BY CASE WHEN ISNULL(p.trangThai, N'Chờ duyệt') = N'Chờ duyệt' THEN 0 ELSE 1 END, p.ngayYeuCau DESC",
                new SqlParameter("@tt", (object)trangThai ?? DBNull.Value)).ToList();

            var dem = db.Database.SqlQuery<DemTrangThai>(@"
                SELECT ISNULL(trangThai, N'Chờ duyệt') AS TrangThai, COUNT(*) AS SoLuong, SUM(soTienYeuCau) AS TongTien
                FROM dbo.NhanThanhToan GROUP BY ISNULL(trangThai, N'Chờ duyệt')").ToList();
            ViewBag.Dem = dem.ToDictionary(d => d.TrangThai, d => d);
            return View(list);
        }

        // POST: Admin/PayoutRequest/Approve (chuyển khoản thử nghiệm)
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Approve(string id, string status)
        {
            int adminId = (Session["UserId"] as int?) ?? 0;
            string maGd = "SBXCHI" + DateTime.Now.ToString("yyMMddHHmmss") + new Random().Next(100, 999);
            // Chỉ xử lý phiếu còn "Chờ duyệt" (hai Admin bấm cùng lúc thì chỉ một người xử lý được)
            int n = db.Database.ExecuteSqlCommand(@"
                UPDATE dbo.NhanThanhToan
                SET trangThai = @da, ngayXuLy = GETDATE(), maGiaoDichChi = @gd, maAdminXuLy = @ad, lyDoTuChoi = NULL
                WHERE maPhieuChi = @id AND ISNULL(trangThai, N'Chờ duyệt') = @cho",
                new SqlParameter("@da", DaChi), new SqlParameter("@gd", maGd), new SqlParameter("@ad", adminId),
                new SqlParameter("@id", id ?? ""), new SqlParameter("@cho", ChoDuyet));
            if (n == 1) TempData["SuccessMessage"] = "Đã chuyển khoản thử nghiệm phiếu " + id + ". Mã giao dịch: " + maGd + ".";
            else TempData["ErrorMessage"] = "Phiếu " + id + " không còn ở trạng thái chờ duyệt.";
            return RedirectToAction("Index", new { status });
        }

        // POST: Admin/PayoutRequest/Reject (từ chối, số tiền trở lại số dư khả dụng của giáo viên)
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Reject(string id, string lyDo, string status)
        {
            lyDo = (lyDo ?? "").Trim();
            if (lyDo.Length < 5 || lyDo.Length > 500)
            {
                TempData["ErrorMessage"] = "Nhập lý do từ chối (5–500 ký tự) để giáo viên biết cần điều chỉnh gì.";
                return RedirectToAction("Index", new { status });
            }
            int adminId = (Session["UserId"] as int?) ?? 0;
            int n = db.Database.ExecuteSqlCommand(@"
                UPDATE dbo.NhanThanhToan
                SET trangThai = @tc, ngayXuLy = GETDATE(), lyDoTuChoi = @ld, maAdminXuLy = @ad
                WHERE maPhieuChi = @id AND ISNULL(trangThai, N'Chờ duyệt') = @cho",
                new SqlParameter("@tc", TuChoi), new SqlParameter("@ld", lyDo), new SqlParameter("@ad", adminId),
                new SqlParameter("@id", id ?? ""), new SqlParameter("@cho", ChoDuyet));
            if (n == 1) TempData["SuccessMessage"] = "Đã từ chối phiếu " + id + ". Số tiền đã trở lại số dư khả dụng của giáo viên.";
            else TempData["ErrorMessage"] = "Phiếu " + id + " không còn ở trạng thái chờ duyệt.";
            return RedirectToAction("Index", new { status });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        public class DemTrangThai
        {
            public string TrangThai { get; set; }
            public int SoLuong { get; set; }
            public decimal? TongTien { get; set; }
        }
    }

    public class PhieuChiAdmin
    {
        public string MaPhieuChi { get; set; }
        public int MaGiaoVien { get; set; }
        public string TenGiaoVien { get; set; }
        public string Email { get; set; }
        public decimal SoTien { get; set; }
        public decimal ThucNhan { get; set; }
        public DateTime? NgayYeuCau { get; set; }
        public string TrangThai { get; set; }
        public string NganHang { get; set; }
        public string SoTaiKhoan { get; set; }
        public string TenChuTaiKhoan { get; set; }
        public DateTime? NgayXuLy { get; set; }
        public string MaGiaoDichChi { get; set; }
        public string LyDoTuChoi { get; set; }
        public string NguoiXuLy { get; set; }
    }
}
