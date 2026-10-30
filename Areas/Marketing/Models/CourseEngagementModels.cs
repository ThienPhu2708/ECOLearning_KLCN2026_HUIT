using System;
using System.Collections.Generic;

namespace ECOLearning.Areas.Marketing.Models
{
    /// <summary>
    /// Tương tác của học viên với khóa học (lượt xem, lượt nhấp, yêu thích) trong một kỳ. TuongTacKhoaHoc chỉ lưu tổng cộng dồn
    /// theo từng cặp học viên và khóa kèm thời điểm gần nhất, nên "trong kỳ" nghĩa là các học viên có hoạt động gần nhất rơi vào kỳ.
    /// </summary>
    public class CourseEngagementViewModel
    {
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
        public string ThongBaoLoi { get; set; }

        public int TongLuotXem { get; set; }
        public int TongLuotClick { get; set; }
        public int TongLuotThich { get; set; }
        public int TongDangKy { get; set; }

        /// <summary>Mọi khóa có tương tác trong kỳ, xếp theo điểm tương tác giảm dần (dùng cho biểu đồ top và gợi ý).</summary>
        public List<CourseEngagementRow> Rows { get; set; } = new List<CourseEngagementRow>();

        // Bộ lọc, sắp xếp và phân trang của bảng chi tiết
        public const int KichThuocTrang = 10;
        public string TuKhoa { get; set; }
        public string DanhMuc { get; set; }

        /// <summary>"co" = đã có ưu đãi, "chua" = chưa có, rỗng = tất cả.</summary>
        public string UuDai { get; set; }

        /// <summary>diem (mặc định), xem, nhap, thich, dangky, chuyendoi.</summary>
        public string SapXep { get; set; }
        public int Trang { get; set; } = 1;
        public int SoTrang { get; set; } = 1;
        public int TongDong { get; set; }
        public List<CourseEngagementRow> TrangRows { get; set; } = new List<CourseEngagementRow>();
        public List<string> DanhMucs { get; set; } = new List<string>();

        /// <summary>Khóa tương tác cao nhưng chưa có ưu đãi áp dụng riêng.</summary>
        public List<CourseEngagementRow> GoiY { get; set; } = new List<CourseEngagementRow>();
    }

    public class CourseEngagementRow
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string TenDanhMuc { get; set; }
        public decimal GiaBan { get; set; }

        public int LuotXem { get; set; }
        public int LuotClick { get; set; }
        public int LuotThich { get; set; }

        /// <summary>Số học viên có xem khóa trong kỳ.</summary>
        public int HocVienXem { get; set; }

        /// <summary>Trong số đó, số học viên đã đăng ký khóa trong kỳ.</summary>
        public int HocVienDangKy { get; set; }

        /// <summary>Tổng lượt đăng ký khóa trong kỳ (kể cả học viên chưa từng xem qua hệ thống).</summary>
        public int DangKyTrongKy { get; set; }

        /// <summary>Có ưu đãi đang chạy áp dụng riêng cho khóa (giảm trực tiếp, hoặc khuyến mãi theo khóa / danh mục).</summary>
        public bool CoUuDai { get; set; }

        /// <summary>Điểm tương tác = lượt xem + 2 × lượt nhấp + 3 × lượt yêu thích.</summary>
        public int Diem => LuotXem + 2 * LuotClick + 3 * LuotThich;

        /// <summary>Tỷ lệ chuyển đổi = học viên xem rồi đăng ký / học viên xem.</summary>
        public decimal? TyLeChuyenDoi => HocVienXem > 0 ? Math.Round((decimal)HocVienDangKy / HocVienXem * 100, 1) : (decimal?)null;
    }
}
