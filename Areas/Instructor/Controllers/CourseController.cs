using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Areas.Instructor.Models;
using ECOLearning.Models;

namespace ECOLearning.Areas.Instructor.Controllers
{
    public class CourseController : InstructorBaseController
    {
        // GET: Instructor/Course (UC9: Danh sách khóa học của giảng viên)
        public ActionResult Index(string search = "", string status = "")
        {
            int teacherId = CurrentTeacherId;

            var query = db.KhoaHoc
                .Where(k => k.maGiaoVien == teacherId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                string s = search.Trim().ToLower();
                query = query.Where(k => k.tenKhoaHoc.ToLower().Contains(s) || k.maKhoaHoc.ToLower().Contains(s));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(k => k.trangThai == status);
            }

            var courses = query.OrderByDescending(k => k.maKhoaHoc).ToList();

            var model = courses.Select(k => {
                int studentCount = k.DangKyKhoaHoc != null ? k.DangKyKhoaHoc.Count : 0;
                int lessonCount = 0;
                if (k.ChuongHoc != null)
                {
                    foreach (var c in k.ChuongHoc)
                    {
                        if (c.BaiHoc != null) lessonCount += c.BaiHoc.Count;
                    }
                }

                return new InstructorCourseItem
                {
                    MaKhoaHoc = k.maKhoaHoc,
                    TenKhoaHoc = k.tenKhoaHoc,
                    AnhBia = k.anhBia ?? "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?w=300&auto=format&fit=crop&q=80",
                    TenDanhMuc = k.DanhMuc != null ? k.DanhMuc.tenDanhMuc : "Chung",
                    GiaBan = k.giaBan,
                    GiaKhuyenMai = k.giaKhuyenMai,
                    SoHocVien = studentCount,
                    TrangThai = k.trangThai ?? "Bản nháp",
                    NgayTao = DateTime.Now,
                    SoBaiHoc = lessonCount,
                    LyDoTuChoi = k.KiemDuyetKhoaHoc != null ? k.KiemDuyetKhoaHoc.OrderByDescending(kd => kd.ngayGui).Select(kd => kd.lyDoTuChoi).FirstOrDefault() : null
                };
            }).ToList();

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.BreadcrumbCurrent = "Quản lý khóa học";

            return View(model);
        }

        private void CreateOrUpdateKiemDuyetRecord(string courseId)
        {
            var kd = db.KiemDuyetKhoaHoc.FirstOrDefault(x => x.maKhoaHoc == courseId);
            if (kd == null)
            {
                kd = new KiemDuyetKhoaHoc
                {
                    maKiemDuyet = "KD-" + Guid.NewGuid().ToString().Substring(0, 8),
                    maKhoaHoc = courseId,
                    ngayGui = DateTime.Now,
                    trangThai = "Chờ duyệt",
                    lyDoTuChoi = null
                };
                db.KiemDuyetKhoaHoc.Add(kd);
            }
            else
            {
                kd.ngayGui = DateTime.Now;
                kd.trangThai = "Chờ duyệt";
                kd.lyDoTuChoi = null;
            }
        }

        // GET: Instructor/Course/Create
        public ActionResult Create()
        {
            var model = new CourseCreateOrEditViewModel();
            model.Categories = db.DanhMuc.Select(c => new SelectListItem
            {
                Value = c.maDanhMuc,
                Text = c.tenDanhMuc
            }).ToList();

            model.CapDo = "Cơ bản";
            ViewBag.BreadcrumbCurrent = "Tạo khóa học mới";
            return View(model);
        }

        // POST: Instructor/Course/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CourseCreateOrEditViewModel model, string actionType = "draft")
        {
            if (string.IsNullOrWhiteSpace(model.TenKhoaHoc))
            {
                ModelState.AddModelError("TenKhoaHoc", "Vui lòng nhập tên khóa học!");
            }

            if (model.GiaBan < 0)
            {
                ModelState.AddModelError("GiaBan", "Giá bán không được là số âm!");
            }

            if (string.IsNullOrEmpty(model.MaDanhMuc))
            {
                ModelState.AddModelError("MaDanhMuc", "Vui lòng chọn danh mục đào tạo!");
            }

            if (!ModelState.IsValid)
            {
                model.Categories = db.DanhMuc.Select(c => new SelectListItem
                {
                    Value = c.maDanhMuc,
                    Text = c.tenDanhMuc
                }).ToList();
                return View(model);
            }

            // Sinh mã khóa học tự động KH + timestamp
            string courseId = "KH" + DateTime.Now.ToString("yyMMddHHmmss");

            var course = new KhoaHoc
            {
                maKhoaHoc = courseId,
                tenKhoaHoc = model.TenKhoaHoc.Trim(),
                moTa = model.MoTa,
                moTaNgan = model.MoTaNgan,
                mucTieuDauRa = model.MucTieuDauRa,
                giaBan = model.GiaBan,
                giaKhuyenMai = model.GiaKhuyenMai,
                capDo = model.CapDo ?? "Cơ bản",
                maDanhMuc = model.MaDanhMuc,
                maGiaoVien = CurrentTeacherId,
                anhBia = !string.IsNullOrWhiteSpace(model.AnhBia) ? model.AnhBia.Trim() : "https://images.unsplash.com/photo-1516321318423-f06f85e504b3?w=500&auto=format&fit=crop&q=80",
                trangThai = (actionType == "submit") ? "Chờ duyệt" : "Bản nháp"
            };

            db.KhoaHoc.Add(course);

            if (actionType == "submit")
            {
                CreateOrUpdateKiemDuyetRecord(courseId);
            }

            db.SaveChanges();

            TempData["SuccessMessage"] = (actionType == "submit")
                ? "Khóa học đã được gửi yêu cầu kiểm duyệt thành công!"
                : "Đã lưu bản nháp khóa học thành công!";

            return RedirectToAction("Index");
        }

        // GET: Instructor/Course/Edit/5
        public ActionResult Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return HttpNotFound();

            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == id && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();

            var model = new CourseCreateOrEditViewModel
            {
                MaKhoaHoc = course.maKhoaHoc,
                TenKhoaHoc = course.tenKhoaHoc,
                MoTa = course.moTa,
                MoTaNgan = course.moTaNgan,
                MucTieuDauRa = course.mucTieuDauRa,
                GiaBan = course.giaBan,
                GiaKhuyenMai = course.giaKhuyenMai,
                CapDo = course.capDo,
                MaDanhMuc = course.maDanhMuc,
                AnhBia = course.anhBia,
                TrangThai = course.trangThai,
                Categories = db.DanhMuc.Select(c => new SelectListItem
                {
                    Value = c.maDanhMuc,
                    Text = c.tenDanhMuc
                }).ToList()
            };

            ViewBag.BreadcrumbCurrent = "Chỉnh sửa khóa học";
            return View(model);
        }

        // POST: Instructor/Course/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(CourseCreateOrEditViewModel model, string actionType = "save")
        {
            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == model.MaKhoaHoc && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();

            if (string.IsNullOrWhiteSpace(model.TenKhoaHoc))
            {
                ModelState.AddModelError("TenKhoaHoc", "Vui lòng nhập tên khóa học!");
            }

            if (!ModelState.IsValid)
            {
                model.Categories = db.DanhMuc.Select(c => new SelectListItem
                {
                    Value = c.maDanhMuc,
                    Text = c.tenDanhMuc
                }).ToList();
                return View(model);
            }

            course.tenKhoaHoc = model.TenKhoaHoc.Trim();
            course.moTa = model.MoTa;
            course.moTaNgan = model.MoTaNgan;
            course.mucTieuDauRa = model.MucTieuDauRa;
            course.giaBan = model.GiaBan;
            course.giaKhuyenMai = model.GiaKhuyenMai;
            course.capDo = model.CapDo ?? "Cơ bản";
            course.maDanhMuc = model.MaDanhMuc;
            if (!string.IsNullOrWhiteSpace(model.AnhBia))
            {
                course.anhBia = model.AnhBia.Trim();
            }

            if (actionType == "submit")
            {
                // Kiểm tra có bài học chưa
                bool hasLessons = course.ChuongHoc != null && course.ChuongHoc.Any(c => c.BaiHoc != null && c.BaiHoc.Any());
                if (!hasLessons)
                {
                    TempData["ErrorMessage"] = "Khóa học không có nội dung bài học! Vui lòng thêm bài học trước khi gửi duyệt.";
                    return RedirectToAction("Index");
                }
                course.trangThai = "Chờ duyệt";
                CreateOrUpdateKiemDuyetRecord(course.maKhoaHoc);
            }

            db.SaveChanges();
            TempData["SuccessMessage"] = "Cập nhật thông tin khóa học thành công!";

            return RedirectToAction("Index");
        }

        // POST: Instructor/Course/SubmitForApproval/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SubmitForApproval(string id)
        {
            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == id && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();

            bool hasLessons = course.ChuongHoc != null && course.ChuongHoc.Any(c => c.BaiHoc != null && c.BaiHoc.Any());
            if (!hasLessons)
            {
                TempData["ErrorMessage"] = "Khóa học không có nội dung bài học! Vui lòng tạo đề cương và video trước khi gửi duyệt.";
                return RedirectToAction("Index");
            }

            course.trangThai = "Chờ duyệt";
            CreateOrUpdateKiemDuyetRecord(course.maKhoaHoc);
            db.SaveChanges();

            TempData["SuccessMessage"] = string.Format("Đã gửi khóa học '{0}' vào hàng đợi kiểm duyệt thành công!", course.tenKhoaHoc);
            return RedirectToAction("Index");
        }

        // POST: Instructor/Course/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(string id)
        {
            int teacherId = CurrentTeacherId;
            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == id && k.maGiaoVien == teacherId);
            if (course == null) return HttpNotFound();

            // Kiểm tra ràng buộc học viên
            int studentCount = course.DangKyKhoaHoc != null ? course.DangKyKhoaHoc.Count : 0;
            if (studentCount > 0)
            {
                TempData["ErrorMessage"] = "Không thể xóa khóa học đã có học viên đăng ký tham gia!";
                return RedirectToAction("Index");
            }

            // Xóa các liên kết bài học & chương học
            if (course.ChuongHoc != null)
            {
                foreach (var ch in course.ChuongHoc.ToList())
                {
                    if (ch.BaiHoc != null)
                    {
                        foreach (var bh in ch.BaiHoc.ToList())
                        {
                            if (bh.TaiNguyen != null)
                            {
                                db.TaiNguyen.RemoveRange(bh.TaiNguyen);
                            }
                            db.BaiHoc.Remove(bh);
                        }
                    }
                    db.ChuongHoc.Remove(ch);
                }
            }

            db.KhoaHoc.Remove(course);
            db.SaveChanges();

            TempData["SuccessMessage"] = string.Format("Đã xóa thành công khóa học '{0}'!", course.tenKhoaHoc);
            return RedirectToAction("Index");
        }
    }
}
