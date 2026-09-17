using System;
using System.Web.Mvc;

namespace ECOLearning.Areas.Instructor.Controllers
{
    [Authorize(Roles = "GiaoVien")]
    public class StudentController : Controller
    {
        // GET: Instructor/Student (UC11: Danh sách học viên và tiến độ học tập)
        public ActionResult Index(string courseId = null, string search = null)
        {
            ViewBag.CourseId = courseId;
            return View();
        }

        // GET: Instructor/Student/DetailPopup
        public ActionResult StudentDetail(string studentId, string courseId)
        {
            return PartialView();
        }
    }
}
