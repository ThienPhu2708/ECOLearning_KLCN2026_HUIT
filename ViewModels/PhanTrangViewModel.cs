using System;
using System.Collections.Generic;
using System.Linq;

namespace ECOLearning.ViewModels
{
    /// <summary>
    /// Phân trang dùng chung cho các trang danh sách khóa học (/danh-muc/{slug}, /Course, /Learning/MyCourses),
    /// hiển thị bằng Views/Shared/_Pagination.cshtml. Số trang ngoài khoảng (âm, quá trang cuối) được kéo về
    /// trang hợp lệ gần nhất để link cũ / link sửa tay không ra trang trống.
    /// </summary>
    public class PhanTrangViewModel
    {
        public const int SoKhoaMoiTrang = 12;

        public PhanTrangViewModel(int trang, int tongSo, int kichThuoc = SoKhoaMoiTrang)
        {
            KichThuoc = kichThuoc > 0 ? kichThuoc : SoKhoaMoiTrang;
            TongSo = Math.Max(0, tongSo);
            Trang = Math.Min(Math.Max(1, trang), TongSoTrang);
        }

        public int Trang { get; private set; }
        public int KichThuoc { get; private set; }
        public int TongSo { get; private set; }

        /// <summary>Tên tham số số trang trên query string.</summary>
        public string ThamSo { get; set; } = "trang";

        /// <summary>Id phần tử đầu danh sách: bấm sang trang thì nhảy tới đó thay vì đầu trang web.</summary>
        public string Neo { get; set; }

        /// <summary>Tên đối tượng trong dòng tóm tắt ("Hiển thị 13–24 trong 40 khóa học").</summary>
        public string DonVi { get; set; } = "khóa học";

        public int TongSoTrang => Math.Max(1, (TongSo + KichThuoc - 1) / KichThuoc);
        public int BoQua => (Trang - 1) * KichThuoc;
        public int TuSo => TongSo == 0 ? 0 : BoQua + 1;
        public int DenSo => Math.Min(TongSo, BoQua + KichThuoc);
        public bool CoNhieuTrang => TongSoTrang > 1;

        public List<T> CatTrang<T>(IEnumerable<T> danhSach)
        {
            return danhSach.Skip(BoQua).Take(KichThuoc).ToList();
        }

        /// <summary>
        /// Các số trang cần hiện, 0 là dấu "…": luôn có trang đầu, trang cuối và một trang mỗi bên trang hiện tại.
        /// Khoảng trống chỉ thiếu đúng một trang thì hiện luôn số trang đó (dấu "…" không ngắn hơn số).
        /// </summary>
        public List<int> DaySoTrang()
        {
            var hien = new SortedSet<int> { 1, TongSoTrang };
            for (int t = Trang - 1; t <= Trang + 1; t++)
            {
                if (t >= 1 && t <= TongSoTrang) hien.Add(t);
            }

            var ketQua = new List<int>();
            int truoc = 0;
            foreach (int t in hien)
            {
                if (t - truoc == 2) ketQua.Add(t - 1);
                else if (t - truoc > 2) ketQua.Add(0);
                ketQua.Add(t);
                truoc = t;
            }
            return ketQua;
        }
    }
}
