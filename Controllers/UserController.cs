using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using ECOLearning.Models;
using ECOLearning.Services;
using ECOLearning.ViewModels;

namespace ECOLearning.Controllers
{
    public class UserController : Controller
    {
        private QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1();

        // GET: /User or /User/Index/{id} or /User?id={id}
        public ActionResult Index(int? id)
        {
            int targetUserId = id ?? (Session["UserId"] as int? ?? 0);
            if (targetUserId <= 0)
            {
                // Nếu không truyền id và không có session, lấy giảng viên đầu tiên có khóa học
                var firstTeacher = db.KhoaHoc
                    .Where(k => k.trangThai == "Đã xuất bản" || k.trangThai == "Đã duyệt" || k.trangThai == "Active")
                    .Select(k => k.maGiaoVien)
                    .FirstOrDefault();

                if (firstTeacher > 0)
                {
                    targetUserId = firstTeacher;
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }

            if (!KhachXemDuoc(targetUserId))
            {
                return HttpNotFound("Không tìm thấy thông tin giảng viên.");
            }

            var viewModel = BuildProfile(targetUserId);
            if (viewModel == null)
            {
                return HttpNotFound("Không tìm thấy thông tin giảng viên.");
            }

            return View(viewModel);
        }

        // GET: /User/Courses/{id}?sort=... — toàn bộ khóa học của một giáo viên (nút "Xem tất cả" ở trang hồ sơ)
        public ActionResult Courses(int? id, string sort)
        {
            if (!id.HasValue || id.Value <= 0)
            {
                return RedirectToAction("Index", "Home");
            }

            if (!KhachXemDuoc(id.Value))
            {
                return HttpNotFound("Không tìm thấy thông tin giảng viên.");
            }

            var viewModel = BuildProfile(id.Value);
            if (viewModel == null)
            {
                return HttpNotFound("Không tìm thấy thông tin giảng viên.");
            }

            switch (sort)
            {
                case "danh-gia":
                    viewModel.DanhSachKhoaHoc = viewModel.DanhSachKhoaHoc
                        .OrderByDescending(k => k.DiemDanhGia).ThenByDescending(k => k.SoDanhGia).ToList();
                    break;
                case "hoc-vien":
                    viewModel.DanhSachKhoaHoc = viewModel.DanhSachKhoaHoc.OrderByDescending(k => k.SoHocVien).ToList();
                    break;
                case "gia-tang":
                    viewModel.DanhSachKhoaHoc = viewModel.DanhSachKhoaHoc.OrderBy(k => k.GiaHienTai).ToList();
                    break;
                default:
                    sort = "mac-dinh";
                    break;
            }
            ViewBag.Sort = sort;

            return View(viewModel);
        }

        /// <summary>Thông tin hồ sơ + thống kê + danh sách khóa học của một giáo viên; null nếu không có người dùng.</summary>
        /// <summary>
        /// Hồ sơ mở công khai (khách đi từ kết quả tìm kiếm) nhưng chỉ với tài khoản giáo viên đang hoạt động;
        /// người đã đăng nhập xem như trước. Email / số điện thoại chỉ hiện khi đã đăng nhập (Views/User/Index.cshtml).
        /// </summary>
        private bool KhachXemDuoc(int targetUserId)
        {
            if (User.Identity.IsAuthenticated || Session["UserId"] != null) return true;
            return db.NguoiDung.Any(u => u.maNguoiDung == targetUserId && u.vaiTro == "GiaoVien" && u.trangThai != false);
        }

        private InstructorProfileViewModel BuildProfile(int targetUserId)
        {
            var vm = new InstructorProfileService(db).LayHoSo(targetUserId);
            if (vm != null)
            {
                if (string.IsNullOrWhiteSpace(vm.ChuyenMon)) vm.ChuyenMon = "Giảng viên chuyên môn E-Learning";
                if (string.IsNullOrWhiteSpace(vm.KinhNghiem)) vm.KinhNghiem = "Chưa cập nhật tiểu sử kinh nghiệm giảng dạy.";
            }
            return vm;
        }

        // GET: /User/Details/{id} (Alias)
        public ActionResult Details(int? id)
        {
            return Index(id);
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
