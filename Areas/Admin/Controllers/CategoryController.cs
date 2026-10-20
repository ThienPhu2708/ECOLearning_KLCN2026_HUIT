using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Areas.Admin.Models;
using ECOLearning.Services;

namespace ECOLearning.Areas.Admin.Controllers
{
    public class CategoryController : AdminBaseController
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        private const int PageSize = 10;
        // Giới hạn theo schema: tenDanhMuc nvarchar(100), moTa nvarchar(500)
        private const int MaxTenLength = 100;
        private const int MaxMoTaLength = 500;

        // GET: Admin/Category (UC14: Quản lý danh mục)
        public ActionResult Index(string search = "", string status = "all", int page = 1)
        {
            var query = db.DanhMuc.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLower();
                query = query.Where(c => c.tenDanhMuc.ToLower().Contains(s) || c.maDanhMuc.ToLower().Contains(s));
            }

            status = (status ?? "all").ToLower();
            if (status == "active")
            {
                query = query.Where(c => c.trangThai == null || c.trangThai == true);
            }
            else if (status == "inactive")
            {
                query = query.Where(c => c.trangThai == false);
            }

            int totalItems = query.Count();
            int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalItems / PageSize));
            page = Math.Min(Math.Max(1, page), totalPages);

            var model = query
                .OrderBy(c => c.maDanhMuc)
                .Skip((page - 1) * PageSize)
                .Take(PageSize)
                .Select(c => new CategoryViewModel
                {
                    MaDanhMuc = c.maDanhMuc,
                    TenDanhMuc = c.tenDanhMuc,
                    MoTa = c.moTa,
                    SoKhoaHoc = c.KhoaHoc.Count(),
                    TrangThai = c.trangThai ?? true
                })
                .ToList();

            // Ảnh minh họa (migration 012); chưa chạy migration thì ô chọn ảnh trong form bị ẩn
            var anhService = new CategoryImageService(db);
            ViewBag.AnhInstalled = anhService.IsInstalled();
            var anh = anhService.LayTatCa();
            foreach (var item in model)
            {
                string duongDan;
                if (anh.TryGetValue(item.MaDanhMuc, out duongDan)) item.AnhMinhHoa = duongDan;
            }

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.PageSize = PageSize;
            ViewBag.ActiveCount = db.DanhMuc.Count(c => c.trangThai == null || c.trangThai == true);
            ViewBag.InactiveCount = db.DanhMuc.Count(c => c.trangThai == false);
            ViewBag.BreadcrumbCurrent = "Quản lý danh mục";
            return View(model);
        }

        // POST: Admin/Category/Create (kèm ảnh minh họa, không bắt buộc)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(string tenDanhMuc, string moTa, HttpPostedFileBase anhMinhHoa)
        {
            string error = ValidateInput(null, tenDanhMuc, moTa);
            if (error != null)
            {
                return Json(new { success = false, message = error });
            }

            // Kiểm tra ảnh trước khi ghi CSDL để ảnh lỗi không tạo ra danh mục thiếu ảnh
            var anhService = new CategoryImageService(db);
            AnhDaXuLy anh = null;
            if (anhMinhHoa != null && anhMinhHoa.ContentLength > 0)
            {
                if (!anhService.IsInstalled())
                {
                    return Json(new { success = false, message = "Chưa chạy Database/Migrations/012_DanhMuc_AnhMinhHoa.sql nên chưa lưu được ảnh." });
                }
                anh = CategoryImageService.ChuanBi(anhMinhHoa, out error);
                if (anh == null)
                {
                    return Json(new { success = false, message = error });
                }
            }

            var newCat = new DanhMuc
            {
                maDanhMuc = GenerateCategoryId(),
                tenDanhMuc = tenDanhMuc.Trim(),
                moTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim(),
                trangThai = true
            };

            db.DanhMuc.Add(newCat);
            db.SaveChanges();

            if (anh != null)
            {
                anhService.CapNhat(newCat.maDanhMuc, CategoryImageService.GhiFile(anh, newCat.maDanhMuc, Server));
            }

            return Json(new { success = true, message = "Thêm danh mục mới thành công!" });
        }

        // POST: Admin/Category/Edit (đổi ảnh: gửi anhMinhHoa; không gửi ảnh thì giữ ảnh cũ)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, string tenDanhMuc, string moTa, HttpPostedFileBase anhMinhHoa)
        {
            var cat = db.DanhMuc.FirstOrDefault(c => c.maDanhMuc == id);
            if (cat == null)
            {
                return Json(new { success = false, message = "Danh mục không tồn tại!" });
            }

            string error = ValidateInput(id, tenDanhMuc, moTa);
            if (error != null)
            {
                return Json(new { success = false, message = error });
            }

            var anhService = new CategoryImageService(db);
            bool coAnhMoi = anhMinhHoa != null && anhMinhHoa.ContentLength > 0;
            AnhDaXuLy anh = null;
            if (coAnhMoi && !anhService.IsInstalled())
            {
                return Json(new { success = false, message = "Chưa chạy Database/Migrations/012_DanhMuc_AnhMinhHoa.sql nên chưa lưu được ảnh." });
            }
            if (coAnhMoi)
            {
                anh = CategoryImageService.ChuanBi(anhMinhHoa, out error);
                if (anh == null)
                {
                    return Json(new { success = false, message = error });
                }
            }

            cat.tenDanhMuc = tenDanhMuc.Trim();
            cat.moTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim();
            db.SaveChanges();

            if (anh != null)
            {
                string anhCu = anhService.Lay(id);
                anhService.CapNhat(id, CategoryImageService.GhiFile(anh, id, Server));
                // Xóa file cũ sau khi CSDL đã trỏ sang ảnh mới
                CategoryImageService.XoaFile(anhCu, Server);
            }

            return Json(new { success = true, message = "Cập nhật danh mục thành công!" });
        }

        // POST: Admin/Category/SetStatus (Tạm ngưng / Kích hoạt lại danh mục)
        // Danh mục tạm ngưng bị ẩn khỏi bộ lọc của học viên và dropdown tạo khóa học của giáo viên;
        // các khóa học đã thuộc danh mục vẫn hoạt động bình thường.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetStatus(string id, bool active)
        {
            var cat = db.DanhMuc.FirstOrDefault(c => c.maDanhMuc == id);
            if (cat == null)
            {
                return Json(new { success = false, message = "Danh mục không tồn tại!" });
            }

            cat.trangThai = active;
            db.SaveChanges();

            string message = active
                ? $"Đã kích hoạt lại danh mục \"{cat.tenDanhMuc}\"."
                : $"Đã tạm ngưng danh mục \"{cat.tenDanhMuc}\". Các khóa học thuộc danh mục vẫn hoạt động bình thường.";
            return Json(new { success = true, message = message });
        }

        // POST: Admin/Category/Delete
        // Chỉ xóa được danh mục chưa có khóa học; danh mục đã có khóa học thì dùng Tạm ngưng.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id)
        {
            var cat = db.DanhMuc.FirstOrDefault(c => c.maDanhMuc == id);
            if (cat == null)
            {
                return Json(new { success = false, message = "Danh mục không tồn tại!" });
            }

            // Nghiệp vụ UC14: Kiểm tra nếu danh mục đang có khóa học trực thuộc
            int courseCount = db.KhoaHoc.Count(k => k.maDanhMuc == id);
            if (courseCount > 0)
            {
                return Json(new {
                    success = false,
                    courseCount = courseCount,
                    message = $"Không thể xóa danh mục đang có {courseCount} khóa học. Hãy dùng chức năng Tạm ngưng."
                });
            }

            string anhCu = new CategoryImageService(db).Lay(id);
            db.DanhMuc.Remove(cat);
            db.SaveChanges();
            CategoryImageService.XoaFile(anhCu, Server);

            return Json(new { success = true, message = "Đã xóa danh mục thành công!" });
        }

        // Trả về thông báo lỗi, hoặc null nếu dữ liệu hợp lệ
        private string ValidateInput(string currentId, string tenDanhMuc, string moTa)
        {
            if (string.IsNullOrWhiteSpace(tenDanhMuc))
            {
                return "Tên danh mục không được để trống!";
            }

            string cleanName = tenDanhMuc.Trim();
            if (cleanName.Length > MaxTenLength)
            {
                return $"Tên danh mục tối đa {MaxTenLength} ký tự!";
            }

            if (!string.IsNullOrWhiteSpace(moTa) && moTa.Trim().Length > MaxMoTaLength)
            {
                return $"Mô tả tối đa {MaxMoTaLength} ký tự!";
            }

            // Nghiệp vụ UC14: Tên danh mục là duy nhất (không phân biệt hoa thường, bỏ khoảng trắng đầu/cuối)
            string lowerName = cleanName.ToLower();
            bool duplicate = db.DanhMuc.Any(c => c.maDanhMuc != currentId && c.tenDanhMuc.Trim().ToLower() == lowerName);
            if (duplicate)
            {
                return "Tên danh mục đã tồn tại trên hệ thống! Vui lòng đặt tên khác.";
            }

            return null;
        }

        // Sinh mã theo quy ước dữ liệu hiện có: DM01, DM02, ...
        private string GenerateCategoryId()
        {
            var existingIds = db.DanhMuc.Select(c => c.maDanhMuc).ToList();
            int maxNumber = existingIds
                .Where(x => x.StartsWith("DM"))
                .Select(x => int.TryParse(x.Substring(2), out int n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max();

            string newId;
            do
            {
                maxNumber++;
                newId = "DM" + maxNumber.ToString("D2");
            } while (existingIds.Contains(newId));

            return newId;
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
