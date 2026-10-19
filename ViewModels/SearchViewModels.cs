using System.Collections.Generic;

namespace ECOLearning.ViewModels
{
    /// <summary>Trang kết quả /tim-kiem?q=...</summary>
    public class TimKiemViewModel
    {
        public string Q { get; set; }

        // Bộ lọc
        public string DanhMuc { get; set; }
        public string CapDo { get; set; }

        /// <summary>"mienphi" / "cophi" / null.</summary>
        public string Gia { get; set; }
        public double? Diem { get; set; }

        /// <summary>lienquan (mặc định) / phobien / danhgia / giatang / giagiam.</summary>
        public string SapXep { get; set; }
        public int Trang { get; set; }
        public int SoTrang { get; set; }
        public int TongSo { get; set; }

        public List<CourseCardViewModel> KetQua { get; set; } = new List<CourseCardViewModel>();

        /// <summary>Không khớp đủ mọi từ nên hiện kết quả khớp một phần.</summary>
        public bool KhopMotPhan { get; set; }

        /// <summary>"Có phải bạn muốn tìm: ..." khi không có kết quả và từ khóa có vẻ gõ sai.</summary>
        public string GoiYSuaLoi { get; set; }

        /// <summary>Danh mục có tên khớp từ khóa (hiện thành chip phía trên kết quả).</summary>
        public List<CategoryLinkViewModel> DanhMucLienQuan { get; set; } = new List<CategoryLinkViewModel>();

        /// <summary>Đếm theo danh mục / cấp độ trên tập kết quả (trước khi lọc theo chính nó).</summary>
        public List<BoLocMuc> BoLocDanhMuc { get; set; } = new List<BoLocMuc>();
        public List<BoLocMuc> BoLocCapDo { get; set; } = new List<BoLocMuc>();

        /// <summary>Từ khóa được tìm nhiều (khi chưa nhập từ khóa hoặc không có kết quả).</summary>
        public List<string> TuKhoaPhoBien { get; set; } = new List<string>();

        /// <summary>Khóa phổ biến (khi không có kết quả).</summary>
        public List<CourseCardViewModel> KhoaPhoBien { get; set; } = new List<CourseCardViewModel>();

        public bool CoBoLoc => !string.IsNullOrEmpty(DanhMuc) || !string.IsNullOrEmpty(CapDo) || !string.IsNullOrEmpty(Gia) || Diem.HasValue;
    }

    public class BoLocMuc
    {
        public string Ma { get; set; }
        public string Ten { get; set; }
        public int SoLuong { get; set; }
    }

    /// <summary>JSON trả về cho dropdown gợi ý ở thanh tìm kiếm.</summary>
    public class GoiYTimKiemViewModel
    {
        public List<GoiYTuKhoa> TuKhoa { get; set; } = new List<GoiYTuKhoa>();
        public List<GoiYKhoaHoc> KhoaHoc { get; set; } = new List<GoiYKhoaHoc>();
        public List<GoiYGiaoVien> GiaoVien { get; set; } = new List<GoiYGiaoVien>();
    }

    public class GoiYTuKhoa
    {
        public string Text { get; set; }
        public string Url { get; set; }
    }

    public class GoiYKhoaHoc
    {
        public string Ten { get; set; }
        public string GiaoVien { get; set; }
        public string Anh { get; set; }
        public string Url { get; set; }
    }

    public class GoiYGiaoVien
    {
        public string Ten { get; set; }
        public string Anh { get; set; }
        public int SoKhoaHoc { get; set; }
        public string Url { get; set; }
    }
}
