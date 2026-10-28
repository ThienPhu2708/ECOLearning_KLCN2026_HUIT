using System;
using System.Collections.Generic;
using ECOLearning.Models;

namespace ECOLearning.ViewModels
{
    /// <summary>Một khóa trong mục "Tiếp tục học" ở trang chủ (Services/HomeSectionService.TiepTucHoc).</summary>
    public class TiepTucHocItem
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string AnhBia { get; set; }
        public string TenGiaoVien { get; set; }
        public int PhanTram { get; set; }
        public int SoBaiXong { get; set; }
        public int TongSoBai { get; set; }
        /// <summary>Bài mở khi bấm "Học tiếp": bài chưa xong vừa xem gần nhất, hoặc bài đầu tiên chưa xong.</summary>
        public string MaBaiHoc { get; set; }
        public string TenBaiHoc { get; set; }
        public int SoThuTuBai { get; set; }
        /// <summary>Vị trí đang xem dở trong bài (giây), 0 nếu chưa xem.</summary>
        public int ViTriGiay { get; set; }
        public DateTime? LanHocGanNhat { get; set; }
        public bool DaBatDau => LanHocGanNhat.HasValue;
    }

    /// <summary>Mục "Bán chạy" ở trang chủ (Services/HomeSectionService.BanChay).</summary>
    public class BanChayResult
    {
        public List<KhoaHoc> KhoaHoc { get; set; } = new List<KhoaHoc>();
        /// <summary>"tuần này" / "30 ngày qua" / "toàn thời gian": kỳ dùng để xếp hạng.</summary>
        public string TenKy { get; set; }
        /// <summary>Lượt đăng ký trong kỳ của từng khóa hiển thị.</summary>
        public Dictionary<string, int> LuotTrongKy { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> TongHocVien { get; set; } = new Dictionary<string, int>();
        /// <summary>3 khóa đầu có lượt đăng ký trong kỳ, gắn nhãn "Bán chạy".</summary>
        public HashSet<string> TopBanChay { get; set; } = new HashSet<string>();
    }

    /// <summary>Mục "Vì bạn đã học X" ở trang chủ (Services/HomeSectionService.ViBanDaHoc).</summary>
    public class GoiYTheoKhoaHoc
    {
        public string MaKhoaHocMoc { get; set; }
        public string TenKhoaHocMoc { get; set; }
        public List<CourseCardViewModel> KhoaHoc { get; set; } = new List<CourseCardViewModel>();
    }

    /// <summary>Một khóa trong mục "Khóa học yêu thích của bạn" ở trang chủ (Services/HomeSectionService.YeuThich).</summary>
    public class YeuThichItem
    {
        public KhoaHoc Course { get; set; }
        public int SoDanhGia { get; set; }
        public double DiemDanhGia { get; set; }
    }
}
