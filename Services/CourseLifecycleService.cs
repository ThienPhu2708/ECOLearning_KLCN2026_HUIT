using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Giáo viên tự Tạm ngưng / Mở bán lại / Xóa khóa học của mình (khu Instructor).
    ///  - Tạm ngưng: chỉ khóa đang xuất bản; ẩn khỏi trang bán, học viên đã mua vẫn học. Mở bán lại ngay, không cần duyệt.
    ///  - Xóa: khóa chưa có lịch sử (học viên, đơn hàng, đánh giá, vi phạm, quiz) thì xóa hẳn; đã có thì xóa mềm
    ///    (trạng thái "Đã xóa") để giữ doanh thu, báo cáo và quyền học của học viên đã mua.
    /// Khóa đang chờ duyệt hoặc đang bị Admin xử lý vi phạm thì không được tạm ngưng / xóa.
    /// </summary>
    public class CourseLifecycleService
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        // Có dòng ở các bảng này nghĩa là khóa học đã có lịch sử: chỉ xóa mềm.
        private static readonly string[][] BangLichSu =
        {
            new[] { "DangKyKhoaHoc", "maKhoaHoc" }, new[] { "ChiTietDonHang", "maKhoaHoc" }, new[] { "DanhGiaKhoaHoc", "maKhoaHoc" },
            new[] { "TienDoHocTap", "maKhoaHoc" }, new[] { "BaoCaoViPham", "maKhoaHoc" }, new[] { "XuLyViPham", "maKhoaHoc" },
            new[] { "QuizAttempt", "CourseId" }, new[] { "QuizAttemptGrant", "CourseId" }, new[] { "QuizCertificate", "CourseId" }
        };

        // Dữ liệu phụ không có ON DELETE CASCADE, xóa trước khi xóa hẳn khóa học
        // (chương, bài trong chương, tài liệu, khuyến mãi, tương tác tự xóa theo CASCADE).
        private static readonly string[][] BangPhu =
        {
            new[] { "KiemDuyetKhoaHoc", "maKhoaHoc" }, new[] { "KhoaHocKhuyenNghi", "maKhoaHoc" }, new[] { "KhoaHocTamAn", "maKhoaHoc" },
            new[] { "QuizQuestion", "CourseId" }, new[] { "QuizConfig", "CourseId" }, new[] { "QuizSkill", "CourseId" }, new[] { "CourseRevision", "CourseId" }
        };

        private static readonly string[] DangChoDuyet = { CourseStatus.ChoDuyet, "ChoDuyet", "Chờ kiểm duyệt", "Chờ xét duyệt", "Pending" };

        public class KetQua
        {
            public string Loi;
            public string TenKhoaHoc;
            public bool XoaHan;
        }

        public CourseLifecycleService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public static bool CoTheTamNgung(string trangThai)
        {
            return CourseRevisionService.Published(trangThai);
        }

        /// <summary>Lý do không xóa được theo trạng thái (null = được xóa).</summary>
        public static string LyDoKhongXoa(string trangThai)
        {
            if (DangChoDuyet.Contains(trangThai)) return "Khóa học đang chờ duyệt. Hãy hủy yêu cầu duyệt trước khi xóa.";
            if (trangThai == CourseStatus.TamKhoa || trangThai == CourseStatus.KhoaVinhVien)
                return "Khóa học đang bị xử lý vi phạm nên không thể xóa.";
            if (trangThai == CourseStatus.TamAn) return "Khóa học đang tạm ẩn do tài khoản bị khóa nên không thể xóa.";
            if (trangThai == CourseStatus.DaXoa) return "Khóa học đã được xóa.";
            return null;
        }

        public KetQua TamNgung(string maKhoaHoc, int maGiaoVien)
        {
            var khoa = Lay(maKhoaHoc, maGiaoVien);
            if (khoa == null) return new KetQua { Loi = "Không tìm thấy khóa học." };
            var kq = new KetQua { TenKhoaHoc = khoa.tenKhoaHoc };
            if (!CoTheTamNgung(khoa.trangThai))
                kq.Loi = "Chỉ tạm ngưng được khóa học đang xuất bản.";
            else if (new CourseRevisionService(db, null).Get(maKhoaHoc)?.Status == "Pending")
                kq.Loi = "Bản sửa của khóa học đang chờ Admin duyệt. Hãy hủy gửi duyệt bản sửa trước khi tạm ngưng.";
            else if (DoiTrangThai(maKhoaHoc, maGiaoVien, khoa.trangThai, CourseStatus.TamNgung) != 1)
                kq.Loi = "Trạng thái khóa học vừa thay đổi. Vui lòng tải lại trang.";
            return kq;
        }

        public KetQua MoBanLai(string maKhoaHoc, int maGiaoVien)
        {
            var khoa = Lay(maKhoaHoc, maGiaoVien);
            if (khoa == null) return new KetQua { Loi = "Không tìm thấy khóa học." };
            var kq = new KetQua { TenKhoaHoc = khoa.tenKhoaHoc };
            if (khoa.trangThai != CourseStatus.TamNgung)
                kq.Loi = "Khóa học không ở trạng thái tạm ngưng.";
            else if (DoiTrangThai(maKhoaHoc, maGiaoVien, CourseStatus.TamNgung, CourseStatus.DaXuatBan) != 1)
                kq.Loi = "Trạng thái khóa học vừa thay đổi. Vui lòng tải lại trang.";
            return kq;
        }

        /// <summary>Có lịch sử (học viên, đơn hàng, đánh giá...) thì xóa sẽ là xóa mềm.</summary>
        public bool CoLichSu(string maKhoaHoc)
        {
            var coBang = BangTonTai();
            foreach (var b in BangLichSu.Where(b => coBang.Contains(b[0])))
            {
                if (db.Database.SqlQuery<int>("SELECT COUNT(1) FROM dbo." + b[0] + " WHERE " + b[1] + " = @id",
                        new SqlParameter("@id", maKhoaHoc)).Single() > 0)
                    return true;
            }
            return false;
        }

        public KetQua Xoa(string maKhoaHoc, int maGiaoVien)
        {
            var khoa = Lay(maKhoaHoc, maGiaoVien);
            if (khoa == null) return new KetQua { Loi = "Không tìm thấy khóa học." };
            var kq = new KetQua { TenKhoaHoc = khoa.tenKhoaHoc, Loi = LyDoKhongXoa(khoa.trangThai) };
            if (kq.Loi != null) return kq;

            if (!CoLichSu(maKhoaHoc))
            {
                try
                {
                    XoaHan(maKhoaHoc, maGiaoVien);
                    kq.XoaHan = true;
                    LamMoiCache();
                    return kq;
                }
                catch (Exception ex) when (ex is SqlException || ex is InvalidOperationException)
                {
                    // Còn dữ liệu khác tham chiếu tới khóa học: chuyển sang xóa mềm
                    System.Diagnostics.Debug.WriteLine("Xóa hẳn khóa " + maKhoaHoc + " thất bại, xóa mềm: " + ex.Message);
                }
            }

            if (DoiTrangThai(maKhoaHoc, maGiaoVien, khoa.trangThai, CourseStatus.DaXoa) != 1)
            {
                kq.Loi = "Trạng thái khóa học vừa thay đổi. Vui lòng tải lại trang.";
                return kq;
            }
            if (BangTonTai().Contains("CourseRevision"))
                db.Database.ExecuteSqlCommand("DELETE FROM dbo.CourseRevision WHERE CourseId = @id", new SqlParameter("@id", maKhoaHoc));
            return kq;
        }

        private void XoaHan(string maKhoaHoc, int maGiaoVien)
        {
            var coBang = BangTonTai();
            using (var tx = db.Database.BeginTransaction())
            {
                foreach (var b in BangPhu.Where(b => coBang.Contains(b[0])))
                    db.Database.ExecuteSqlCommand("DELETE FROM dbo." + b[0] + " WHERE " + b[1] + " = @id", new SqlParameter("@id", maKhoaHoc));
                // Bài học không thuộc chương (mã "{maKhoaHoc}_{n}") không nối khóa ngoại tới khóa học
                db.Database.ExecuteSqlCommand(
                    "DELETE FROM dbo.BaiHoc WHERE maChuong IS NULL AND LEFT(maBaiHoc, LEN(@id) + 1) = @id + '_'",
                    new SqlParameter("@id", maKhoaHoc));
                int n = db.Database.ExecuteSqlCommand("DELETE FROM dbo.KhoaHoc WHERE maKhoaHoc = @id AND maGiaoVien = @gv",
                    new SqlParameter("@id", maKhoaHoc), new SqlParameter("@gv", maGiaoVien));
                if (n != 1) throw new InvalidOperationException("Không xóa được khóa học.");
                tx.Commit();
            }
        }

        private int DoiTrangThai(string maKhoaHoc, int maGiaoVien, string tu, string sang)
        {
            int n = db.Database.ExecuteSqlCommand(
                "UPDATE dbo.KhoaHoc SET trangThai = @sang WHERE maKhoaHoc = @id AND maGiaoVien = @gv AND trangThai = @tu",
                new SqlParameter("@sang", sang), new SqlParameter("@id", maKhoaHoc),
                new SqlParameter("@gv", maGiaoVien), new SqlParameter("@tu", tu));
            if (n == 1) LamMoiCache();
            return n;
        }

        private class KhoaNgan
        {
            public string tenKhoaHoc { get; set; }
            public string trangThai { get; set; }
        }

        // Khóa đã xóa (mềm) coi như không còn đối với giáo viên
        private KhoaNgan Lay(string maKhoaHoc, int maGiaoVien)
        {
            if (string.IsNullOrEmpty(maKhoaHoc)) return null;
            return db.Database.SqlQuery<KhoaNgan>(
                "SELECT tenKhoaHoc, trangThai FROM dbo.KhoaHoc WHERE maKhoaHoc = @id AND maGiaoVien = @gv AND ISNULL(trangThai, '') <> @xoa",
                new SqlParameter("@id", maKhoaHoc), new SqlParameter("@gv", maGiaoVien), new SqlParameter("@xoa", CourseStatus.DaXoa)).SingleOrDefault();
        }

        private HashSet<string> BangTonTai()
        {
            var ten = BangLichSu.Concat(BangPhu).Select(b => b[0]).ToList();
            var thamSo = ten.Select((t, i) => new SqlParameter("@t" + i, t)).ToArray();
            return new HashSet<string>(db.Database.SqlQuery<string>(
                "SELECT name FROM sys.tables WHERE name IN (" + string.Join(", ", thamSo.Select(p => p.ParameterName)) + ")",
                thamSo).ToList(), StringComparer.OrdinalIgnoreCase);
        }

        // Trang chủ, tìm kiếm, gợi ý cập nhật ngay
        private static void LamMoiCache()
        {
            SearchService.XoaCache();
            RelatedCourseService.XoaCache();
        }
    }
}
