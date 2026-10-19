using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Đánh giá khóa học ở trang chi tiết và trang "Tất cả đánh giá":
    ///   - "Bạn thấy hữu ích?": người dùng đã đăng nhập chọn Hữu ích / Không hữu ích (bấm lại để bỏ), không tự chấm đánh giá của mình.
    ///   - Phản hồi của giáo viên: chỉ giáo viên dạy khóa đó được viết / sửa / xóa, mỗi đánh giá tối đa một phản hồi.
    /// Bảng DanhGiaHuuIch, PhanHoiDanhGia ở migration 011 (chưa có trong mô hình EF nên đọc / ghi bằng SQL).
    /// </summary>
    public class CourseReviewService
    {
        public const int DoDaiPhanHoiToiDa = 1000;

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public CourseReviewService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public bool IsInstalled()
        {
            try
            {
                return db.Database.SqlQuery<int>(@"
                    SELECT CASE WHEN OBJECT_ID(N'dbo.DanhGiaHuuIch', N'U') IS NOT NULL
                                 AND OBJECT_ID(N'dbo.PhanHoiDanhGia', N'U') IS NOT NULL THEN 1 ELSE 0 END").First() == 1;
            }
            catch
            {
                return false;
            }
        }

        private class DemSao
        {
            public int SoSao { get; set; }
            public int SoLuong { get; set; }
        }

        /// <summary>
        /// Đánh giá của khóa: tổng quan (điểm, phân bố sao) trên toàn bộ đánh giá + một trang đánh giá đã lọc / sắp xếp.
        /// Lọc và phân trang làm trong SQL (OFFSET / FETCH); tìm nội dung không phân biệt dấu.
        /// </summary>
        public DanhSachDanhGiaViewModel LayDanhSach(KhoaHoc khoa, int maNguoiXem, int? locSao, string tuKhoa, string sapXep, int trang, int kichThuoc)
        {
            var vm = new DanhSachDanhGiaViewModel
            {
                MaKhoaHoc = khoa.maKhoaHoc,
                TenKhoaHoc = khoa.tenKhoaHoc,
                AnhBia = khoa.anhBia,
                MaGiaoVien = khoa.maGiaoVien,
                TenGiaoVien = khoa.NguoiDung != null ? khoa.NguoiDung.hoTen : "Giảng viên",
                AnhGiaoVien = khoa.NguoiDung != null ? khoa.NguoiDung.anhDaiDien : null,
                MaNguoiXem = maNguoiXem,
                CoTuongTac = IsInstalled(),
                LocSao = locSao.HasValue && locSao.Value >= 1 && locSao.Value <= 5 ? locSao : null,
                TuKhoa = string.IsNullOrWhiteSpace(tuKhoa) ? null : tuKhoa.Trim(),
                SapXep = new[] { "moinhat", "huuich", "caonhat", "thapnhat" }.Contains(sapXep) ? sapXep : "moinhat",
                Trang = Math.Max(1, trang)
            };
            if (vm.TuKhoa != null && vm.TuKhoa.Length > 100) vm.TuKhoa = vm.TuKhoa.Substring(0, 100);

            foreach (var d in db.Database.SqlQuery<DemSao>(@"
                SELECT soSao AS SoSao, COUNT(*) AS SoLuong FROM dbo.DanhGiaKhoaHoc WHERE maKhoaHoc = @kh GROUP BY soSao",
                new SqlParameter("@kh", khoa.maKhoaHoc)).ToList())
            {
                if (d.SoSao >= 1 && d.SoSao <= 5) vm.SoTheoSao[d.SoSao] = d.SoLuong;
            }
            vm.TongSo = vm.SoTheoSao.Sum();
            vm.DiemTrungBinh = vm.TongSo > 0 ? Enumerable.Range(1, 5).Sum(s => s * vm.SoTheoSao[s]) / (double)vm.TongSo : 0;
            if (vm.TongSo == 0) return vm;

            string dieuKien = "d.maKhoaHoc = @kh"
                + (vm.LocSao.HasValue ? " AND d.soSao = @sao" : "")
                + (vm.TuKhoa != null ? " AND (d.noiDung COLLATE Vietnamese_CI_AI LIKE @tk OR nd.hoTen COLLATE Vietnamese_CI_AI LIKE @tk)" : "");
            Func<List<SqlParameter>> thamSo = () =>
            {
                var ps = new List<SqlParameter> { new SqlParameter("@kh", khoa.maKhoaHoc), new SqlParameter("@uid", maNguoiXem) };
                if (vm.LocSao.HasValue) ps.Add(new SqlParameter("@sao", vm.LocSao.Value));
                if (vm.TuKhoa != null) ps.Add(new SqlParameter("@tk", SqlDbType.NVarChar, 210) { Value = "%" + ThoatLike(vm.TuKhoa) + "%" });
                return ps;
            };

            vm.SoKetQua = db.Database.SqlQuery<int>(@"
                SELECT COUNT(*) FROM dbo.DanhGiaKhoaHoc d LEFT JOIN dbo.NguoiDung nd ON nd.maNguoiDung = d.maHocVien WHERE " + dieuKien,
                thamSo().ToArray()).First();
            int kich = Math.Max(1, kichThuoc);
            vm.SoTrang = Math.Max(1, (int)Math.Ceiling(vm.SoKetQua / (double)kich));
            if (vm.Trang > vm.SoTrang) vm.Trang = vm.SoTrang;

            string thuTu;
            switch (vm.SapXep)
            {
                case "huuich": thuTu = "SoHuuIch DESC, d.ngayDanhGia DESC"; break;
                case "caonhat": thuTu = "d.soSao DESC, d.ngayDanhGia DESC"; break;
                case "thapnhat": thuTu = "d.soSao ASC, d.ngayDanhGia DESC"; break;
                default: thuTu = "d.ngayDanhGia DESC"; break;
            }

            // Bảng bình chọn / phản hồi chưa có (chưa chạy 011): vẫn hiện đánh giá, không có số liệu tương tác
            string cotTuongTac = vm.CoTuongTac
                ? @"(SELECT COUNT(*) FROM dbo.DanhGiaHuuIch h WHERE h.maDanhGia = d.maDanhGia AND h.huuIch = 1) AS SoHuuIch,
                    (SELECT COUNT(*) FROM dbo.DanhGiaHuuIch h WHERE h.maDanhGia = d.maDanhGia AND h.huuIch = 0) AS SoKhongHuuIch,
                    (SELECT TOP (1) CAST(h.huuIch AS INT) FROM dbo.DanhGiaHuuIch h WHERE h.maDanhGia = d.maDanhGia AND h.maNguoiDung = @uid) AS BinhChonCuaToi,
                    p.noiDung AS PhanHoiNoiDung, p.ngayTao AS PhanHoiNgayTao, p.ngayCapNhat AS PhanHoiNgayCapNhat"
                : @"0 AS SoHuuIch, 0 AS SoKhongHuuIch, CAST(NULL AS INT) AS BinhChonCuaToi,
                    CAST(NULL AS NVARCHAR(1000)) AS PhanHoiNoiDung, CAST(NULL AS DATETIME) AS PhanHoiNgayTao, CAST(NULL AS DATETIME) AS PhanHoiNgayCapNhat";
            string joinPhanHoi = vm.CoTuongTac ? " LEFT JOIN dbo.PhanHoiDanhGia p ON p.maDanhGia = d.maDanhGia" : "";

            var ps2 = thamSo();
            ps2.Add(new SqlParameter("@boQua", (vm.Trang - 1) * kich));
            ps2.Add(new SqlParameter("@lay", kich));
            vm.Items = db.Database.SqlQuery<DanhGiaItem>(@"
                SELECT * FROM (
                    SELECT d.maDanhGia AS MaDanhGia, d.maHocVien AS MaHocVien, ISNULL(nd.hoTen, N'Học viên') AS TenHocVien,
                           nd.anhDaiDien AS AnhDaiDien, d.soSao AS SoSao, d.noiDung AS NoiDung, d.ngayDanhGia AS NgayDanhGia,
                           " + cotTuongTac + @"
                    FROM dbo.DanhGiaKhoaHoc d
                    LEFT JOIN dbo.NguoiDung nd ON nd.maNguoiDung = d.maHocVien" + joinPhanHoi + @"
                    WHERE " + dieuKien + @"
                ) d
                ORDER BY " + thuTu.Replace("d.soSao", "d.SoSao").Replace("d.ngayDanhGia", "d.NgayDanhGia") + @", d.MaDanhGia
                OFFSET @boQua ROWS FETCH NEXT @lay ROWS ONLY",
                ps2.ToArray()).ToList();
            return vm;
        }

        private static string ThoatLike(string s)
        {
            return s.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }

        public class KetQuaBinhChon
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public int SoHuuIch { get; set; }
            public int SoKhongHuuIch { get; set; }
            public int? BinhChon { get; set; }
        }

        private class ThongTinDanhGia
        {
            public string MaDanhGia { get; set; }
            public int MaHocVien { get; set; }
            public int MaGiaoVien { get; set; }
        }

        private ThongTinDanhGia LayThongTin(string maDanhGia)
        {
            if (string.IsNullOrWhiteSpace(maDanhGia)) return null;
            return db.Database.SqlQuery<ThongTinDanhGia>(@"
                SELECT d.maDanhGia AS MaDanhGia, d.maHocVien AS MaHocVien, k.maGiaoVien AS MaGiaoVien
                FROM dbo.DanhGiaKhoaHoc d INNER JOIN dbo.KhoaHoc k ON k.maKhoaHoc = d.maKhoaHoc
                WHERE d.maDanhGia = @ma",
                new SqlParameter("@ma", maDanhGia)).FirstOrDefault();
        }

        /// <summary>
        /// Chọn "Hữu ích" (huuIch = true) / "Không hữu ích" (false). Chọn lại đúng lựa chọn đang có thì bỏ chọn;
        /// chọn lựa chọn còn lại thì đổi. Khóa dòng (UPDLOCK, HOLDLOCK) để hai lần bấm liên tiếp không tạo trùng.
        /// </summary>
        public KetQuaBinhChon BinhChon(string maDanhGia, int maNguoiDung, bool huuIch)
        {
            var dg = LayThongTin(maDanhGia);
            if (dg == null) return new KetQuaBinhChon { Message = "Không tìm thấy đánh giá." };
            if (dg.MaHocVien == maNguoiDung) return new KetQuaBinhChon { Message = "Bạn không thể tự bình chọn đánh giá của mình." };

            db.Database.ExecuteSqlCommand(@"
                IF EXISTS (SELECT 1 FROM dbo.DanhGiaHuuIch WITH (UPDLOCK, HOLDLOCK) WHERE maDanhGia = @dg AND maNguoiDung = @u AND huuIch = @v)
                    DELETE FROM dbo.DanhGiaHuuIch WHERE maDanhGia = @dg AND maNguoiDung = @u;
                ELSE IF EXISTS (SELECT 1 FROM dbo.DanhGiaHuuIch WITH (UPDLOCK, HOLDLOCK) WHERE maDanhGia = @dg AND maNguoiDung = @u)
                    UPDATE dbo.DanhGiaHuuIch SET huuIch = @v, ngayCapNhat = GETDATE() WHERE maDanhGia = @dg AND maNguoiDung = @u;
                ELSE
                    INSERT INTO dbo.DanhGiaHuuIch (maDanhGia, maNguoiDung, huuIch) VALUES (@dg, @u, @v);",
                new SqlParameter("@dg", maDanhGia), new SqlParameter("@u", maNguoiDung), new SqlParameter("@v", huuIch));

            var kq = db.Database.SqlQuery<KetQuaBinhChon>(@"
                SELECT CAST(1 AS BIT) AS Success, CAST(NULL AS NVARCHAR(10)) AS Message,
                       (SELECT COUNT(*) FROM dbo.DanhGiaHuuIch WHERE maDanhGia = @dg AND huuIch = 1) AS SoHuuIch,
                       (SELECT COUNT(*) FROM dbo.DanhGiaHuuIch WHERE maDanhGia = @dg AND huuIch = 0) AS SoKhongHuuIch,
                       (SELECT TOP (1) CAST(huuIch AS INT) FROM dbo.DanhGiaHuuIch WHERE maDanhGia = @dg AND maNguoiDung = @u) AS BinhChon",
                new SqlParameter("@dg", maDanhGia), new SqlParameter("@u", maNguoiDung)).First();
            return kq;
        }

        public class KetQuaPhanHoi
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public string NoiDung { get; set; }
            public DateTime? NgayTao { get; set; }
            public DateTime? NgayCapNhat { get; set; }
        }

        /// <summary>Viết / sửa phản hồi. Chỉ giáo viên dạy khóa của đánh giá đó.</summary>
        public KetQuaPhanHoi LuuPhanHoi(string maDanhGia, int maNguoiDung, string noiDung)
        {
            var dg = LayThongTin(maDanhGia);
            if (dg == null) return new KetQuaPhanHoi { Message = "Không tìm thấy đánh giá." };
            if (dg.MaGiaoVien != maNguoiDung) return new KetQuaPhanHoi { Message = "Chỉ giáo viên của khóa học mới phản hồi được đánh giá." };
            noiDung = (noiDung ?? "").Trim();
            if (noiDung.Length == 0) return new KetQuaPhanHoi { Message = "Vui lòng nhập nội dung phản hồi." };
            if (noiDung.Length > DoDaiPhanHoiToiDa) return new KetQuaPhanHoi { Message = "Phản hồi tối đa " + DoDaiPhanHoiToiDa + " ký tự." };

            db.Database.ExecuteSqlCommand(@"
                IF EXISTS (SELECT 1 FROM dbo.PhanHoiDanhGia WITH (UPDLOCK, HOLDLOCK) WHERE maDanhGia = @dg)
                    UPDATE dbo.PhanHoiDanhGia SET noiDung = @nd, ngayCapNhat = GETDATE() WHERE maDanhGia = @dg;
                ELSE
                    INSERT INTO dbo.PhanHoiDanhGia (maDanhGia, maGiaoVien, noiDung) VALUES (@dg, @gv, @nd);",
                new SqlParameter("@dg", maDanhGia), new SqlParameter("@gv", maNguoiDung), new SqlParameter("@nd", noiDung));

            var kq = db.Database.SqlQuery<KetQuaPhanHoi>(@"
                SELECT CAST(1 AS BIT) AS Success, CAST(NULL AS NVARCHAR(10)) AS Message, noiDung AS NoiDung, ngayTao AS NgayTao, ngayCapNhat AS NgayCapNhat
                FROM dbo.PhanHoiDanhGia WHERE maDanhGia = @dg",
                new SqlParameter("@dg", maDanhGia)).First();
            return kq;
        }

        public KetQuaPhanHoi XoaPhanHoi(string maDanhGia, int maNguoiDung)
        {
            var dg = LayThongTin(maDanhGia);
            if (dg == null) return new KetQuaPhanHoi { Message = "Không tìm thấy đánh giá." };
            if (dg.MaGiaoVien != maNguoiDung) return new KetQuaPhanHoi { Message = "Chỉ giáo viên của khóa học mới xóa được phản hồi." };
            db.Database.ExecuteSqlCommand("DELETE FROM dbo.PhanHoiDanhGia WHERE maDanhGia = @dg", new SqlParameter("@dg", maDanhGia));
            return new KetQuaPhanHoi { Success = true, Message = "Đã xóa phản hồi." };
        }
    }
}
