using System.Collections.Generic;

namespace ECOLearning.ViewModels
{
    /// <summary>Menu "Khám phá" trên header: Danh mục → Cấp độ → Khóa học.</summary>
    public class ExploreCategoryViewModel
    {
        public string MaDanhMuc { get; set; }
        public string TenDanhMuc { get; set; }
        public int SoKhoaHoc { get; set; }
        public List<ExploreLevelViewModel> CapDo { get; set; } = new List<ExploreLevelViewModel>();
    }

    public class ExploreLevelViewModel
    {
        /// <summary>Giá trị KhoaHoc.capDo; null khi khóa học chưa phân cấp độ.</summary>
        public string CapDo { get; set; }
        public string TenHienThi => string.IsNullOrWhiteSpace(CapDo) ? "Chưa phân cấp độ" : CapDo;
        public int SoKhoaHoc { get; set; }
        public List<ExploreCourseViewModel> KhoaHoc { get; set; } = new List<ExploreCourseViewModel>();
    }

    public class ExploreCourseViewModel
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
    }
}
