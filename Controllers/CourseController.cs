using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    public class CourseController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: Course (UC4: Tìm kiếm & Lọc nâng cao đa tiêu chí)
        // Mở cho khách (link "Khám phá" trên menu khi chưa đăng nhập): khách xem và lọc được mọi khóa đang bán. Nút thả tim tự ẩn
        // với khách (_FavoriteButton); mua / thêm giỏ / vào học ở trang chi tiết mới yêu cầu đăng nhập.
        public ActionResult Index(string search, string category, bool? isFree, bool? isPaid, string level, double? minRating, string sort, int trang = 1)
        {
            // Bỏ khóa người dùng đã sở hữu (đã mua qua giỏ hàng hoặc đăng ký khóa 0đ, cả hai đều ghi DangKyKhoaHoc):
            // khóa đã sở hữu chỉ hiện ở "Khóa học của tôi"
            int userId = (Session["UserId"] as int?) ?? 0;
            var query = db.KhoaHoc
                .Include(k => k.NguoiDung)
                .Include(k => k.DanhMuc)
                .Include(k => k.DanhGiaKhoaHoc)
                .Where(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active")
                .Where(k => !db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == k.maKhoaHoc && d.maHocVien == userId))
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
                query = query.Where(k => k.giaBan == 0);
            }
            else if (isPaid.HasValue && isPaid.Value && (isFree == null || !isFree.Value))
            {
                query = query.Where(k => k.giaBan > 0);
            }

            // 4. Lọc theo Cấp độ (Mọi cấp độ, Cơ bản, Trung cấp, Nâng cao)
            if (!string.IsNullOrWhiteSpace(level))
            {
                query = query.Where(k => k.capDo == level);
            }

            // 5. Lọc theo Đánh giá tối thiểu (Rating >= minRating): làm trong SQL để đếm và phân trang đúng
            if (minRating.HasValue && minRating.Value > 0)
            {
                double diemToiThieu = minRating.Value;
                query = query.Where(k => k.DanhGiaKhoaHoc.Any() && k.DanhGiaKhoaHoc.Average(d => (double)d.soSao) >= diemToiThieu);
            }

            // 6. Sắp xếp; thêm mã khóa làm khóa phụ để thứ tự ổn định giữa các trang (giá bằng nhau không bị nhảy)
            IOrderedQueryable<KhoaHoc> sorted;
            if (sort == "price-asc")
            {
                sorted = query.OrderBy(k => k.giaBan).ThenByDescending(k => k.maKhoaHoc);
            }
            else if (sort == "price-desc")
            {
                sorted = query.OrderByDescending(k => k.giaBan).ThenByDescending(k => k.maKhoaHoc);
            }
            else
            {
                sorted = query.OrderByDescending(k => k.maKhoaHoc);
            }

            // 7. Phân trang 12 khóa / trang: đếm rồi chỉ lấy khóa của trang đang xem (OFFSET/FETCH trong SQL)
            var phanTrang = new PhanTrangViewModel(trang, query.Count()) { Neo = "ds-khoa-hoc" };
            var courseList = sorted.Skip(phanTrang.BoQua).Take(phanTrang.KichThuoc).ToList();

            // Pass filter metadata to View
            // Danh mục tạm ngưng không hiện trong bộ lọc; khóa học thuộc danh mục đó vẫn hiển thị bình thường
            ViewBag.Categories = db.DanhMuc.Where(d => d.trangThai == null || d.trangThai == true).ToList();
            ViewBag.SearchTerm = search;
            ViewBag.SelectedCategory = category;
            ViewBag.IsFree = isFree;
            ViewBag.IsPaid = isPaid;
            ViewBag.SelectedLevel = level;
            ViewBag.MinRating = minRating;
            ViewBag.Sort = sort;
            ViewBag.PhanTrang = phanTrang;
            ViewBag.SoKhoaDaSoHuu = userId > 0 ? db.DangKyKhoaHoc.Count(d => d.maHocVien == userId) : 0;

            // Số học viên mỗi khóa: một truy vấn GROUP BY nhẹ (xem ghi chú ở HomeController.Index)
            // (chỉ đếm cho các khóa của trang đang xem)
            var maKhoaTrangNay = courseList.Select(k => k.maKhoaHoc).ToList();
            ViewBag.EnrollCounts = db.DangKyKhoaHoc
                .Where(d => maKhoaTrangNay.Contains(d.maKhoaHoc))
                .GroupBy(d => d.maKhoaHoc)
                .Select(g => new { MaKhoaHoc = g.Key, SoHocVien = g.Count() })
                .ToDictionary(x => x.MaKhoaHoc, x => x.SoHocVien);

            return View(courseList);
        }

        // GET: Course/Details/5 (Chi tiết khóa học & Video Preview)
        public ActionResult Details(string id)
        {
            // Mở cho khách (tìm kiếm công khai): chỉ Mua / Thêm giỏ / Thả tim / Vào học mới yêu cầu đăng nhập

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
            int currentUserId = (Session["UserId"] as int?) ?? 0;
            var currentUser = Session["User"] as NguoiDung;
            if (currentUser == null && currentUserId > 0)
            {
                currentUser = db.NguoiDung.Find(currentUserId);
                Session["User"] = SessionNguoiDung.BanSao(currentUser);
            }
            var sessionRole = Session["Role"]?.ToString();
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
            // Giáo viên xem khóa học của chính mình: không hiển thị nút mua/đăng ký
            ViewBag.IsOwner = currentUserId > 0 && course.maGiaoVien == currentUserId;

            // Gợi ý khóa học: ghi nhận lượt xem của người học (bỏ qua khóa của chính mình và Admin)
            if (course.maGiaoVien != currentUserId && (sessionRole == "HocVien" || sessionRole == "GiaoVien"))
            {
                new RecommendationService(db).RecordView(Session, currentUserId, course.maKhoaHoc);
            }

            var cartList = Session["Cart"] as List<ECOLearning.ViewModels.CartItemViewModel>;
            bool isInCart = cartList != null && cartList.Any(i => i.CourseId == course.maKhoaHoc);
            ViewBag.IsInCart = isInCart;

            // Cuối trang: "Thường xuyên được mua cùng nhau" + "Học viên khác cũng mua" (Item-based trên dữ liệu mua)
            try
            {
                var lienQuan = new RelatedCourseService(db).LayChoKhoaHoc(course.maKhoaHoc, currentUserId);
                lienQuan.LaKhach = currentUserId <= 0 && !User.Identity.IsAuthenticated;
                if (cartList != null) lienQuan.TrongGio = new HashSet<string>(cartList.Select(i => i.CourseId));
                ViewBag.KhoaHocLienQuan = lienQuan;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Related courses error: " + ex.Message);
            }

            // Giảng viên (cùng cách tính với trang hồ sơ /User) + các khóa đang bán khác của giảng viên
            try
            {
                var hoSo = new InstructorProfileService(db).LayHoSo(course.maGiaoVien);
                if (hoSo != null)
                {
                    ViewBag.GiangVien = new ECOLearning.ViewModels.GiangVienKhoaHocViewModel
                    {
                        HoSo = hoSo,
                        MaKhoaHocDangXem = course.maKhoaHoc,
                        KhoaKhac = hoSo.DanhSachKhoaHoc.Where(k => k.Course.maKhoaHoc != course.maKhoaHoc).ToList()
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Instructor info error: " + ex.Message);
            }

            // Đánh giá: 5 đánh giá mới nhất, "Bạn thấy hữu ích?", phản hồi của giáo viên; nhiều hơn 5 thì có nút "Xem tất cả"
            ViewBag.DanhGia = new CourseReviewService(db).LayDanhSach(course, currentUserId, null, null, "moinhat", 1,
                ECOLearning.ViewModels.DanhSachDanhGiaViewModel.SoHienThiTrenTrangChiTiet);

            // 3. Dữ liệu chương và bài học
            var chapters = db.ChuongHoc
                .Where(c => c.maKhoaHoc == course.maKhoaHoc)
                .OrderBy(c => c.thuTu)
                .Include("BaiHoc")
                .ToList();
            ViewBag.Chapters = chapters;

            var unchapteredLessons = db.BaiHoc
                .Where(b => b.maChuong == null && b.maBaiHoc.StartsWith(course.maKhoaHoc + "_"))
                .OrderBy(b => b.thuTu)
                .ToList();
            ViewBag.UnchapteredLessons = unchapteredLessons;

            return View(course);
        }

        // GET: Course/Reviews/{id}?sao=&q=&sapXep=&trang= — trang "Tất cả đánh giá" (mở cho cả khách)
        // ValidateInput(false): ô tìm kiếm có thể chứa ký tự như "<"; mọi chỗ xuất ra đều được Razor encode.
        [ValidateInput(false)]
        public ActionResult Reviews(string id, int? sao, string q, string sapXep, int trang = 1)
        {
            if (string.IsNullOrWhiteSpace(id)) return HttpNotFound();
            var course = db.KhoaHoc.Include(k => k.NguoiDung).FirstOrDefault(k => k.maKhoaHoc == id);
            if (course == null) return HttpNotFound();

            int currentUserId = (Session["UserId"] as int?) ?? 0;
            bool laChuHoacAdmin = Session["Role"]?.ToString() == "Admin" || (currentUserId > 0 && course.maGiaoVien == currentUserId);
            if (!laChuHoacAdmin && course.trangThai != "Đã xuất bản" && course.trangThai != "Đã duyệt" && course.trangThai != "Active")
            {
                return HttpNotFound();
            }

            var vm = new CourseReviewService(db).LayDanhSach(course, currentUserId, sao, q, sapXep, trang,
                ECOLearning.ViewModels.DanhSachDanhGiaViewModel.SoMoiTrang);
            return View(vm);
        }

        // POST: Course/EnrollFree
        [HttpPost]
        [StaffReadOnly] // Admin / Marketing chỉ xem (StaffViewMode)
        public ActionResult EnrollFree(string courseId)
        {
            int currentUserId = (Session["UserId"] as int?) ?? 0;
            var currentUser = Session["User"] as NguoiDung;
            if (currentUser == null && currentUserId > 0)
            {
                currentUser = db.NguoiDung.Find(currentUserId);
                Session["User"] = SessionNguoiDung.BanSao(currentUser);
            }

            if (currentUser == null)
            {
                return Json(new
                {
                    success = false,
                    requireLogin = true,
                    message = "Vui lòng đăng nhập để đăng ký khóa học!"
                });
            }

            if (string.IsNullOrEmpty(courseId))
            {
                return Json(new { success = false, message = "Mã khóa học không hợp lệ!" });
            }

            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == courseId);
            if (course == null)
            {
                return Json(new { success = false, message = "Không tìm thấy khóa học!" });
            }

            // Chỉ đăng ký được khóa học đang mở bán (không nhận khóa học tạm khóa / khóa vĩnh viễn / chưa xuất bản)
            if (!ECOLearning.Services.CourseStatus.IsPublished(course.trangThai))
            {
                return Json(new { success = false, message = "Khóa học hiện không mở đăng ký!" });
            }

            if (course.maGiaoVien == currentUser.maNguoiDung)
            {
                return Json(new { success = false, message = "Bạn là giáo viên của khóa học này nên không thể tự đăng ký học!" });
            }

            // Kiểm tra giá: chỉ cho phép đăng ký trực tiếp khóa miễn phí (giá khóa học = giaBan)
            decimal effectivePrice = course.giaBan;

            if (effectivePrice > 0)
            {
                return Json(new
                {
                    success = false,
                    message = "Khóa học này có học phí. Vui lòng thêm vào giỏ hàng và tiến hành thanh toán!"
                });
            }

            // Kiểm tra đã đăng ký chưa
            var existingDK = db.DangKyKhoaHoc.FirstOrDefault(d => d.maHocVien == currentUser.maNguoiDung && d.maKhoaHoc == courseId);
            if (existingDK != null)
            {
                return Json(new
                {
                    success = true,
                    isAlreadyEnrolled = true,
                    message = "Bạn đã đăng ký khóa học này rồi! Đang chuyển đến không gian học tập...",
                    redirectUrl = Url.Action("MyCourses", "Learning")
                });
            }

            // Tạo bản ghi đăng ký mới (maDangKy tối đa 20 ký tự)
            string dkId = "DK" + DateTime.Now.ToString("yyMMddHHmmss") + (new Random().Next(1000, 9999));
            var dk = new DangKyKhoaHoc
            {
                maDangKy = dkId,
                ngayDangKy = DateTime.Now,
                trangThaiDK = "Đã kích hoạt",
                maHocVien = currentUser.maNguoiDung,
                maKhoaHoc = courseId
            };
            db.DangKyKhoaHoc.Add(dk);
            db.SaveChanges();

            return Json(new
            {
                success = true,
                message = "Đăng ký khóa học thành công! Đang chuyển đến trang Khóa học của tôi...",
                redirectUrl = Url.Action("MyCourses", "Learning")
            });
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
