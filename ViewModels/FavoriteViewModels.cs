using System.Collections.Generic;
using ECOLearning.Models;

namespace ECOLearning.ViewModels
{
    /// <summary>Một khóa trên trang "Khóa học đã tim".</summary>
    public class FavoriteCourseItem
    {
        public KhoaHoc Course { get; set; }
        public int SoDanhGia { get; set; }
        public double DiemDanhGia { get; set; }
    }

    /// <summary>Trang quản lý "Khóa học đã tim".</summary>
    public class FavoritesPageViewModel
    {
        public List<FavoriteCourseItem> Items { get; set; } = new List<FavoriteCourseItem>();

        /// <summary>false khi chưa chạy migration 005 (chưa có bảng TuongTacKhoaHoc).</summary>
        public bool DaKhoiTao { get; set; } = true;
    }

    /// <summary>Nút tim: khóa nào, ai dạy (ẩn nút với khóa của chính mình), kiểu hiển thị.</summary>
    public class FavoriteButtonModel
    {
        public string CourseId { get; set; }
        public int TeacherId { get; set; }

        /// <summary>"icon": tim tròn ở góc ảnh bìa thẻ khóa học; "square": nút vuông cạnh nút giỏ hàng / đăng ký ở trang chi tiết.</summary>
        public string Variant { get; set; } = "icon";
    }

    /// <summary>
    /// Menu "Khóa học của tôi" trên header, 2 tầng như menu "Khám phá":
    /// tầng 1 = Khóa học đã đăng ký / Khóa học đã tim, tầng 2 = các khóa gần đây của nhóm đang chọn.
    /// </summary>
    public class MyCoursesMenuViewModel
    {
        public List<ExploreCourseViewModel> KhoaDaDangKy { get; set; } = new List<ExploreCourseViewModel>();
        public List<ExploreCourseViewModel> KhoaDaTim { get; set; } = new List<ExploreCourseViewModel>();
        public int SoKhoaDaDangKy { get; set; }
        public int SoKhoaDaTim { get; set; }
    }
}
