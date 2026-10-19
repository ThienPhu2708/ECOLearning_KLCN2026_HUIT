using System;
using System.Collections.Generic;

namespace ECOLearning.ViewModels
{
    public class MyCourseItemViewModel
    {
        public string MaKhoaHoc { get; set; }
        public string TenKhoaHoc { get; set; }
        public string AnhBia { get; set; }
        public string TenDanhMuc { get; set; }
        public string TenGiangVien { get; set; }
        public DateTime? NgayDangKy { get; set; }
        public DateTime? NgayHoanThanh { get; set; }
        public int TongSoBaiHoc { get; set; }
        public int SoBaiDaHoanThanh { get; set; }
        public int PhanTramTienDo { get; set; }
        public string TenBaiHocTiepTheo { get; set; }
        public string MaBaiHocTiepTheo { get; set; }
        public bool DaHoanThanh => PhanTramTienDo >= 100;
    }

    /// <summary>Trang Khóa học của tôi: hai tab (?tab=dang-hoc | hoan-thanh), mỗi tab 12 khóa / trang.</summary>
    public class MyCoursesViewModel
    {
        public const string TabDangHoc = "dang-hoc";
        public const string TabHoanThanh = "hoan-thanh";

        public string Tab { get; set; } = TabDangHoc;
        public bool LaTabHoanThanh => Tab == TabHoanThanh;

        public int SoDangHoc { get; set; }
        public int SoHoanThanh { get; set; }
        public int TotalCount => SoDangHoc + SoHoanThanh;

        /// <summary>Các khóa của tab và trang đang xem.</summary>
        public List<MyCourseItemViewModel> Items { get; set; } = new List<MyCourseItemViewModel>();
        public PhanTrangViewModel PhanTrang { get; set; }
    }
}
