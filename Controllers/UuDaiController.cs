using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Controllers
{
    /// <summary>
    /// Trang "Ưu đãi" mở ra khi bấm ảnh quảng cáo popup: liệt kê các chương trình của một chiến dịch (hoặc một chương trình),
    /// mã để lưu vào ví và các khóa học được áp dụng. Công khai, không cần đăng nhập (lưu mã thì cần).
    /// </summary>
    public class UuDaiController : Controller
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private int UserId => (Session["UserId"] as int?) ?? 0;

        // GET: /UuDai/ChienDich/CD...
        public ActionResult ChienDich(string id)
        {
            return HienThi(QuangCaoService.LoaiChienDich, id);
        }

        // GET: /UuDai/ChuongTrinh/KM...
        public ActionResult ChuongTrinh(string id)
        {
            return HienThi(QuangCaoService.LoaiChuongTrinh, id);
        }

        private ActionResult HienThi(string loai, string id)
        {
            string vaiTro = Session["Role"] as string;
            int hocVien = vaiTro == "HocVien" || vaiTro == "GiaoVien" ? UserId : 0;
            var trang = new QuangCaoService(db).LayTrang(loai, id, hocVien);
            if (trang == null)
            {
                Response.StatusCode = 404;
                return View("HetHan");
            }
            return View("Index", trang);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
