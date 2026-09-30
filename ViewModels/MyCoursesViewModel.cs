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

    public class MyCoursesViewModel
    {
        public List<MyCourseItemViewModel> OngoingCourses { get; set; } = new List<MyCourseItemViewModel>();
        public List<MyCourseItemViewModel> CompletedCourses { get; set; } = new List<MyCourseItemViewModel>();
        public int TotalCount => (OngoingCourses != null ? OngoingCourses.Count : 0) + (CompletedCourses != null ? CompletedCourses.Count : 0);
    }
}
