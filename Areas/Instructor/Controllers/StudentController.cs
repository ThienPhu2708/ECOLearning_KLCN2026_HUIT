using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Models;
using ECOLearning.Models;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class StudentController : InstructorBaseController
    {
        public ActionResult CourseStudents(string courseId, string search = "", string sort = "date_desc", string status = "")
        {
            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == courseId && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();

            var result = (ViewResult)Index(courseId, search, sort, status);
            ViewBag.IsCourseStudentPage = true;
            ViewBag.ShowCourseBreadcrumb = true;
            ViewBag.BreadcrumbCourseName = course.tenKhoaHoc;
            ViewBag.BreadcrumbCourseId = course.maKhoaHoc;
            ViewBag.BreadcrumbCurrent = "Danh sách học viên";
            result.ViewName = "Index";
            return result;
        }

        // GET: Instructor/Student (UC11: Quản lý học viên)
        // sort: date_desc (mặc định) | date_asc | progress_desc | progress_asc | rating_desc | rating_asc
        public ActionResult Index(string courseId = "", string search = "", string sort = "date_desc", string status = "")
        {
            int teacherId = CurrentTeacherId;
            var teacherCourses = db.KhoaHoc
                .Where(k => k.maGiaoVien == teacherId)
                .ToList();

            var courseIds = teacherCourses.Select(k => k.maKhoaHoc).ToList();
            if (!string.IsNullOrEmpty(courseId) && !courseIds.Contains(courseId)) return HttpNotFound();

            var enrollmentsQuery = db.DangKyKhoaHoc
                .Where(dk => courseIds.Contains(dk.maKhoaHoc))
                .AsQueryable();

            if (!string.IsNullOrEmpty(courseId))
            {
                enrollmentsQuery = enrollmentsQuery.Where(dk => dk.maKhoaHoc == courseId);
            }

            var enrollments = enrollmentsQuery.ToList();

            // Lấy tiến độ học tập
            var progressList = db.TienDoHocTap
                .Where(td => courseIds.Contains(td.maKhoaHoc))
                .ToList();

            // Lấy đánh giá
            var reviews = db.DanhGiaKhoaHoc
                .Where(dg => courseIds.Contains(dg.maKhoaHoc))
                .ToList();

            var studentList = new List<InstructorStudentItem>();

            foreach (var dk in enrollments)
            {
                var user = dk.NguoiDung;
                if (user == null) continue;

                var progress = progressList.FirstOrDefault(p => p.maHocVien == dk.maHocVien && p.maKhoaHoc == dk.maKhoaHoc);
                var review = reviews.FirstOrDefault(r => r.maHocVien == dk.maHocVien && r.maKhoaHoc == dk.maKhoaHoc);

                studentList.Add(new InstructorStudentItem
                {
                    MaHocVien = user.maNguoiDung,
                    HoTen = user.hoTen,
                    Email = user.email,
                    AnhDaiDien = user.anhDaiDien ?? "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=100&auto=format&fit=crop&q=80",
                    MaKhoaHoc = dk.maKhoaHoc,
                    TenKhoaHoc = dk.KhoaHoc != null ? dk.KhoaHoc.tenKhoaHoc : dk.maKhoaHoc,
                    NgayThamGia = dk.ngayDangKy ?? DateTime.Now,
                    TienDoHoanThanh = progress?.phanTramHoanThanh ?? 0,
                    SoSao = review?.soSao
                });
            }

            // Use the same lesson-level progress and quiz state as certificate eligibility.
            var quizService = new ECOLearning.Services.QuizService(db);
            var quizRows = new List<ECOLearning.ViewModels.QuizStudentRow>();
            if (quizService.Installed())
            {
                var states = quizService.Gradebook(teacherId, courseId);
                quizRows = QuizController.Filter(states, status);
                var allowed = new HashSet<string>(quizRows.Select(r => r.CourseId + ":" + r.StudentId));
                studentList = studentList.Where(s => allowed.Contains(s.MaKhoaHoc + ":" + s.MaHocVien)).ToList();
                foreach (var item in studentList)
                {
                    var state = states.FirstOrDefault(r => r.StudentId == item.MaHocVien && r.CourseId == item.MaKhoaHoc);
                    if (state == null) continue;
                    item.TienDoHoanThanh = state.Progress;
                    item.QuizStatus = state.QuizStatus;
                    item.QuizScore = state.BestScore;
                    item.CertificateStatus = state.CertificateStatus;
                }
            }
            if (!string.IsNullOrEmpty(search))
            {
                string s = search.Trim().ToLower();
                studentList = studentList.Where(st => st.HoTen.ToLower().Contains(s) || st.Email.ToLower().Contains(s)).ToList();
            }

            // Sắp xếp (học viên chưa đánh giá luôn ở cuối khi sắp theo sao)
            switch (sort)
            {
                case "date_asc":
                    studentList = studentList.OrderBy(st => st.NgayThamGia).ToList();
                    break;
                case "progress_desc":
                    studentList = studentList.OrderByDescending(st => st.TienDoHoanThanh).ThenByDescending(st => st.NgayThamGia).ToList();
                    break;
                case "progress_asc":
                    studentList = studentList.OrderBy(st => st.TienDoHoanThanh).ThenByDescending(st => st.NgayThamGia).ToList();
                    break;
                case "quiz_desc":
                    studentList = studentList.OrderBy(st => st.QuizScore.HasValue ? 0 : 1).ThenByDescending(st => st.QuizScore).ThenByDescending(st => st.NgayThamGia).ToList();
                    break;
                case "quiz_asc":
                    studentList = studentList.OrderBy(st => st.QuizScore.HasValue ? 0 : 1).ThenBy(st => st.QuizScore).ThenByDescending(st => st.NgayThamGia).ToList();
                    break;
                case "rating_desc":
                    studentList = studentList.OrderBy(st => st.SoSao.HasValue && st.SoSao > 0 ? 0 : 1).ThenByDescending(st => st.SoSao).ThenByDescending(st => st.NgayThamGia).ToList();
                    break;
                case "rating_asc":
                    studentList = studentList.OrderBy(st => st.SoSao.HasValue && st.SoSao > 0 ? 0 : 1).ThenBy(st => st.SoSao).ThenByDescending(st => st.NgayThamGia).ToList();
                    break;
                default:
                    sort = "date_desc";
                    studentList = studentList.OrderByDescending(st => st.NgayThamGia).ToList();
                    break;
            }

            ViewBag.Sort = sort;
            var visible = new HashSet<string>(studentList.Select(s => s.MaKhoaHoc + ":" + s.MaHocVien));
            ViewBag.QuizRows = quizRows.Where(r => visible.Contains(r.CourseId + ":" + r.StudentId)).ToList();
            ViewBag.Status = status;
            ViewBag.TeacherCourses = teacherCourses;
            ViewBag.SelectedCourseId = courseId;
            ViewBag.Search = search;
            ViewBag.BreadcrumbCurrent = "Quản lý học viên";

            return View(studentList);
        }
    }
}
