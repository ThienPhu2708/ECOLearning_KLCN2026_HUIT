using System.Collections.Generic;
using System.Linq;

namespace ECOLearning.ViewModels
{
    /// <summary>Hai mục cuối trang chi tiết khóa học (Services/RelatedCourseService.cs).</summary>
    public class KhoaHocLienQuanViewModel
    {
        /// <summary>"Thường xuyên được mua cùng nhau": Item-based trên các lần mua cách nhau ≤ 30 ngày của cùng học viên.</summary>
        public List<KhoaHocLienQuanItem> MuaCungNhau { get; set; } = new List<KhoaHocLienQuanItem>();

        /// <summary>"Học viên khác cũng mua": Item-based trên toàn bộ lịch sử mua (không lặp khóa của mục trên).</summary>
        public List<KhoaHocLienQuanItem> CungMua { get; set; } = new List<KhoaHocLienQuanItem>();

        public bool CoNoiDung => MuaCungNhau.Count > 0 || CungMua.Count > 0;

        /// <summary>Khóa đang xem, đứng đầu combo "mua cùng nhau" (null nếu người xem không mua được: đã sở hữu / là giáo viên).</summary>
        public KhoaHocLienQuanItem KhoaDangXem { get; set; }

        /// <summary>Mã các khóa đang có trong giỏ (session) để hiện sẵn "Đã trong giỏ".</summary>
        public HashSet<string> TrongGio { get; set; } = new HashSet<string>();

        /// <summary>Khách chưa đăng nhập: nút thêm giỏ dẫn tới trang đăng nhập (trang chi tiết không cho khách dùng giỏ).</summary>
        public bool LaKhach { get; set; }

        /// <summary>Combo "Thường xuyên được mua cùng nhau": khóa đang xem + các khóa hay mua cùng.</summary>
        public List<KhoaHocLienQuanItem> GoiMuaCung
        {
            get
            {
                var ds = new List<KhoaHocLienQuanItem>();
                if (KhoaDangXem != null) ds.Add(KhoaDangXem);
                ds.AddRange(MuaCungNhau);
                return ds;
            }
        }

        public decimal TongGia => GoiMuaCung.Sum(x => x.GiaHienTai);
        public decimal TongGiaGoc => GoiMuaCung.Sum(x => x.The.Course.giaBan);
    }

    public class KhoaHocLienQuanItem
    {
        public CourseCardViewModel The { get; set; }

        /// <summary>Số học viên đã mua cả khóa đang xem và khóa này (trong điều kiện của mục).</summary>
        public int SoNguoiChung { get; set; }

        /// <summary>Độ tương đồng cosine giữa hai khóa (0–1).</summary>
        public double DoTuongDong { get; set; }

        /// <summary>Tổng thời lượng video các bài học (giây).</summary>
        public int ThoiLuongGiay { get; set; }

        /// <summary>Lần duyệt gần nhất (hiện "Đã cập nhật: M/yyyy").</summary>
        public System.DateTime? NgayCapNhat { get; set; }

        /// <summary>Giá bán hiệu lực (đã trừ giảm trực tiếp), cùng giá thanh toán.</summary>
        public decimal GiaHienTai => The.GiaHienTai;

        /// <summary>"5,5 giờ" / "45 phút" / null khi chưa có thời lượng.</summary>
        public string ThoiLuongHienThi
        {
            get
            {
                if (ThoiLuongGiay <= 0) return null;
                if (ThoiLuongGiay < 3600) return System.Math.Max(1, ThoiLuongGiay / 60) + " phút";
                return (System.Math.Round(ThoiLuongGiay / 3600.0 * 2) / 2).ToString("0.#") + " giờ";
            }
        }
    }
}
