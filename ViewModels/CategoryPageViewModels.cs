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

        /// <summary>Giá bán hiệu lực: giá gốc đã trừ giảm trực tiếp (CourseDiscountService); giá gốc là Course.giaBan.</summary>
        public decimal GiaHienTai => ECOLearning.Services.CourseDiscountService.Tinh(Course).Gia;

        /// <summary>Nhãn góc ảnh do nơi gọi chọn (vd. "Mới", "Đánh giá cao"); null thì thẻ tự chọn như cũ.</summary>
        public string Nhan { get; set; }
        /// <summary>"moi" / "danhgia" / "banchay": màu của nhãn.</summary>
        public string NhanKieu { get; set; }
        /// <summary>Dòng ghi chú nhỏ dưới tên giáo viên (vd. "Ra mắt 19/10/2026", "Đã xem hôm qua").</summary>
        public string GhiChu { get; set; }
        /// <summary>true: không tự gắn "Top Trending" / "Đánh giá cao", chỉ hiện <see cref="Nhan"/> (các mục ở trang chủ).</summary>
        public bool ChiNhanChiDinh { get; set; }
    }

    /// <summary>Danh mục / cấp độ kèm số khóa, dùng cho các nút lọc trên trang danh mục.</summary>
    public class CategoryLinkViewModel
    {
        public string Ten { get; set; }
        public string Slug { get; set; }
        public int SoKhoaHoc { get; set; }
    }

    /// <summary>
    /// Biểu tượng + màu của thẻ danh mục, chọn theo vị trí danh mục (sắp theo mã). Trang chủ ("Khám phá theo
    /// lĩnh vực") và trang Tất cả lĩnh vực dùng chung để một danh mục luôn mang cùng biểu tượng ở hai nơi.
    /// </summary>
    public class IconDanhMuc
    {
        public string Icon { get; private set; }
        public string Bg { get; private set; }
        public string Color { get; private set; }

        private static readonly IconDanhMuc[] BangMau =
        {
            new IconDanhMuc { Icon = "bi-laptop", Bg = "#EEF2FF", Color = "#434CE8" },
            new IconDanhMuc { Icon = "bi-palette", Bg = "#FFF7ED", Color = "#EA580C" },
            new IconDanhMuc { Icon = "bi-bar-chart-line", Bg = "#ECFDF5", Color = "#10B981" },
            new IconDanhMuc { Icon = "bi-rocket-takeoff", Bg = "#F0F9FF", Color = "#0284C7" },
            new IconDanhMuc { Icon = "bi-code-slash", Bg = "#F3E8FF", Color = "#9333EA" },
            new IconDanhMuc { Icon = "bi-megaphone", Bg = "#FCE7F3", Color = "#DB2777" },
            new IconDanhMuc { Icon = "bi-briefcase", Bg = "#FEF3C7", Color = "#D97706" }
        };

        public static IconDanhMuc TheoViTri(int viTri)
        {
            return BangMau[((viTri % BangMau.Length) + BangMau.Length) % BangMau.Length];
        }
    }

    /// <summary>Một thẻ lĩnh vực trên trang /danh-muc (Tất cả lĩnh vực).</summary>
    public class LinhVucItem
    {
        public string MaDanhMuc { get; set; }
        public string Ten { get; set; }
        public string MoTa { get; set; }
        public string Slug { get; set; }
        public IconDanhMuc Icon { get; set; }

        /// <summary>Ảnh minh họa Admin tải lên (DanhMuc.anhMinhHoa), null nếu chưa có.</summary>
        public string AnhMinhHoa { get; set; }

        /// <summary>Khóa đang bán, đã bỏ khóa người dùng sở hữu (khớp số khóa trên trang danh mục).</summary>
        public int SoKhoaHoc { get; set; }
        public int SoHocVien { get; set; }
        public int SoGiangVien { get; set; }
    }

    /// <summary>Trang /danh-muc: mọi lĩnh vực đang hoạt động, bấm thẻ để sang /danh-muc/{slug}.</summary>
    public class TatCaLinhVucViewModel
    {
        public List<LinhVucItem> Items { get; set; } = new List<LinhVucItem>();
        public int TongSoKhoaHoc { get; set; }
    }

    /// <summary>Trang danh mục /danh-muc/{slug}: một trang dùng chung cho mọi danh mục.</summary>
    public class CategoryPageViewModel
    {
        public string MaDanhMuc { get; set; }
        public string TenDanhMuc { get; set; }
        public string MoTa { get; set; }
        public string Slug { get; set; }

        /// <summary>Ảnh minh họa Admin tải lên (DanhMuc.anhMinhHoa, migration 012), null nếu chưa có.</summary>
        public string AnhMinhHoa { get; set; }

        /// <summary>Số khóa trong danh mục (chưa lọc cấp độ, đã bỏ khóa người dùng sở hữu).</summary>
        public int TongSoKhoaHoc { get; set; }
        public int SoKhoaDaSoHuu { get; set; }

        /// <summary>Các cấp độ có trong danh mục (Slug = giá trị capDo dùng trên query string).</summary>
        public List<CategoryLinkViewModel> CapDo { get; set; } = new List<CategoryLinkViewModel>();
        public string CapDoDangChon { get; set; }
        public string SapXep { get; set; }

        /// <summary>Các khóa của trang đang xem (đã cắt theo PhanTrang).</summary>
        public List<CourseCardViewModel> Items { get; set; } = new List<CourseCardViewModel>();
        public PhanTrangViewModel PhanTrang { get; set; }

        /// <summary>Các danh mục khác (chuyển nhanh sang trang danh mục khác).</summary>
        public List<CategoryLinkViewModel> DanhMucKhac { get; set; } = new List<CategoryLinkViewModel>();
    }
}
