using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.UI;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    /// <summary>
    /// Tìm kiếm khóa học, mở cho cả khách chưa đăng nhập (Services/SearchService.cs):
    ///   GET /tim-kiem?q=...            trang kết quả (lọc, sắp xếp, phân trang)
    ///   GET /Search/Suggest?q=...      JSON cho dropdown khi gõ: tối đa 7 từ khóa, 5 khóa học chưa sở hữu, 2 giáo viên
    /// </summary>
    public class SearchController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private int UserId => (Session["UserId"] as int?) ?? 0;

        // GET: /tim-kiem?q=&danhMuc=&capDo=&gia=&diem=&sapXep=&trang=
        // ValidateInput(false): cho phép tìm "<div>", "a<b"... (ASP.NET mặc định báo lỗi). Mọi chỗ xuất q ra trang đều được Razor encode.
        [ValidateInput(false)]
        public ActionResult Index(string q, string danhMuc, string capDo, string gia, double? diem, string sapXep, int trang = 1)
        {
            var service = new SearchService(db);
            var vm = service.TimKiem(new TimKiemViewModel
            {
                Q = q,
                DanhMuc = danhMuc,
                CapDo = capDo,
                Gia = gia,
                Diem = diem,
                SapXep = sapXep,
                Trang = trang
            }, UserId);

            // Thống kê từ khóa (gợi ý cho người sau): chỉ lần mở trang đầu, không tính lúc đổi trang / bộ lọc.
            // Kết quả chỉ khớp một phần tính là 0 để câu đó không được gợi ý lại.
            if (vm.Q.Length > 0 && trang <= 1 && !vm.CoBoLoc)
            {
                service.GhiNhanTimKiem(vm.Q, vm.KhopMotPhan ? 0 : vm.TongSo, Session);
            }
            return View(vm);
        }

        // GET: /Search/Suggest?q=...
        [HttpGet]
        [ValidateInput(false)]
        [OutputCache(Location = OutputCacheLocation.None, NoStore = true)]
        public JsonResult Suggest(string q)
        {
            var vm = new GoiYTimKiemViewModel();
            if (string.IsNullOrWhiteSpace(q) || q.Length > SearchService.DoDaiToiDa * 2)
            {
                return Json(vm, JsonRequestBehavior.AllowGet);
            }

            var kq = new SearchService(db).GoiY(q, UserId);
            vm.TuKhoa = kq.TuKhoa.Select(t => new GoiYTuKhoa { Text = t, Url = Url.Action("Index", "Search", new { q = t }) }).ToList();
            vm.KhoaHoc = kq.KhoaHoc.Select(k => new GoiYKhoaHoc
            {
                Ten = k.TenKhoaHoc,
                GiaoVien = k.TenGiaoVien,
                Anh = Anh(k.AnhBia),
                Url = Url.Action("Details", "Course", new { id = k.MaKhoaHoc })
            }).ToList();
            vm.GiaoVien = kq.GiaoVien.Select(g => new GoiYGiaoVien
            {
                Ten = g.HoTen,
                Anh = Anh(g.AnhDaiDien),
                SoKhoaHoc = g.SoKhoaHoc,
                Url = Url.Action("Index", "User", new { id = g.MaGiaoVien })
            }).ToList();
            return Json(vm, JsonRequestBehavior.AllowGet);
        }

        private string Anh(string duongDan)
        {
            if (string.IsNullOrWhiteSpace(duongDan)) return null;
            return duongDan.StartsWith("~", StringComparison.Ordinal) ? Url.Content(duongDan) : duongDan;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && db != null)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
