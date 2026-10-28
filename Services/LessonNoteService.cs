using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Ghi chú bài học của học viên (bảng GhiChuBaiHoc, migration 020). Dùng chung cho trang học trên web (LearningController)
    /// và API ứng dụng (LearningApiController) để ghi chú tạo ở đâu cũng thấy ở nơi còn lại.
    /// Người gọi kiểm tra quyền học khóa (đã ghi danh, khóa chưa bị khóa vĩnh viễn) và bài thuộc khóa; service chỉ cho học viên
    /// đọc / sửa / xóa ghi chú của chính mình.
    /// </summary>
    public class LessonNoteService
    {
        public const int DoDaiToiDa = 1000;
        public const int SoGhiChuToiDaMoiBai = 200;
        private const int GiayToiDa = 24 * 3600;

        public class GhiChu
        {
            public int MaGhiChu { get; set; }
            public string MaBaiHoc { get; set; }
            public int ThoiDiemGiay { get; set; }
            public string NoiDung { get; set; }
            public DateTime NgayTao { get; set; }
            public DateTime? NgayCapNhat { get; set; }
        }

        public class KetQua
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            /// <summary>Mã lỗi cho API: NOTE_EMPTY, NOTE_TOO_LONG, NOTE_LIMIT, NOTE_NOT_FOUND, NOTES_NOT_INSTALLED.</summary>
            public string Code { get; set; }
            public GhiChu GhiChu { get; set; }

            public static KetQua Loi(string code, string message) => new KetQua { Code = code, Message = message };
        }

        private const string Cot = "maGhiChu AS MaGhiChu, maBaiHoc AS MaBaiHoc, thoiDiemGiay AS ThoiDiemGiay, noiDung AS NoiDung, ngayTao AS NgayTao, ngayCapNhat AS NgayCapNhat";
        private static bool? daCaiDat;
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public LessonNoteService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        /// <summary>Đã chạy migration 020 (có bảng GhiChuBaiHoc). Chỉ ghi nhớ khi đã có bảng để chạy migration xong không cần khởi động lại.</summary>
        public bool DaCaiDat()
        {
            if (daCaiDat == true) return true;
            bool co = db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.GhiChuBaiHoc', N'U') IS NULL THEN 0 ELSE 1 END").First() == 1;
            if (co) daCaiDat = true;
            return co;
        }

        private static KetQua ChuaCaiDat() => KetQua.Loi("NOTES_NOT_INSTALLED", "Chức năng ghi chú chưa sẵn sàng (cần chạy Database/Migrations/020_GhiChuBaiHoc.sql).");

        /// <summary>Ghi chú của học viên trong một bài, theo mốc thời gian video.</summary>
        public List<GhiChu> LayTheoBai(int maHocVien, string maBaiHoc)
        {
            if (!DaCaiDat()) return new List<GhiChu>();
            return db.Database.SqlQuery<GhiChu>(
                "SELECT " + Cot + " FROM dbo.GhiChuBaiHoc WHERE maHocVien = @hv AND maBaiHoc = @bai ORDER BY thoiDiemGiay, maGhiChu",
                new SqlParameter("@hv", maHocVien), new SqlParameter("@bai", maBaiHoc)).ToList();
        }

        /// <summary>Ghi chú của học viên trong các bài đã cho (cả khóa), theo thứ tự bài rồi mốc thời gian.</summary>
        public List<GhiChu> LayTheoCacBai(int maHocVien, IList<string> maBaiTheoThuTu)
        {
            if (!DaCaiDat() || maBaiTheoThuTu.Count == 0) return new List<GhiChu>();
            var thuTu = maBaiTheoThuTu.Select((m, i) => new { m, i }).GroupBy(x => x.m).ToDictionary(g => g.Key, g => g.First().i);
            return db.Database.SqlQuery<GhiChu>(
                    "SELECT " + Cot + " FROM dbo.GhiChuBaiHoc WHERE maHocVien = @hv", new SqlParameter("@hv", maHocVien)).ToList()
                .Where(g => thuTu.ContainsKey(g.MaBaiHoc))
                .OrderBy(g => thuTu[g.MaBaiHoc]).ThenBy(g => g.ThoiDiemGiay).ThenBy(g => g.MaGhiChu)
                .ToList();
        }

        public GhiChu Lay(int maHocVien, int maGhiChu)
        {
            if (!DaCaiDat()) return null;
            return db.Database.SqlQuery<GhiChu>("SELECT " + Cot + " FROM dbo.GhiChuBaiHoc WHERE maGhiChu = @id AND maHocVien = @hv",
                new SqlParameter("@id", maGhiChu), new SqlParameter("@hv", maHocVien)).FirstOrDefault();
        }

        public KetQua Them(int maHocVien, string maBaiHoc, double giay, string noiDung)
        {
            if (!DaCaiDat()) return ChuaCaiDat();
            var loi = KiemTraNoiDung(ref noiDung);
            if (loi != null) return loi;
            int soHienCo = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.GhiChuBaiHoc WHERE maHocVien = @hv AND maBaiHoc = @bai",
                new SqlParameter("@hv", maHocVien), new SqlParameter("@bai", maBaiHoc)).First();
            if (soHienCo >= SoGhiChuToiDaMoiBai)
                return KetQua.Loi("NOTE_LIMIT", "Mỗi bài học có tối đa " + SoGhiChuToiDaMoiBai + " ghi chú.");

            int id = db.Database.SqlQuery<int>(
                "INSERT INTO dbo.GhiChuBaiHoc (maHocVien, maBaiHoc, thoiDiemGiay, noiDung) VALUES (@hv, @bai, @giay, @nd); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new SqlParameter("@hv", maHocVien), new SqlParameter("@bai", maBaiHoc),
                new SqlParameter("@giay", ChuanHoaGiay(giay)), new SqlParameter("@nd", noiDung)).First();
            return new KetQua { Success = true, Message = "Đã lưu ghi chú.", GhiChu = Lay(maHocVien, id) };
        }

        /// <summary>Sửa nội dung (và mốc thời gian nếu có) ghi chú của chính học viên.</summary>
        public KetQua Sua(int maHocVien, int maGhiChu, string noiDung, double? giay)
        {
            if (!DaCaiDat()) return ChuaCaiDat();
            var cu = Lay(maHocVien, maGhiChu);
            if (cu == null) return KetQua.Loi("NOTE_NOT_FOUND", "Không tìm thấy ghi chú.");
            var loi = KiemTraNoiDung(ref noiDung);
            if (loi != null) return loi;
            db.Database.ExecuteSqlCommand(
                "UPDATE dbo.GhiChuBaiHoc SET noiDung = @nd, thoiDiemGiay = @giay, ngayCapNhat = GETDATE() WHERE maGhiChu = @id AND maHocVien = @hv",
                new SqlParameter("@nd", noiDung), new SqlParameter("@giay", giay.HasValue ? ChuanHoaGiay(giay.Value) : cu.ThoiDiemGiay),
                new SqlParameter("@id", maGhiChu), new SqlParameter("@hv", maHocVien));
            return new KetQua { Success = true, Message = "Đã cập nhật ghi chú.", GhiChu = Lay(maHocVien, maGhiChu) };
        }

        public KetQua Xoa(int maHocVien, int maGhiChu)
        {
            if (!DaCaiDat()) return ChuaCaiDat();
            int n = db.Database.ExecuteSqlCommand("DELETE FROM dbo.GhiChuBaiHoc WHERE maGhiChu = @id AND maHocVien = @hv",
                new SqlParameter("@id", maGhiChu), new SqlParameter("@hv", maHocVien));
            return n > 0 ? new KetQua { Success = true, Message = "Đã xóa ghi chú." } : KetQua.Loi("NOTE_NOT_FOUND", "Không tìm thấy ghi chú.");
        }

        /// <summary>
        /// Đưa ghi chú cũ đang nằm trong localStorage của trình duyệt lên máy chủ (một lần, khi mở bài học).
        /// Bỏ qua ghi chú rỗng và ghi chú đã có (cùng mốc giây, cùng nội dung). Trả về số ghi chú đã thêm.
        /// </summary>
        public int NhapTuTrinhDuyet(int maHocVien, string maBaiHoc, IEnumerable<KeyValuePair<double, string>> ghiChu)
        {
            if (!DaCaiDat()) return 0;
            var daCo = new HashSet<string>(LayTheoBai(maHocVien, maBaiHoc).Select(g => g.ThoiDiemGiay + "|" + g.NoiDung));
            int them = 0;
            foreach (var g in ghiChu.Take(SoGhiChuToiDaMoiBai))
            {
                string nd = g.Value;
                if (KiemTraNoiDung(ref nd) != null) continue;
                if (!daCo.Add(ChuanHoaGiay(g.Key) + "|" + nd)) continue;
                if (Them(maHocVien, maBaiHoc, g.Key, nd).Success) them++;
            }
            return them;
        }

        private static KetQua KiemTraNoiDung(ref string noiDung)
        {
            noiDung = (noiDung ?? "").Trim();
            if (noiDung.Length == 0) return KetQua.Loi("NOTE_EMPTY", "Vui lòng nhập nội dung ghi chú.");
            if (noiDung.Length > DoDaiToiDa) return KetQua.Loi("NOTE_TOO_LONG", "Ghi chú tối đa " + DoDaiToiDa + " ký tự.");
            return null;
        }

        private static int ChuanHoaGiay(double giay)
        {
            if (double.IsNaN(giay) || double.IsInfinity(giay) || giay < 0) return 0;
            return (int)Math.Min(Math.Floor(giay), GiayToiDa);
        }
    }
}
