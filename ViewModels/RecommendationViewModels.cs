using System;
using System.Collections.Generic;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.ViewModels
{
    /// <summary>
    /// Một khóa học được gợi ý: thứ tự và nguồn do thuật toán lọc cộng tác (stored procedure) trả về,
    /// thông tin khóa học nạp bằng Entity Framework để hiển thị thẻ.
    /// </summary>
    public class RecommendedCourseViewModel
    {
        public KhoaHoc Course { get; set; }

        /// <summary>Số lượt và điểm đánh giá trung bình (tính sẵn, không nạp cả danh sách DanhGiaKhoaHoc).</summary>
        public int SoDanhGia { get; set; }
        public double DiemDanhGia { get; set; }
        public int ThuTu { get; set; }
        public double DiemDuDoan { get; set; }

        /// <summary>LocCongTac hoặc PhoBien (bù khi chưa đủ gợi ý / người dùng mới).</summary>
        public string Nguon { get; set; }

        /// <summary>Khóa đã học đóng góp nhiều nhất vào gợi ý ("Vì bạn đã học ...").</summary>
        public string MaKhoaHocLyDo { get; set; }
        public string TenKhoaHocLyDo { get; set; }

        public bool LaLocCongTac => Nguon == RecommendationService.NguonLocCongTac;
    }

    /// <summary>Mục "Vì bạn đã xem X" ở trang chủ: các khóa tương tự nhất với khóa người dùng xem gần nhất.</summary>
    public class GoiYTheoKhoaVuaXemViewModel
    {
        public string MaKhoaHocMoc { get; set; }
        public string TenKhoaHocMoc { get; set; }
        public List<RecommendedCourseViewModel> Items { get; set; } = new List<RecommendedCourseViewModel>();
    }

    /// <summary>Trang "Khóa học gợi ý".</summary>
    public class RecommendationPageViewModel
    {
        public List<RecommendedCourseViewModel> Items { get; set; } = new List<RecommendedCourseViewModel>();

        /// <summary>Đã chạy Database/Migrations/005_GoiY_LocCongTac.sql hay chưa.</summary>
        public bool DaKhoiTao { get; set; }

        public string ThongBaoLoi { get; set; }

        /// <summary>Số khóa người dùng đã tương tác (đăng ký, học, đánh giá, xem...).</summary>
        public int SoKhoaDaTuongTac { get; set; }

        /// <summary>Lần cuối job tính lại ma trận độ tương đồng.</summary>
        public DateTime? MaTranCapNhatLuc { get; set; }

        public DateTime TinhLuc { get; set; } = DateTime.Now;

        public int SoGoiYLocCongTac => Items.Count(i => i.LaLocCongTac);
    }
}
