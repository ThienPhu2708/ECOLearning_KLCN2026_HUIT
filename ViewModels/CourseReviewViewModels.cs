using System;
using System.Collections.Generic;

namespace ECOLearning.ViewModels
{
    /// <summary>Một đánh giá của học viên kèm bình chọn "hữu ích" và phản hồi của giáo viên (Services/CourseReviewService.cs).</summary>
    public class DanhGiaItem
    {
        public string MaDanhGia { get; set; }
        public int MaHocVien { get; set; }
        public string TenHocVien { get; set; }
        public string AnhDaiDien { get; set; }
        public int SoSao { get; set; }
        public string NoiDung { get; set; }
        public DateTime? NgayDanhGia { get; set; }

        public int SoHuuIch { get; set; }
        public int SoKhongHuuIch { get; set; }

        /// <summary>Bình chọn của người đang xem: 1 = hữu ích, 0 = không hữu ích, null = chưa chọn.</summary>
        public int? BinhChonCuaToi { get; set; }

        public string PhanHoiNoiDung { get; set; }
        public DateTime? PhanHoiNgayTao { get; set; }
        public DateTime? PhanHoiNgayCapNhat { get; set; }
        public bool CoPhanHoi => !string.IsNullOrEmpty(PhanHoiNoiDung);
    }

    /// <summary>Danh sách đánh giá của một khóa (mục ở trang chi tiết và trang "Tất cả đánh giá").</summary>
    public class DanhSachDanhGiaViewModel
    {
        /// <summary>Số đánh giá hiện ở trang chi tiết; nhiều hơn thì có nút "Xem tất cả".</summary>
        public const int SoHienThiTrenTrangChiTiet = 5;
        public const int SoMoiTrang = 10;

        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string AnhBia { get; set; }

        // Giáo viên của khóa (người duy nhất được phản hồi đánh giá)
        public int MaGiaoVien { get; set; }
        public string TenGiaoVien { get; set; }
        public string AnhGiaoVien { get; set; }

        /// <summary>Người đang xem (0 = khách).</summary>
        public int MaNguoiXem { get; set; }
        public bool LaGiaoVienKhoaHoc => MaNguoiXem > 0 && MaNguoiXem == MaGiaoVien;

        /// <summary>Bảng bình chọn / phản hồi đã có (migration 011); chưa có thì ẩn các nút này.</summary>
        public bool CoTuongTac { get; set; }

        public int TongSo { get; set; }
        public double DiemTrungBinh { get; set; }

        /// <summary>SoTheoSao[s] = số đánh giá s sao (s = 1..5).</summary>
        public int[] SoTheoSao { get; set; } = new int[6];

        public List<DanhGiaItem> Items { get; set; } = new List<DanhGiaItem>();

        // Bộ lọc của trang "Tất cả đánh giá"
        public int? LocSao { get; set; }
        public string TuKhoa { get; set; }

        /// <summary>moinhat (mặc định) / huuich / caonhat / thapnhat.</summary>
        public string SapXep { get; set; } = "moinhat";
        public int Trang { get; set; } = 1;
        public int SoTrang { get; set; } = 1;

        /// <summary>Số đánh giá khớp bộ lọc (để phân trang).</summary>
        public int SoKetQua { get; set; }

        public bool CoXemTatCa => TongSo > SoHienThiTrenTrangChiTiet;

        public int PhanTramSao(int sao) => TongSo > 0 ? (int)Math.Round(SoTheoSao[sao] * 100.0 / TongSo) : 0;
    }

    /// <summary>Mục "Giảng viên" + "Các khóa học khác của …" ở trang chi tiết khóa học (Views/Course/_InstructorInfo.cshtml).</summary>
    public class GiangVienKhoaHocViewModel
    {
        public const int SoKhoaKhacHienThi = 3;

        public InstructorProfileViewModel HoSo { get; set; }
        public string MaKhoaHocDangXem { get; set; }

        /// <summary>Các khóa đang bán khác của giáo viên (trừ khóa đang xem).</summary>
        public List<KhoaHocCuaGiaoVienItem> KhoaKhac { get; set; } = new List<KhoaHocCuaGiaoVienItem>();
    }

    /// <summary>Tham số cho partial một đánh giá (Views/Course/_ReviewItem.cshtml).</summary>
    public class DanhGiaItemModel
    {
        public DanhGiaItem Item { get; set; }
        public DanhSachDanhGiaViewModel DanhSach { get; set; }
    }
}
