using System.Collections.Generic;

namespace ECOLearning.ViewModels
{
    /// <summary>Một lĩnh vực (danh mục) trong bước chọn lĩnh vực quan tâm.</summary>
    public class LinhVucQuanTamItem
    {
        public string MaDanhMuc { get; set; }
        public string TenDanhMuc { get; set; }

        /// <summary>Số khóa đang bán trong danh mục (0 khi danh mục chưa có khóa, hoặc khi chỉ đọc lựa chọn đã lưu).</summary>
        public int SoKhoaHoc { get; set; }
    }

    /// <summary>Hộp thoại "Bạn quan tâm lĩnh vực nào?" (Views/Shared/_InterestPicker.cshtml).</summary>
    public class InterestPickerViewModel
    {
        public List<LinhVucQuanTamItem> LuaChon { get; set; } = new List<LinhVucQuanTamItem>();
        public HashSet<string> DaChon { get; set; } = new HashSet<string>();

        /// <summary>Tự mở khi vào trang (học viên mới, chưa trả lời lần nào). Sai: chỉ mở khi bấm "Chọn / Đổi lĩnh vực".</summary>
        public bool TuDongMo { get; set; }

        public int SoLuaChonToiDa { get; set; } = 3;

        /// <summary>Số lĩnh vực hiện sẵn; các lĩnh vực còn lại ẩn sau nút "Xem thêm".</summary>
        public const int SoHienThiBanDau = 4;

        /// <summary>Hộp thoại tự mở sau khoảng này kể từ khi vào trang, để học viên kịp nhìn trang trước.</summary>
        public const int GiayChoTuDongMo = 5;
    }
}
