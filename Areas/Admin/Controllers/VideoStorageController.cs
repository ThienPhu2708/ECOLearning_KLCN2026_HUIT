using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Admin.Controllers
{
    /// <summary>
    /// Thao tác của card "Lưu trữ video" (nằm trong trang Báo cáo hệ thống): chuyển video cũ trên máy chủ lên R2,
    /// thử lại video lỗi, tải câu lệnh SQL cho hosting. Trang Admin/VideoStorage cũ chuyển về Báo cáo hệ thống.
    /// </summary>
    public class VideoStorageController : AdminBaseController
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Admin/VideoStorage
        public ActionResult Index()
        {
            return VeBaoCao();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChuyenVideoCu()
        {
            string ketQua = VideoStorageService.ChuyenVideoCu(db);
            TempData[R2Storage.DaCauHinh ? "SuccessMessage" : "ErrorMessage"] = ketQua;
            AppLog.Ghi("VIDEO", "Admin " + Session["UserId"] + " chuyển video cũ lên R2: " + ketQua);
            return VeBaoCao();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ThuLai()
        {
            int n = VideoStorageService.ThuLai();
            TempData["SuccessMessage"] = n > 0 ? "Đã đưa lại " + n + " video vào hàng đợi." : "Không có video nào bị lỗi.";
            return VeBaoCao();
        }

        // GET: Admin/VideoStorage/SqlChuyenDoi — câu lệnh UPDATE để chạy trên CSDL hosting
        public ActionResult SqlChuyenDoi()
        {
            string file = VideoStorageService.DuongDanFileSql;
            if (!System.IO.File.Exists(file)) return HttpNotFound();
            return File(System.IO.File.ReadAllBytes(file), "application/sql", "r2-migration.sql");
        }

        private ActionResult VeBaoCao()
        {
            return Redirect(Url.Action("Index", "SystemReport", new { area = "Admin" }) + "#luu-tru-video");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
