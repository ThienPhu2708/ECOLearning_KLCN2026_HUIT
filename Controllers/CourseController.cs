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
                .Where(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active")
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
                var firstCourse = db.KhoaHoc.FirstOrDefault(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active");
                if (firstCourse != null)
                {
                    id = firstCourse.maKhoaHoc;
                }
            }

            // 1. Tìm kiếm khóa học theo Mã hoặc Tên khóa học
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
                    .FirstOrDefault(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active");
            }

            if (course == null)
            {
                return HttpNotFound();
            }

            // Cho phép Giảng viên tạo khóa học hoặc Admin xem thử khóa học chưa duyệt
            var currentUser = Session["User"] as NguoiDung;
            var sessionRole = Session["Role"]?.ToString();
            int currentUserId = (Session["UserId"] as int?) ?? 0;
            bool isOwnerOrAdmin = (sessionRole == "Admin" || (course.maGiaoVien == currentUserId && currentUserId > 0));

            if (!isOwnerOrAdmin && course.trangThai != "Đã xuất bản" && course.trangThai != "Đã duyệt" && course.trangThai != "Active")
            {
                return HttpNotFound();
            }

            // 2. Kiểm tra trạng thái học viên đăng ký
            bool isEnrolled = false;
            if (currentUser != null)
            {
                isEnrolled = db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == course.maKhoaHoc && d.maHocVien == currentUser.maNguoiDung);
            }
            ViewBag.IsEnrolled = isEnrolled;

            var cartList = Session["Cart"] as List<ECOLearning.ViewModels.CartItemViewModel>;
            bool isInCart = cartList != null && cartList.Any(i => i.CourseId == course.maKhoaHoc);
            ViewBag.IsInCart = isInCart;

            // 3. Dữ liệu chương và bài học
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
