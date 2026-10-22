using System;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Admin.Controllers
{
    /// <summary>
    /// Lưu trữ video: trạng thái Cloudflare R2 và ffmpeg, hàng đợi nén / tải lên, chuyển video cũ trên máy chủ lên R2.
    /// </summary>
    public class VideoStorageController : AdminBaseController
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        public class VideoStorageViewModel
        {
            public bool R2DaCauHinh { get; set; }
            public string TenBucket { get; set; }
            public bool FfmpegSanSang { get; set; }
            public string FfmpegThongTin { get; set; }
            public int BaiTrenR2 { get; set; }
            public int BaiTrenMayChu { get; set; }
            public int FileTrenMayChu { get; set; }
            public bool CoFileSql { get; set; }
            public System.Collections.Generic.List<VideoStorageService.CongViecVideo> DangCho { get; set; }
            public System.Collections.Generic.List<VideoStorageService.KetQuaVideo> LichSu { get; set; }
        }

        // GET: Admin/VideoStorage
        public ActionResult Index()
        {
            string ffmpeg;
            bool coFfmpeg = VideoStorageService.KiemTraFfmpeg(out ffmpeg);
            var model = new VideoStorageViewModel
            {
                R2DaCauHinh = R2Storage.DaCauHinh,
                TenBucket = R2Storage.TenBucket,
                FfmpegSanSang = coFfmpeg,
                FfmpegThongTin = ffmpeg,
                BaiTrenR2 = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.BaiHoc WHERE duongDanVideo LIKE N'r2:%'").Single(),
                BaiTrenMayChu = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.BaiHoc WHERE duongDanVideo LIKE N'/assets/uploads/videos/%'").Single(),
                FileTrenMayChu = db.Database.SqlQuery<int>("SELECT COUNT(DISTINCT duongDanVideo) FROM dbo.BaiHoc WHERE duongDanVideo LIKE N'/assets/uploads/videos/%'").Single(),
                CoFileSql = System.IO.File.Exists(VideoStorageService.DuongDanFileSql),
                DangCho = VideoStorageService.DanhSachDangCho(),
                LichSu = VideoStorageService.LichSuGanDay()
            };
            ViewBag.BreadcrumbCurrent = "Lưu trữ video";
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChuyenVideoCu()
        {
            string ketQua = VideoStorageService.ChuyenVideoCu(db);
            TempData[R2Storage.DaCauHinh ? "SuccessMessage" : "ErrorMessage"] = ketQua;
            AppLog.Ghi("VIDEO", "Admin " + Session["UserId"] + " chuyển video cũ lên R2: " + ketQua);
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ThuLai()
        {
            int n = VideoStorageService.ThuLai();
            TempData["SuccessMessage"] = n > 0 ? "Đã đưa lại " + n + " video vào hàng đợi." : "Không có video nào bị lỗi.";
            return RedirectToAction("Index");
        }

        // GET: Admin/VideoStorage/SqlChuyenDoi — câu lệnh UPDATE để chạy trên CSDL hosting
        public ActionResult SqlChuyenDoi()
        {
            string file = VideoStorageService.DuongDanFileSql;
            if (!System.IO.File.Exists(file)) return HttpNotFound();
            return File(System.IO.File.ReadAllBytes(file), "application/sql", "r2-migration.sql");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
