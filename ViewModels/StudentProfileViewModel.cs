using System;
using System.ComponentModel.DataAnnotations;

namespace ECOLearning.ViewModels
{
    public class StudentProfileViewModel
    {
        public int UserId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập Họ và tên")]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [Display(Name = "Email")]
        public string Email { get; set; }

        [Display(Name = "Số điện thoại")]
        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại không đúng định dạng (Ví dụ: 0912345678)")]
        public string Phone { get; set; }

        [Display(Name = "Giới tính")]
        public string Gender { get; set; }

        [Display(Name = "Ngày sinh")]
        [DataType(DataType.Date)]
        [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
        public DateTime? BirthDate { get; set; }

        [Display(Name = "Ảnh đại diện")]
        public string AvatarUrl { get; set; }

        public string RoleName { get; set; }

        // Thống kê tiến độ học tập
        public int EnrolledCoursesCount { get; set; }
        public int CompletedCoursesCount { get; set; }
        public int TotalCertificates { get; set; }

        // Đổi mật khẩu
        [Display(Name = "Mật khẩu hiện tại")]
        public string CurrentPassword { get; set; }

        [Display(Name = "Mật khẩu mới")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Mật khẩu mới phải từ 8 ký tự trở lên")]
        public string NewPassword { get; set; }

        [Display(Name = "Xác nhận mật khẩu mới")]
        [Compare("NewPassword", ErrorMessage = "Xác nhận mật khẩu mới không khớp")]
        public string ConfirmPassword { get; set; }

        // Thông tin hồ sơ giáo viên (dành riêng cho vai trò GiaoVien)
        public bool IsTeacher { get; set; }
        public string ApplicationId { get; set; }

        [Display(Name = "Chuyên môn giảng dạy")]
        public string Specialization { get; set; }

        [Display(Name = "Kinh nghiệm & Giới thiệu")]
        public string BioExperience { get; set; }

        [Display(Name = "Ngân hàng")]
        public string BankName { get; set; }

        [Display(Name = "Số tài khoản")]
        public string AccountNumber { get; set; }

        [Display(Name = "Tên chủ tài khoản")]
        public string AccountName { get; set; }

        [Display(Name = "Tệp chứng chỉ")]
        public string CertificateFilePath { get; set; }

        [Display(Name = "Tên tệp chứng chỉ")]
        public string CertificateFileName { get; set; }

        public string TeacherStatus { get; set; }
    }
}
