using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Models;
using ECOLearning.Models;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class CurriculumController : InstructorBaseController
    {
        // GET: Instructor/Curriculum (UC10: Đề cương & Tài nguyên khóa học)
        public ActionResult Index(string courseId = "")
        {
            int teacherId = CurrentTeacherId;
            var teacherCourses = db.KhoaHoc
                .Where(k => k.maGiaoVien == teacherId)
                .ToList();

            if (!teacherCourses.Any())
            {
                ViewBag.NoCourses = true;
                ViewBag.BreadcrumbCurrent = "Đề cương & Tài nguyên";
                return View(new CurriculumViewModel());
            }

            var currentCourse = !string.IsNullOrEmpty(courseId)
                ? teacherCourses.FirstOrDefault(k => k.maKhoaHoc == courseId)
                : teacherCourses.FirstOrDefault();

            if (currentCourse == null)
            {
                currentCourse = teacherCourses.First();
            }

            var model = new CurriculumViewModel
            {
                MaKhoaHoc = currentCourse.maKhoaHoc,
                TenKhoaHoc = currentCourse.tenKhoaHoc
            };

            var chapters = db.ChuongHoc
                .Where(c => c.maKhoaHoc == currentCourse.maKhoaHoc)
                .OrderBy(c => c.thuTu)
                .ToList();

            foreach (var ch in chapters)
            {
                var chItem = new ChuongHocItem
                {
                    MaChuong = ch.maChuong,
                    TenChuong = ch.tenChuong,
                    ThuTu = ch.thuTu ?? 1
                };

                var lessons = db.BaiHoc
                    .Where(b => b.maChuong == ch.maChuong)
                    .OrderBy(b => b.thuTu)
                    .ToList();

                foreach (var les in lessons)
                {
                    var lesItem = new BaiHocItem
                    {
                        MaBaiHoc = les.maBaiHoc,
                        TenBaiHoc = les.tenBaiHoc,
                        DuongDanVideo = les.duongDanVideo,
                        ThoiLuong = les.thoiLuong ?? 0,
                        ChoXemThu = les.choXemThu ?? false,
                        ThuTu = les.thuTu ?? 1
                    };

                    var resources = db.TaiNguyen
                        .Where(t => t.maBaiHoc == les.maBaiHoc)
                        .ToList();

                    foreach (var res in resources)
                    {
                        lesItem.TaiNguyens.Add(new TaiNguyenItem
                        {
                            MaTaiNguyen = res.maTaiNguyen,
                            TenTaiNguyen = res.tenTaiNguyen,
                            DuongDan = res.duongDan,
                            LoaiTaiNguyen = res.loaiTaiNguyen
                        });
                    }

                    chItem.BaiHocs.Add(lesItem);
                }

                model.ChuongHocs.Add(chItem);
            }

            ViewBag.TeacherCourses = teacherCourses;
            ViewBag.SelectedCourseId = currentCourse.maKhoaHoc;
            ViewBag.BreadcrumbCurrent = "Đề cương & Tài nguyên";

            return View(model);
        }

        // POST: Instructor/Curriculum/AddChapter
        [HttpPost]
        public JsonResult AddChapter(string courseId, string tenChuong, int? thuTu)
        {
            if (string.IsNullOrWhiteSpace(tenChuong))
            {
                return Json(new { success = false, message = "Tên chương học không được để trống!" });
            }

            string chapterId = "CH" + DateTime.Now.ToString("yyMMddHHmmss");
            var chapter = new ChuongHoc
            {
                maChuong = chapterId,
                maKhoaHoc = courseId,
                tenChuong = tenChuong.Trim(),
                thuTu = thuTu ?? (db.ChuongHoc.Count(c => c.maKhoaHoc == courseId) + 1)
            };

            db.ChuongHoc.Add(chapter);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã thêm chương học mới thành công!", chapterId = chapterId });
        }

        // POST: Instructor/Curriculum/DeleteChapter
        [HttpPost]
        public JsonResult DeleteChapter(string chapterId)
        {
            var chapter = db.ChuongHoc.FirstOrDefault(c => c.maChuong == chapterId);
            if (chapter == null)
            {
                return Json(new { success = false, message = "Không tìm thấy chương học!" });
            }

            // Xóa các bài học con và tài nguyên
            if (chapter.BaiHoc != null)
            {
                foreach (var b in chapter.BaiHoc.ToList())
                {
                    if (b.TaiNguyen != null)
                    {
                        db.TaiNguyen.RemoveRange(b.TaiNguyen);
                    }
                    db.BaiHoc.Remove(b);
                }
            }

            db.ChuongHoc.Remove(chapter);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã xóa chương học thành công!" });
        }

        // POST: Instructor/Curriculum/AddLesson
        [HttpPost]
        public JsonResult AddLesson(string chapterId, string tenBaiHoc, string videoUrl, int? thoiLuong, bool choXemThu)
        {
            if (string.IsNullOrWhiteSpace(tenBaiHoc))
            {
                return Json(new { success = false, message = "Tên bài học không được để trống!" });
            }

            string lessonId = "BH" + DateTime.Now.ToString("yyMMddHHmmss");
            int order = db.BaiHoc.Count(b => b.maChuong == chapterId) + 1;

            var lesson = new BaiHoc
            {
                maBaiHoc = lessonId,
                maChuong = chapterId,
                tenBaiHoc = tenBaiHoc.Trim(),
                duongDanVideo = !string.IsNullOrWhiteSpace(videoUrl) ? videoUrl.Trim() : "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                thoiLuong = thoiLuong ?? 15,
                choXemThu = choXemThu,
                thuTu = order
            };

            db.BaiHoc.Add(lesson);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã thêm bài học thành công!", lessonId = lessonId });
        }

        // POST: Instructor/Curriculum/DeleteLesson
        [HttpPost]
        public JsonResult DeleteLesson(string lessonId)
        {
            var lesson = db.BaiHoc.FirstOrDefault(b => b.maBaiHoc == lessonId);
            if (lesson == null)
            {
                return Json(new { success = false, message = "Không tìm thấy bài học!" });
            }

            if (lesson.TaiNguyen != null)
            {
                db.TaiNguyen.RemoveRange(lesson.TaiNguyen);
            }

            db.BaiHoc.Remove(lesson);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã xóa bài học thành công!" });
        }

        // POST: Instructor/Curriculum/AddResource
        [HttpPost]
        public JsonResult AddResource(string lessonId, string tenTaiNguyen, string fileUrl, string loaiTaiNguyen)
        {
            if (string.IsNullOrWhiteSpace(tenTaiNguyen))
            {
                return Json(new { success = false, message = "Tên tài nguyên không được để trống!" });
            }

            string resId = "TN" + DateTime.Now.ToString("yyMMddHHmmss");
            var res = new TaiNguyen
            {
                maTaiNguyen = resId,
                maBaiHoc = lessonId,
                tenTaiNguyen = tenTaiNguyen.Trim(),
                duongDan = !string.IsNullOrWhiteSpace(fileUrl) ? fileUrl.Trim() : "https://example.com/tai-lieu.pdf",
                loaiTaiNguyen = !string.IsNullOrWhiteSpace(loaiTaiNguyen) ? loaiTaiNguyen.Trim() : "PDF"
            };

            db.TaiNguyen.Add(res);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã thêm tài nguyên thành công!", resourceId = resId });
        }

        // POST: Instructor/Curriculum/DeleteResource
        [HttpPost]
        public JsonResult DeleteResource(string resourceId)
        {
            var res = db.TaiNguyen.FirstOrDefault(t => t.maTaiNguyen == resourceId);
            if (res == null)
            {
                return Json(new { success = false, message = "Không tìm thấy tài nguyên!" });
            }

            db.TaiNguyen.Remove(res);
            db.SaveChanges();

            return Json(new { success = true, message = "Đã xóa tài nguyên thành công!" });
        }
    }
}
