using System.Collections.Generic;
using ECOLearning.Models;

namespace ECOLearning.ViewModels
{
    /// <summary>Một thẻ khóa học (Views/Shared/_CourseCard.cshtml): số học viên / đánh giá đã đếm sẵn.</summary>
    public class CourseCardViewModel
    {
        public KhoaHoc Course { get; set; }
        public int SoHocVien { get; set; }
        public int SoDanhGia { get; set; }
        public double DiemDanhGia { get; set; }

        public decimal GiaHienTai => Course.giaKhuyenMai.HasValue ? Course.giaKhuyenMai.Value : Course.giaBan;
    }

    /// <summary>Danh mục / cấp độ kèm số khóa, dùng cho các nút lọc trên trang danh mục.</summary>
    public class CategoryLinkViewModel
    {
        public string Ten { get; set; }
        public string Slug { get; set; }
        public int SoKhoaHoc { get; set; }
    }

    /// <summary>Trang danh mục /danh-muc/{slug}: một trang dùng chung cho mọi danh mục.</summary>
    public class CategoryPageViewModel
    {
        public string MaDanhMuc { get; set; }
        public string TenDanhMuc { get; set; }
        public string MoTa { get; set; }
        public string Slug { get; set; }

        /// <summary>Số khóa trong danh mục (chưa lọc cấp độ, đã bỏ khóa người dùng sở hữu).</summary>
        public int TongSoKhoaHoc { get; set; }
        public int SoKhoaDaSoHuu { get; set; }

        /// <summary>Các cấp độ có trong danh mục (Slug = giá trị capDo dùng trên query string).</summary>
        public List<CategoryLinkViewModel> CapDo { get; set; } = new List<CategoryLinkViewModel>();
        public string CapDoDangChon { get; set; }
        public string SapXep { get; set; }

        public List<CourseCardViewModel> Items { get; set; } = new List<CourseCardViewModel>();

        /// <summary>Các danh mục khác (chuyển nhanh sang trang danh mục khác).</summary>
        public List<CategoryLinkViewModel> DanhMucKhac { get; set; } = new List<CategoryLinkViewModel>();
    }
}
