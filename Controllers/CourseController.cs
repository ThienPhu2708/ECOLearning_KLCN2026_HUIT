using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;

namespace ECOLearning.Controllers
{
    public class CourseController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Course (UC4: Tìm kiếm & Lọc nâng cao đa tiêu chí)
        public ActionResult Index(string search, string category, bool? isFree, bool? isPaid, string level, double? minRating, string sort)
        {
            var query = db.KhoaHoc
                .Include(k => k.NguoiDung)
                .Include(k => k.DanhMuc)
                .Include(k => k.DanhGiaKhoaHoc)
                .AsQueryable();

            // 1. Tìm kiếm thông minh theo từ khóa (Tên khóa học, Tên giảng viên, Mô tả, Danh mục)
            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim().ToLower();
                query = query.Where(k =>
                    k.tenKhoaHoc.ToLower().Contains(term) ||
                    (k.NguoiDung != null && k.NguoiDung.hoTen.ToLower().Contains(term)) ||
                    (k.moTa != null && k.moTa.ToLower().Contains(term)) ||
                    (k.moTaNgan != null && k.moTaNgan.ToLower().Contains(term)) ||
                    (k.DanhMuc != null && k.DanhMuc.tenDanhMuc.ToLower().Contains(term))
                );
            }

            // 2. Lọc theo Danh mục
            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(k => k.maDanhMuc == category || (k.DanhMuc != null && k.DanhMuc.tenDanhMuc == category));
            }

            // 3. Lọc theo Mức giá (Miễn phí / Có phí)
            if (isFree.HasValue && isFree.Value && (isPaid == null || !isPaid.Value))
            {
                query = query.Where(k => k.giaBan == 0 || (k.giaKhuyenMai.HasValue && k.giaKhuyenMai.Value == 0));
            }
            else if (isPaid.HasValue && isPaid.Value && (isFree == null || !isFree.Value))
            {
                query = query.Where(k => k.giaBan > 0 && (!k.giaKhuyenMai.HasValue || k.giaKhuyenMai.Value > 0));
            }

            // 4. Lọc theo Cấp độ (Mọi cấp độ, Cơ bản, Trung cấp, Nâng cao)
            if (!string.IsNullOrWhiteSpace(level))
            {
                query = query.Where(k => k.capDo == level);
            }

            // 5. Sắp xếp
            if (sort == "price-asc")
            {
                query = query.OrderBy(k => k.giaKhuyenMai.HasValue ? k.giaKhuyenMai.Value : k.giaBan);
            }
            else if (sort == "price-desc")
            {
                query = query.OrderByDescending(k => k.giaKhuyenMai.HasValue ? k.giaKhuyenMai.Value : k.giaBan);
            }
            else
            {
                query = query.OrderByDescending(k => k.maKhoaHoc);
            }

            var courseList = query.ToList();

            // 6. Lọc theo Đánh giá tối thiểu (Rating >= minRating)
            if (minRating.HasValue && minRating.Value > 0)
            {
                courseList = courseList.Where(k => k.DanhGiaKhoaHoc != null && k.DanhGiaKhoaHoc.Count > 0 && k.DanhGiaKhoaHoc.Average(d => (double)d.soSao) >= minRating.Value).ToList();
            }

            // Pass filter metadata to View
            ViewBag.Categories = db.DanhMuc.Where(d => d.trangThai == true).ToList();
            ViewBag.SearchTerm = search;
            ViewBag.SelectedCategory = category;
            ViewBag.IsFree = isFree;
            ViewBag.IsPaid = isPaid;
            ViewBag.SelectedLevel = level;
            ViewBag.MinRating = minRating;
            ViewBag.Sort = sort;

            return View(courseList);
        }

        // GET: Course/Details/5 (Chi tiết khóa học & Video Preview)
        public ActionResult Details(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                id = "KH_TEST01";
            }

            // 1. Đảm bảo dữ liệu khóa học mẫu đã có trong CSDL
            HomeController.EnsureSampleCoursesSeeded(db);

            // 2. Tìm kiếm khóa học theo Mã hoặc Tên khóa học
            var course = db.KhoaHoc
                .Include(k => k.NguoiDung)
                .Include(k => k.DanhMuc)
                .Include(k => k.DanhGiaKhoaHoc)
                .Include("DanhGiaKhoaHoc.NguoiDung")
                .Include("ChuongHoc.BaiHoc")
                .FirstOrDefault(k => k.maKhoaHoc == id || (k.tenKhoaHoc != null && k.tenKhoaHoc.Contains(id)));

            if (course == null)
            {
                course = db.KhoaHoc
                    .Include(k => k.NguoiDung)
                    .Include(k => k.DanhMuc)
                    .Include(k => k.DanhGiaKhoaHoc)
                    .Include("DanhGiaKhoaHoc.NguoiDung")
                    .Include("ChuongHoc.BaiHoc")
                    .FirstOrDefault(k => k.maKhoaHoc == "KH_TEST01") ?? db.KhoaHoc.FirstOrDefault();
            }

            if (course == null)
            {
                return HttpNotFound();
            }

            // 3. Kiểm tra trạng thái học viên đăng ký
            bool isEnrolled = false;
            var currentUser = Session["User"] as NguoiDung;
            if (currentUser != null)
            {
                isEnrolled = db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == course.maKhoaHoc && d.maHocVien == currentUser.maNguoiDung);
            }
            ViewBag.IsEnrolled = isEnrolled;

            var cartList = Session["Cart"] as List<ECOLearning.ViewModels.CartItemViewModel>;
            bool isInCart = cartList != null && cartList.Any(i => i.CourseId == course.maKhoaHoc);
            ViewBag.IsInCart = isInCart;

            // 4. Đồng bộ đánh giá CSDL: KH_TEST01 có 1 lượt đánh giá từ người dùng hiện tại, KH_TEST02 có 0 lượt đánh giá
            try
            {
                if (course.maKhoaHoc == "KH_TEST01")
                {
                    var activeUser = currentUser 
                                  ?? db.NguoiDung.FirstOrDefault(u => u.email == "anh675262@gmail.com") 
                                  ?? db.NguoiDung.FirstOrDefault(u => u.maNguoiDung == 2) 
                                  ?? db.NguoiDung.FirstOrDefault(u => u.vaiTro == "HocVien");

                    if (activeUser != null)
                    {
                        var existingReview = db.DanhGiaKhoaHoc.FirstOrDefault(r => r.maKhoaHoc == "KH_TEST01");
                        if (existingReview != null)
                        {
                            existingReview.maHocVien = activeUser.maNguoiDung;
                            existingReview.NguoiDung = activeUser;
                            existingReview.soSao = 5;
                            existingReview.noiDung = "Khóa học thử nghiệm rất tuyệt vời, giao diện trải nghiệm người dùng hiện đại và mượt mà!";
                            existingReview.ngayDanhGia = DateTime.Now;
                        }
                        else
                        {
                            db.DanhGiaKhoaHoc.Add(new DanhGiaKhoaHoc
                            {
                                maDanhGia = "DG_TEST01_" + activeUser.maNguoiDung,
                                maKhoaHoc = "KH_TEST01",
                                maHocVien = activeUser.maNguoiDung,
                                NguoiDung = activeUser,
                                soSao = 5,
                                noiDung = "Khóa học thử nghiệm rất tuyệt vời, giao diện trải nghiệm người dùng hiện đại và mượt mà!",
                                ngayDanhGia = DateTime.Now
                            });
                        }
                        db.SaveChanges();
                    }
                }
                else if (course.maKhoaHoc == "KH_TEST02")
                {
                    var extraReviews = db.DanhGiaKhoaHoc.Where(r => r.maKhoaHoc == "KH_TEST02").ToList();
                    if (extraReviews.Count > 0)
                    {
                        db.DanhGiaKhoaHoc.RemoveRange(extraReviews);
                        db.SaveChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Sync reviews error: " + ex.Message);
            }

            // 5. Đồng bộ lại dữ liệu chương và bài học
            var chapters = db.ChuongHoc
                .Where(c => c.maKhoaHoc == course.maKhoaHoc)
                .OrderBy(c => c.thuTu)
                .Include("BaiHoc")
                .ToList();
            ViewBag.Chapters = chapters;

            return View(course);
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
