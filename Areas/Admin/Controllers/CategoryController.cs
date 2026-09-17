using System;
using System.Web.Mvc;

namespace ECOLearning.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CategoryController : Controller
    {
        // GET: Admin/Category (UC14: Quản lý danh mục)
        public ActionResult Index()
        {
            return View();
        }

        // POST: Admin/Category/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(string tenDanhMuc, string moTa)
        {
            // Nghiệp vụ: Kiểm tra trùng lặp tên danh mục
            return RedirectToAction("Index");
        }

        // POST: Admin/Category/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, string tenDanhMuc, string moTa)
        {
            return RedirectToAction("Index");
        }

        // POST: Admin/Category/Delete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id)
        {
            // Nghiệp vụ: Khóa xóa nếu danh mục đang có khóa học trực thuộc
            return RedirectToAction("Index");
        }
    }
}
