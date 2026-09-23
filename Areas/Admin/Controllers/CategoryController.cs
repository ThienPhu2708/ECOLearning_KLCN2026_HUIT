using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Areas.Admin.Models;

namespace ECOLearning.Areas.Admin.Controllers
{
    public class CategoryController : AdminBaseController
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Admin/Category (UC14: Quản lý danh mục)
        public ActionResult Index()
        {
            var categories = db.DanhMuc.ToList();

            var model = categories.Select(c => new CategoryViewModel
            {
                MaDanhMuc = c.maDanhMuc,
                TenDanhMuc = c.tenDanhMuc,
                MoTa = c.moTa,
                SoKhoaHoc = c.KhoaHoc != null ? c.KhoaHoc.Count : 0,
                TrangThai = c.trangThai ?? true
            }).ToList();

            ViewBag.BreadcrumbCurrent = "Quản lý danh mục";
            return View(model);
        }

        // POST: Admin/Category/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(string tenDanhMuc, string moTa)
        {
            if (string.IsNullOrWhiteSpace(tenDanhMuc))
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Tên danh mục không được để trống!" });

                TempData["Error"] = "Tên danh mục không được để trống!";
                return RedirectToAction("Index");
            }

            string cleanName = tenDanhMuc.Trim();

            // Nghiệp vụ UC14: Kiểm tra trùng lặp tên danh mục trong CSDL
            bool exists = db.DanhMuc.Any(c => c.tenDanhMuc.Trim().Equals(cleanName, StringComparison.OrdinalIgnoreCase));
            if (exists)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Tên danh mục đã tồn tại trên hệ thống! Vui lòng đặt tên khác." });

                TempData["Error"] = "Tên danh mục đã tồn tại trên hệ thống! Vui lòng đặt tên khác.";
                return RedirectToAction("Index");
            }

            // Tạo mã danh mục tự động
            int nextId = db.DanhMuc.Count() + 1;
            string newId = "CAT-" + nextId.ToString("D3");
            while (db.DanhMuc.Any(c => c.maDanhMuc == newId))
            {
                nextId++;
                newId = "CAT-" + nextId.ToString("D3");
            }

            var newCat = new DanhMuc
            {
                maDanhMuc = newId,
                tenDanhMuc = cleanName,
                moTa = moTa,
                trangThai = true
            };

            db.DanhMuc.Add(newCat);
            db.SaveChanges();

            if (Request.IsAjaxRequest())
                return Json(new { success = true, message = "Thêm danh mục mới thành công!" });

            TempData["Success"] = "Thêm danh mục mới thành công!";
            return RedirectToAction("Index");
        }

        // POST: Admin/Category/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, string tenDanhMuc, string moTa)
        {
            var cat = db.DanhMuc.FirstOrDefault(c => c.maDanhMuc == id);
            if (cat == null)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Danh mục không tồn tại!" });

                TempData["Error"] = "Danh mục không tồn tại!";
                return RedirectToAction("Index");
            }

            string cleanName = tenDanhMuc.Trim();

            // Kiểm tra trùng tên danh mục với các bản ghi khác trong CSDL
            bool duplicate = db.DanhMuc.Any(c => c.maDanhMuc != id && c.tenDanhMuc.Trim().Equals(cleanName, StringComparison.OrdinalIgnoreCase));
            if (duplicate)
            {
                if (Request.IsAjaxRequest())
                    return Json(new { success = false, message = "Tên danh mục đã tồn tại trên hệ thống!" });

                TempData["Error"] = "Tên danh mục đã tồn tại trên hệ thống!";
                return RedirectToAction("Index");
            }

            cat.tenDanhMuc = cleanName;
            cat.moTa = moTa;
            db.SaveChanges();

            if (Request.IsAjaxRequest())
                return Json(new { success = true, message = "Cập nhật danh mục thành công!" });

            TempData["Success"] = "Cập nhật danh mục thành công!";
            return RedirectToAction("Index");
        }

        // POST: Admin/Category/Delete
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
                    message = $"Không thể xóa danh mục đang có {courseCount} khóa học sử dụng!" 
                });
            }

            db.DanhMuc.Remove(cat);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã xóa danh mục thành công!" });
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
