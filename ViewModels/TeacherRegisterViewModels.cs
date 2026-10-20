namespace ECOLearning.ViewModels
{
    /// <summary>
    /// Giá trị đã nhập ở form đăng ký giáo viên (Areas/Instructor/Views/TeacherApplication/Register.cshtml):
    /// giữ lại khi nộp bị báo lỗi, điền sẵn khi nộp lại hồ sơ đã bị từ chối. (Tệp chứng chỉ trình duyệt không cho điền sẵn.)
    /// </summary>
    public class TeacherRegisterFormViewModel
    {
        public string Specialization { get; set; }
        public string BioExperience { get; set; }

        /// <summary>Mã ngân hàng trong Services/BankDirectory.</summary>
        public string BankCode { get; set; }
        public string AccountNumber { get; set; }
        public string AccountName { get; set; }

        /// <summary>Ghi chú khi ngân hàng được đoán từ tên cũ gõ tự do (hồ sơ bị từ chối nộp lại).</summary>
        public string BankNote { get; set; }
    }

    /// <summary>Ô chọn ngân hàng dùng chung (Views/Shared/_BankPicker.cshtml, assets/js/bank-picker.js).</summary>
    public class BankPickerViewModel
    {
        /// <summary>id của ô tìm (để label for= trỏ tới).</summary>
        public string Id { get; set; } = "bank-search";

        /// <summary>Tên trường ẩn gửi lên server (mã ngân hàng).</summary>
        public string Name { get; set; } = "bankCode";
        public string SelectedCode { get; set; }

        /// <summary>Lớp CSS của ô nhập theo trang (tr-input / form-control).</summary>
        public string InputClass { get; set; }
        public string Error { get; set; }

        /// <summary>Ghi chú dưới ô (vd. ngân hàng được nhận ra từ tên cũ, cần kiểm tra lại).</summary>
        public string Note { get; set; }

        /// <summary>id ô được focus sau khi chọn ngân hàng.</summary>
        public string NextFieldId { get; set; }
    }
}
