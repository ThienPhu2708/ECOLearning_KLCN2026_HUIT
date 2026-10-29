using System;
using System.Collections.Generic;

namespace ECOLearning.ViewModels
{
    /// <summary>Ảnh quảng cáo đang chạy, dùng cho popup trang chủ (Coupon/QuangCao) và API ứng dụng.</summary>
    public class QuangCaoItem
    {
        /// <summary>ChienDich hoặc ChuongTrinh (khớp tên action của UuDaiController).</summary>
        public string Loai { get; set; }
        public string Ma { get; set; }
        public string Ten { get; set; }
        public string Anh { get; set; }
        public DateTime NgayKetThuc { get; set; }
    }

    /// <summary>Ảnh và cờ popup hiện tại của một chiến dịch / chương trình (form Marketing).</summary>
    public class QuangCaoHienTai
    {
        public string Anh { get; set; }
        public bool HienPopup { get; set; }
    }

    /// <summary>Trang "Ưu đãi" mở ra khi bấm ảnh quảng cáo: một chiến dịch (nhiều chương trình) hoặc một chương trình.</summary>
    public class UuDaiTrangViewModel
    {
        public string Loai { get; set; }
        public string Ma { get; set; }
        public string TieuDe { get; set; }
        public string MoTa { get; set; }
        public string Anh { get; set; }
        public DateTime NgayBatDau { get; set; }
        public DateTime NgayKetThuc { get; set; }
        public List<UuDaiTrangMuc> Muc { get; set; } = new List<UuDaiTrangMuc>();
        public bool DaDangNhap { get; set; }
    }

    /// <summary>Một chương trình khuyến mãi trên trang ưu đãi.</summary>
    public class UuDaiTrangMuc
    {
        public string MaKhuyenMai { get; set; }
        public string Ten { get; set; }
        public string GiaTri { get; set; }
        public string GhiChu { get; set; }
        public List<string> DieuKien { get; set; } = new List<string>();
        public DateTime NgayKetThuc { get; set; }
        public bool LaGiamTrucTiep { get; set; }
        public bool LaThanThiet { get; set; }

        /// <summary>Tổng giá trị các khóa được áp dụng trong giỏ phải đạt mức này (null = không yêu cầu).</summary>
        public decimal? DonToiThieu { get; set; }

        /// <summary>Mã coupon công khai; null với giảm trực tiếp (không cần mã).</summary>
        public string MaCode { get; set; }

        /// <summary>Học viên đang đăng nhập lưu được mã này vào ví ngay.</summary>
        public bool CoTheLuu { get; set; }
        public bool DaLuu { get; set; }

        /// <summary>Ưu đãi thân thiết mà học viên chưa đạt: hiện phần còn thiếu thay vì nút lưu.</summary>
        public string ThongBao { get; set; }

        /// <summary>Áp dụng cho mọi khóa học (không liệt kê khóa).</summary>
        public bool ApDungMoiKhoa { get; set; }
        public int TongSoKhoa { get; set; }
        public List<UuDaiKhoa> Khoa { get; set; } = new List<UuDaiKhoa>();
    }

    public class UuDaiKhoa
    {
        public string MaKhoaHoc { get; set; }
        public string Ten { get; set; }
        public string AnhBia { get; set; }
        public decimal GiaGoc { get; set; }
        public decimal Gia { get; set; }
        public int PhanTram { get; set; }
        public bool DangGiam => Gia < GiaGoc;
    }
}
