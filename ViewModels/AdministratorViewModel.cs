using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ECOLearning.Services;

namespace ECOLearning.ViewModels
{
    public sealed class NewAdministratorViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
        public string FullName { get; set; }
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự.")]
        public string Email { get; set; }
        [Required]
        public string Role { get; set; } = AccountAdminService.AdminKiemDuyetNoiDung;
        public bool ConfirmAdmin { get; set; }
    }
    public sealed class AdministratorViewModel
    {
        public bool Installed { get; set; }
        public int Total { get; set; }
        public int Active { get; set; }
        public int CurrentUserId { get; set; }
        public string Query { get; set; }
        public string Status { get; set; }
        public PhanTrangViewModel Pagination { get; set; }
        public List<NguoiDungAdminItem> Administrators { get; set; } = new List<NguoiDungAdminItem>();
        public NewAdministratorViewModel NewAdmin { get; set; } = new NewAdministratorViewModel();
    }
}
