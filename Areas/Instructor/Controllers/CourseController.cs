using System;
using System.Web;
using System.Web.Mvc;

namespace ECOLearning.Areas.Instructor.Controllers
{
    [Authorize(Roles = "GiaoVien")]
    public class CourseController : Controller
    {
        // GET: Instructor/Course (UC9: Danh sách khóa học của giảng viên)
        public ActionResult Index()
        {
            return View();
        }

        // GET: Instructor/Course/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Instructor/Course/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(FormCollection form, HttpPostedFileBase thumbnail)
        {
            // Lưu bản nháp hoặc chuyển tiếp sang bước tạo đề cương
            return RedirectToAction("Index");
        }

        // GET: Instructor/Course/Edit/5
        public ActionResult Edit(string id)
        {
            return View();
        }

        // POST: Instructor/Course/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(string id, FormCollection form, HttpPostedFileBase thumbnail)
        {
            return RedirectToAction("Index");
        }

        // POST: Instructor/Course/SubmitForApproval/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitForApproval(string id)
        {
            // Kiểm tra nếu chưa có video sẽ cảnh báo 'Khóa học không có nội dung'
            return RedirectToAction("Index");
        }

        // POST: Instructor/Course/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id)
        {
            return RedirectToAction("Index");
        }
    }
}
