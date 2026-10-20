using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    /// <summary>Các thành phần điều hướng dùng chung trong _Layout (child action).</summary>
    public class NavigationController : Controller
    {
        private const int SoKhoaHocMoiCapDo = 8;

        // Cùng điều kiện "đang bán" với trang Course/Index để số lượng khớp với trang danh sách
        private static readonly string[] TrangThaiDangBan = { "Đã xuất bản", "Đã duyệt", "Active" };

        private static readonly Dictionary<string, int> ThuTuCapDo = new Dictionary<string, int>
        {
            { "Cơ bản", 1 }, { "Trung cấp", 2 }, { "Nâng cao", 3 }
        };

        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // Menu "Khám phá" 3 tầng: Danh mục → Cấp độ → Khóa học. Danh mục tạm ngưng bị ẩn như ở bộ lọc.
        // Không cache: danh mục / khóa học mới hiện ngay ở lần tải trang kế tiếp.
        [ChildActionOnly]
        public ActionResult ExploreMenu()
        {
            var model = new List<ExploreCategoryViewModel>();
            try
            {
                var danhMuc = db.DanhMuc
                    .Where(c => c.trangThai == null || c.trangThai == true)
                    .Select(c => new { c.maDanhMuc, c.tenDanhMuc })
                    .ToList();

                // Bỏ khóa người dùng đã sở hữu (đã mua / đăng ký 0đ) như trang chủ và trang Course:
                // khóa đã sở hữu chỉ hiện ở "Khóa học của tôi"
                int userId = (Session["UserId"] as int?) ?? 0;
                var khoaHoc = db.KhoaHoc
                    .Where(k => TrangThaiDangBan.Contains(k.trangThai))
                    .Where(k => !k.DangKyKhoaHoc.Any(d => d.maHocVien == userId))
                    .Select(k => new { k.maKhoaHoc, k.tenKhoaHoc, k.maDanhMuc, k.capDo, SoHocVien = k.DangKyKhoaHoc.Count() })
                    .ToList();

                model = danhMuc
                    .Select(c =>
                    {
                        var khoaTrongDanhMuc = khoaHoc.Where(k => k.maDanhMuc == c.maDanhMuc).ToList();
                        return new ExploreCategoryViewModel
                        {
                            MaDanhMuc = c.maDanhMuc,
                            TenDanhMuc = c.tenDanhMuc,
                            SoKhoaHoc = khoaTrongDanhMuc.Count,
                            CapDo = khoaTrongDanhMuc
                                .GroupBy(k => string.IsNullOrWhiteSpace(k.capDo) ? null : k.capDo.Trim())
                                .OrderBy(g => g.Key != null && ThuTuCapDo.ContainsKey(g.Key) ? ThuTuCapDo[g.Key] : 99)
                                .ThenBy(g => g.Key)
                                .Select(g => new ExploreLevelViewModel
                                {
                                    CapDo = g.Key,
                                    SoKhoaHoc = g.Count(),
                                    KhoaHoc = g.OrderByDescending(k => k.SoHocVien)
                                        .ThenBy(k => k.tenKhoaHoc)
                                        .Take(SoKhoaHocMoiCapDo)
                                        .Select(k => new ExploreCourseViewModel { MaKhoaHoc = k.maKhoaHoc, TenKhoaHoc = k.tenKhoaHoc })
                                        .ToList()
                                })
                                .ToList()
                        };
                    })
                    .OrderByDescending(c => c.SoKhoaHoc)
                    .ThenBy(c => c.TenDanhMuc)
                    .ToList();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Explore menu error: " + ex.Message);
            }

            return PartialView("_ExploreMenu", model);
        }

        // Menu "Khóa học của tôi" 2 tầng như "Khám phá": Khóa học đã đăng ký / Khóa học đã tim → các khóa gần đây
        [ChildActionOnly]
        public ActionResult MyCoursesMenu()
        {
            return PartialView("_MyCoursesMenu", BuildMyCoursesMenu());
        }

        // GET: Navigation/MyCoursesMenuPanel — tải lại bảng "Khóa học của tôi" sau khi thả / bỏ tim (assets/js/favorite.js)
        [HttpGet]
        [OutputCache(NoStore = true, Duration = 0)]
        public ActionResult MyCoursesMenuPanel()
        {
            if ((Session["UserId"] as int?).GetValueOrDefault() <= 0)
            {
                return new HttpStatusCodeResult(401);
            }
            return PartialView("_MyCoursesMenu", BuildMyCoursesMenu());
        }

        private MyCoursesMenuViewModel BuildMyCoursesMenu()
        {
            var model = new MyCoursesMenuViewModel();
            int userId = (Session["UserId"] as int?) ?? 0;
            if (userId > 0)
            {
                try
                {
                    var daDangKy = db.DangKyKhoaHoc.Where(d => d.maHocVien == userId);
                    model.SoKhoaDaDangKy = daDangKy.Count();
                    model.KhoaDaDangKy = daDangKy
                        .OrderByDescending(d => d.ngayDangKy)
                        .Take(SoKhoaHocMoiCapDo)
                        .Select(d => new ExploreCourseViewModel { MaKhoaHoc = d.maKhoaHoc, TenKhoaHoc = d.KhoaHoc.tenKhoaHoc })
                        .ToList();

                    var favorites = new ECOLearning.Services.FavoriteService(db);
                    model.SoKhoaDaTim = favorites.CountFavorites(userId);
                    model.KhoaDaTim = favorites.GetFavoriteMenuItems(userId, SoKhoaHocMoiCapDo);
                }
                catch (System.Exception ex)
                {
                    // Chưa chạy migration 005: menu vẫn hiện, nhóm "đã tim" để trống
                    System.Diagnostics.Debug.WriteLine("My courses menu error: " + ex.Message);
                }
            }
            return model;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
