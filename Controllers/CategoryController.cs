using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    /// <summary>
    /// Trang danh mục: MỘT trang cho mọi danh mục (/danh-muc/{slug}, route "DanhMuc" trong RouteConfig).
    /// Slug sinh từ tên danh mục (SlugHelper) nên thêm danh mục mới không phải tạo thêm trang.
    /// </summary>
    public class CategoryController : Controller
    {
        private static readonly string[] TrangThaiDangBan = { "Đã xuất bản", "Đã duyệt", "Active" };

        private static readonly Dictionary<string, int> ThuTuCapDo = new Dictionary<string, int>
        {
            { "Cơ bản", 1 }, { "Trung cấp", 2 }, { "Nâng cao", 3 }
        };

        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: /danh-muc/{slug}?level=...&sort=...
        public ActionResult Index(string slug, string level, string sort)
        {
            if (!User.Identity.IsAuthenticated && Session["UserId"] == null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Request.RawUrl });
            }
            if (string.IsNullOrWhiteSpace(slug))
            {
                return RedirectToAction("Index", "Course");
            }

            // Danh mục đang hoạt động (danh mục tạm ngưng không có trang, giống bộ lọc ở trang Course)
            var danhMuc = db.DanhMuc
                .Where(d => d.trangThai == null || d.trangThai == true)
                .Select(d => new { d.maDanhMuc, d.tenDanhMuc, d.moTa })
                .ToList()
                .Select(d => new { d.maDanhMuc, d.tenDanhMuc, d.moTa, Slug = SlugHelper.ToSlug(d.tenDanhMuc) })
                .ToList();

            string key = slug.Trim().ToLowerInvariant();
            var current = danhMuc.FirstOrDefault(d => d.Slug == key)
                ?? danhMuc.FirstOrDefault(d => string.Equals(d.maDanhMuc, slug.Trim(), StringComparison.OrdinalIgnoreCase));
            if (current == null)
            {
                return HttpNotFound("Không tìm thấy danh mục.");
            }
            // Truy cập bằng mã danh mục (link cũ): chuyển về đường dẫn theo tên. Không dùng 301 vì đổi tên
            // danh mục thì slug đổi theo, trình duyệt nhớ 301 sẽ dẫn sai.
            if (current.Slug != key && !string.IsNullOrEmpty(current.Slug))
            {
                return RedirectToAction("Index", new { slug = current.Slug, level, sort });
            }

            // Khóa đang bán trong danh mục, bỏ khóa người dùng đã sở hữu (như trang chủ và trang Course)
            int userId = (Session["UserId"] as int?) ?? 0;
            var courses = db.KhoaHoc
                .Include(k => k.NguoiDung)
                .Include(k => k.DanhMuc)
                .Where(k => k.maDanhMuc == current.maDanhMuc && TrangThaiDangBan.Contains(k.trangThai))
                .Where(k => !db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == k.maKhoaHoc && d.maHocVien == userId))
                .ToList();

            var ids = courses.Select(k => k.maKhoaHoc).ToList();
            var enrollCounts = db.DangKyKhoaHoc
                .Where(d => ids.Contains(d.maKhoaHoc))
                .GroupBy(d => d.maKhoaHoc)
                .Select(g => new { MaKhoaHoc = g.Key, SoHocVien = g.Count() })
                .ToDictionary(x => x.MaKhoaHoc, x => x.SoHocVien);
            var ratings = db.DanhGiaKhoaHoc
                .Where(d => ids.Contains(d.maKhoaHoc))
                .GroupBy(d => d.maKhoaHoc)
                .Select(g => new { MaKhoaHoc = g.Key, SoDanhGia = g.Count(), Diem = g.Average(d => (double)d.soSao) })
                .ToDictionary(x => x.MaKhoaHoc);

            var items = courses.Select(k =>
            {
                int soHocVien;
                enrollCounts.TryGetValue(k.maKhoaHoc, out soHocVien);
                return new CourseCardViewModel
                {
                    Course = k,
                    SoHocVien = soHocVien,
                    SoDanhGia = ratings.ContainsKey(k.maKhoaHoc) ? ratings[k.maKhoaHoc].SoDanhGia : 0,
                    DiemDanhGia = ratings.ContainsKey(k.maKhoaHoc) ? ratings[k.maKhoaHoc].Diem : 0
                };
            }).ToList();

            var model = new CategoryPageViewModel
            {
                MaDanhMuc = current.maDanhMuc,
                TenDanhMuc = current.tenDanhMuc,
                MoTa = current.moTa,
                Slug = current.Slug,
                TongSoKhoaHoc = items.Count,
                SoKhoaDaSoHuu = userId > 0
                    ? db.DangKyKhoaHoc.Count(d => d.maHocVien == userId && d.KhoaHoc.maDanhMuc == current.maDanhMuc)
                    : 0,
                CapDo = items
                    .Where(i => !string.IsNullOrWhiteSpace(i.Course.capDo))
                    .GroupBy(i => i.Course.capDo.Trim())
                    .OrderBy(g => ThuTuCapDo.ContainsKey(g.Key) ? ThuTuCapDo[g.Key] : 99)
                    .ThenBy(g => g.Key)
                    .Select(g => new CategoryLinkViewModel { Ten = g.Key, Slug = g.Key, SoKhoaHoc = g.Count() })
                    .ToList()
            };

            // Lọc cấp độ
            if (!string.IsNullOrWhiteSpace(level))
            {
                model.CapDoDangChon = level.Trim();
                items = items.Where(i => string.Equals((i.Course.capDo ?? "").Trim(), model.CapDoDangChon, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            // Sắp xếp
            switch (sort)
            {
                case "gia-tang":
                    items = items.OrderBy(i => i.GiaHienTai).ToList();
                    break;
                case "gia-giam":
                    items = items.OrderByDescending(i => i.GiaHienTai).ToList();
                    break;
                case "danh-gia":
                    items = items.OrderByDescending(i => i.DiemDanhGia).ThenByDescending(i => i.SoDanhGia).ToList();
                    break;
                default:
                    sort = "pho-bien";
                    items = items.OrderByDescending(i => i.SoHocVien).ThenByDescending(i => i.Course.maKhoaHoc).ToList();
                    break;
            }
            model.SapXep = sort;
            model.Items = items;

            // Danh mục khác: đếm khóa đang bán (bỏ khóa đã sở hữu) để người dùng chuyển nhanh
            var soKhoaTheoDanhMuc = db.KhoaHoc
                .Where(k => TrangThaiDangBan.Contains(k.trangThai))
                .Where(k => !db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == k.maKhoaHoc && d.maHocVien == userId))
                .GroupBy(k => k.maDanhMuc)
                .Select(g => new { MaDanhMuc = g.Key, SoKhoa = g.Count() })
                .ToDictionary(x => x.MaDanhMuc ?? "", x => x.SoKhoa);
            model.DanhMucKhac = danhMuc
                .Where(d => d.maDanhMuc != current.maDanhMuc && !string.IsNullOrEmpty(d.Slug))
                .Select(d => new CategoryLinkViewModel
                {
                    Ten = d.tenDanhMuc,
                    Slug = d.Slug,
                    SoKhoaHoc = soKhoaTheoDanhMuc.ContainsKey(d.maDanhMuc) ? soKhoaTheoDanhMuc[d.maDanhMuc] : 0
                })
                .OrderByDescending(d => d.SoKhoaHoc)
                .ThenBy(d => d.Ten)
                .ToList();

            return View(model);
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
