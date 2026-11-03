using System;
using System.Collections.Generic;

namespace ECOLearning.ViewModels
{
    public class HoiThoaiItemViewModel
    {
        public int MaHoiThoai { get; set; }
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string AnhBiaKhoaHoc { get; set; }
        public int MaHocVien { get; set; }
        public string TenHocVien { get; set; }
        public string AnhDaiDienHocVien { get; set; }
        public int MaGiaoVien { get; set; }
        public string TenGiaoVien { get; set; }
        public string AnhDaiDienGiaoVien { get; set; }
        public string TinCuoiTomTat { get; set; }
        public DateTime? TinCuoiLuc { get; set; }
        public int SoTinChuaDoc { get; set; }
        public string TrangThai { get; set; } // Mo, ChiDoc, BiBaoCao
        public bool DaTraLoi { get; set; }
        public int MaNguoiGuiCuoi { get; set; }
    }

    public class TinNhanItemViewModel
    {
        public int MaTinNhan { get; set; }
        public int MaHoiThoai { get; set; }
        public int MaNguoiGui { get; set; }
        public string TenNguoiGui { get; set; }
        public string AnhDaiDien { get; set; }
        public string NoiDung { get; set; }
        public DateTime NgayGui { get; set; }
        public bool DaThuHoi { get; set; }
        public bool LaCuaToi { get; set; }
        public bool CoTheThuHoi { get; set; }
        public int RecallSeconds { get { return DaThuHoi ? 0 : Math.Max(0, (int)Math.Ceiling((NgayGui.AddMinutes(5) - DateTime.Now).TotalSeconds)); } }
        public string ImageUrl { get { return !DaThuHoi && ECOLearning.Services.ChatService.TenAnh(NoiDung) != null ? "/Chat/Image/" + MaTinNhan : null; } }
    }

    public class ThongTinHocVienChatViewModel
    {
        public int MaHocVien { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
        public string AnhDaiDien { get; set; }
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public DateTime? NgayMua { get; set; }
        public int TienDoPhanTram { get; set; }
        public string BaiDangHoc { get; set; }
        public decimal? DiemQuiz { get; set; }
        public string TrangThaiQuiz { get; set; }
        public string TrangThaiChungChi { get; set; }
    }

    public class ChatRoomViewModel
    {
        public HoiThoaiItemViewModel HoiThoai { get; set; }
        public List<TinNhanItemViewModel> DanhSachTinNhan { get; set; } = new List<TinNhanItemViewModel>();
        public ThongTinHocVienChatViewModel ThongTinHocVien { get; set; }
        public bool LaGiaoVien { get; set; }
        public int CurrentUserId { get; set; }
    }

    public class StudentChatIndexViewModel
    {
        public List<HoiThoaiItemViewModel> DanhSachHoiThoai { get; set; } = new List<HoiThoaiItemViewModel>();
        public int? SelectedHoiThoaiId { get; set; }
        public ChatRoomViewModel PhongChatHienTai { get; set; }
    }

    public class InstructorChatIndexViewModel
    {
        public List<HoiThoaiItemViewModel> DanhSachHoiThoai { get; set; } = new List<HoiThoaiItemViewModel>();
        public string FilterCourseId { get; set; }
        public string FilterStatus { get; set; } // all, unread, unanswered
        public int? SelectedHoiThoaiId { get; set; }
        public ChatRoomViewModel PhongChatHienTai { get; set; }
        public List<CourseFilterItem> DanhSachKhoaHoc { get; set; } = new List<CourseFilterItem>();
    }

    public class CourseFilterItem
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
    }
}
