using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using ECOLearning.Controllers.Api;
using ECOLearning.Models;
using ECOLearning.Services.Api;

namespace ECOLearning.Services
{
    /// <summary>
    /// Thao tác của Admin lên tài khoản (migration 016): khóa / mở khóa có thời hạn, cảnh cáo, tạo tài khoản nhân viên,
    /// đặt lại mật khẩu bằng mật khẩu tạm. Mọi thao tác ghi vào NhatKyQuanTri.
    /// Quy tắc:
    ///  - Không xóa tài khoản (còn đơn hàng, doanh thu, chứng chỉ), chỉ khóa.
    ///  - Không tự khóa mình, không khóa Admin cuối cùng còn hoạt động.
    ///  - Khóa tài khoản: thu hồi token app; phiên web bị đăng xuất ở lần truy cập kế tiếp (AccountStatusFilter).
    ///  - Khóa giáo viên: các khóa đang bán chuyển sang "Tạm ẩn" (lưu trạng thái cũ ở KhoaHocTamAn) nên không bán thêm,
    ///    học viên đã mua vẫn học; phiếu chi của giáo viên bị tạm giữ. Mở khóa thì khôi phục trạng thái cũ.
    ///  - Admin không bao giờ xem mật khẩu: mật khẩu tạm chỉ gửi qua email, người dùng phải đổi ở lần đăng nhập đầu.
    /// </summary>
    public class AccountAdminService
    {
        public const string HocVien = "HocVien";
        public const string GiaoVien = "GiaoVien";
        public const string Marketing = "Marketing";
        public const string Admin = "Admin";

        public const string HdKhoa = "KhoaTaiKhoan";
        public const string HdMoKhoa = "MoKhoa";
        public const string HdCanhCao = "CanhCao";
        public const string HdTaoNhanVien = "TaoNhanVien";
        public const string HdDatLaiMatKhau = "DatLaiMatKhau";
        public const string HdTamDungKhuyenMai = "TamDungKhuyenMai";
        public const string HdChoChayKhuyenMai = "ChoChayKhuyenMai";
        public const string HdThayPhien = "ThayPhienDangNhap";       // migration 017, hệ thống ghi (PhienDocQuyenService)

        public const string DoiTuongNguoiDung = "NguoiDung";
        public const string DoiTuongKhuyenMai = "KhuyenMai";

        /// <summary>Thời hạn khóa Admin được chọn (ngày); 0 = vĩnh viễn.</summary>
        public static readonly int[] ThoiHanKhoa = { 7, 30, 0 };

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public AccountAdminService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public bool IsInstalled()
        {
            return IsInstalled(db);
        }

        public static bool IsInstalled(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            return db.Database.SqlQuery<int>(@"
                SELECT CASE WHEN COL_LENGTH('dbo.NguoiDung', 'khoaDenNgay') IS NOT NULL
                             AND OBJECT_ID('dbo.NhatKyQuanTri') IS NOT NULL
                             AND OBJECT_ID('dbo.KhoaHocTamAn') IS NOT NULL THEN 1 ELSE 0 END").Single() == 1;
        }

        // ================================================================== đọc

        public TrangThaiTaiKhoan LayTrangThai(int maNguoiDung)
        {
            return db.Database.SqlQuery<TrangThaiTaiKhoan>(@"
                SELECT nd.maNguoiDung AS MaNguoiDung, nd.hoTen AS HoTen, nd.email AS Email, nd.vaiTro AS VaiTro,
                       CAST(ISNULL(nd.trangThai, 1) AS BIT) AS HoatDong, nd.lyDoKhoa AS LyDoKhoa, nd.ngayKhoa AS NgayKhoa,
                       nd.khoaDenNgay AS KhoaDenNgay, nd.canDoiMatKhau AS CanDoiMatKhau, ad.hoTen AS TenAdminKhoa
                FROM dbo.NguoiDung nd
                LEFT JOIN dbo.NguoiDung ad ON ad.maNguoiDung = nd.maAdminKhoa
                WHERE nd.maNguoiDung = @id", new SqlParameter("@id", maNguoiDung)).FirstOrDefault();
        }

        public List<NhatKyQuanTriItem> LayNhatKy(string loaiDoiTuong, string maDoiTuong, int top = 50)
        {
            return db.Database.SqlQuery<NhatKyQuanTriItem>(@"
                SELECT TOP (@top) n.maNhatKy AS MaNhatKy, n.hanhDong AS HanhDong, n.lyDo AS LyDo, n.ngayTao AS NgayTao,
                       ISNULL(a.hoTen, N'Hệ thống') AS TenAdmin
                FROM dbo.NhatKyQuanTri n
                LEFT JOIN dbo.NguoiDung a ON a.maNguoiDung = n.maAdmin
                WHERE n.loaiDoiTuong = @loai AND n.maDoiTuong = @ma
                ORDER BY n.ngayTao DESC, n.maNhatKy DESC",
                new SqlParameter("@top", top), new SqlParameter("@loai", loaiDoiTuong), new SqlParameter("@ma", maDoiTuong)).ToList();
        }

        public void GhiNhatKy(int? maAdmin, string hanhDong, string loaiDoiTuong, string maDoiTuong, string lyDo)
        {
            db.Database.ExecuteSqlCommand(@"
                INSERT INTO dbo.NhatKyQuanTri (maAdmin, hanhDong, loaiDoiTuong, maDoiTuong, lyDo)
                VALUES (@ad, @hd, @loai, @ma, @ld)",
                new SqlParameter("@ad", (object)maAdmin ?? DBNull.Value),
                new SqlParameter("@hd", hanhDong),
                new SqlParameter("@loai", loaiDoiTuong),
                new SqlParameter("@ma", maDoiTuong),
                new SqlParameter("@ld", (object)Cat(lyDo, 1000) ?? DBNull.Value));
        }

        // ================================================================== khóa / mở khóa

        /// <param name="soNgay">Số ngày khóa; 0 = vĩnh viễn.</param>
        public KetQuaQuanTri Khoa(int maAdmin, int maNguoiDung, string lyDo, int soNgay)
        {
            lyDo = (lyDo ?? "").Trim();
            if (lyDo.Length < 5) return KetQuaQuanTri.Loi("Vui lòng nhập lý do khóa (tối thiểu 5 ký tự).");
            if (lyDo.Length > 500) return KetQuaQuanTri.Loi("Lý do khóa tối đa 500 ký tự.");
            if (!ThoiHanKhoa.Contains(soNgay)) return KetQuaQuanTri.Loi("Thời hạn khóa không hợp lệ.");
            if (maAdmin == maNguoiDung) return KetQuaQuanTri.Loi("Bạn không thể tự khóa tài khoản của mình.");

            var tk = LayTrangThai(maNguoiDung);
            if (tk == null) return KetQuaQuanTri.Loi("Không tìm thấy người dùng.");
            if (!tk.HoatDong) return KetQuaQuanTri.Loi("Tài khoản này đang bị khóa. Hãy mở khóa trước nếu muốn khóa lại với thời hạn khác.");
            if (tk.VaiTro == Admin && DemAdminHoatDong() <= 1) return KetQuaQuanTri.Loi("Không thể khóa Quản trị viên cuối cùng của hệ thống.");

            DateTime? denNgay = soNgay > 0 ? DateTime.Now.AddDays(soNgay) : (DateTime?)null;
            int soKhoaAn = 0;
            using (var tran = db.Database.BeginTransaction())
            {
                db.Database.ExecuteSqlCommand(@"
                    UPDATE dbo.NguoiDung
                    SET trangThai = 0, lyDoKhoa = @ld, ngayKhoa = GETDATE(), khoaDenNgay = @den, maAdminKhoa = @ad
                    WHERE maNguoiDung = @id",
                    new SqlParameter("@ld", lyDo),
                    new SqlParameter("@den", (object)denNgay ?? DBNull.Value),
                    new SqlParameter("@ad", maAdmin),
                    new SqlParameter("@id", maNguoiDung));

                if (tk.VaiTro == GiaoVien) soKhoaAn = AnKhoaHocCuaGiaoVien(maNguoiDung);

                GhiNhatKy(maAdmin, HdKhoa, DoiTuongNguoiDung, maNguoiDung.ToString(),
                    (soNgay > 0 ? "Khóa " + soNgay + " ngày" : "Khóa vĩnh viễn") + ": " + lyDo);
                tran.Commit();
            }

            ThuHoiTokenApp(maNguoiDung);
            AccountStatusFilter.DanhDauThayDoi(maNguoiDung);

            string thoiHan = denNgay.HasValue ? "đến " + denNgay.Value.ToString("HH:mm dd/MM/yyyy") : "vĩnh viễn";
            string doanKhoaHoc = soKhoaAn > 0
                ? "<p>Trong thời gian khóa, " + soKhoaAn + " khóa học đang bán của bạn được tạm ẩn khỏi trang bán hàng; học viên đã mua vẫn học bình thường. Các yêu cầu rút tiền sẽ được xử lý sau khi tài khoản được mở lại.</p>"
                : "";
            var mail = EmailService.SendAdminNoticeEmail(tk.Email, tk.HoTen, "[E-Learning] Tài khoản của bạn đã bị tạm khóa",
                "<p>Ban quản trị E-Learning đã khóa tài khoản của bạn <strong>" + thoiHan + "</strong>. Bạn sẽ không đăng nhập được trên website và ứng dụng trong thời gian này.</p>"
                + doanKhoaHoc
                + "<p>Nếu bạn cho rằng đây là nhầm lẫn, vui lòng phản hồi qua trang Liên hệ.</p>",
                "Lý do", WebUtility.HtmlEncode(lyDo), "danger");

            string thongBao = "Đã khóa tài khoản " + tk.Email + " " + thoiHan + ".";
            if (soKhoaAn > 0) thongBao += " Đã tạm ẩn " + soKhoaAn + " khóa học đang bán.";
            if (!mail.Success) thongBao += " (Chưa gửi được email thông báo.)";
            return KetQuaQuanTri.Ok(thongBao);
        }

        /// <param name="maAdmin">null khi hệ thống tự mở do hết hạn khóa.</param>
        public KetQuaQuanTri MoKhoa(int? maAdmin, int maNguoiDung, string ghiChu)
        {
            ghiChu = (ghiChu ?? "").Trim();
            if (ghiChu.Length > 500) return KetQuaQuanTri.Loi("Ghi chú tối đa 500 ký tự.");

            var tk = LayTrangThai(maNguoiDung);
            if (tk == null) return KetQuaQuanTri.Loi("Không tìm thấy người dùng.");
            if (tk.HoatDong) return KetQuaQuanTri.Loi("Tài khoản này đang hoạt động.");

            int soKhoaHienLai;
            using (var tran = db.Database.BeginTransaction())
            {
                // Điều kiện trangThai = 0 tránh hai yêu cầu mở khóa cùng lúc ghi nhật ký hai lần
                int n = db.Database.ExecuteSqlCommand(@"
                    UPDATE dbo.NguoiDung
                    SET trangThai = 1, lyDoKhoa = NULL, ngayKhoa = NULL, khoaDenNgay = NULL, maAdminKhoa = NULL
                    WHERE maNguoiDung = @id AND trangThai = 0", new SqlParameter("@id", maNguoiDung));
                if (n == 0)
                {
                    tran.Rollback();
                    return KetQuaQuanTri.Loi("Tài khoản này đang hoạt động.");
                }

                soKhoaHienLai = HienLaiKhoaHocCuaGiaoVien(maNguoiDung);
                string noiDung = maAdmin.HasValue ? (ghiChu.Length > 0 ? ghiChu : "Admin mở khóa") : "Tự mở khóa khi hết thời hạn khóa";
                GhiNhatKy(maAdmin, HdMoKhoa, DoiTuongNguoiDung, maNguoiDung.ToString(), noiDung);
                tran.Commit();
            }

            var mail = EmailService.SendAdminNoticeEmail(tk.Email, tk.HoTen, "[E-Learning] Tài khoản của bạn đã được mở khóa",
                "<p>Tài khoản E-Learning của bạn đã được mở khóa. Bạn có thể đăng nhập và sử dụng lại bình thường.</p>"
                + (soKhoaHienLai > 0 ? "<p>" + soKhoaHienLai + " khóa học của bạn đã được hiển thị lại trên trang bán hàng.</p>" : ""),
                null, null, "success");

            string thongBao = "Đã mở khóa tài khoản " + tk.Email + ".";
            if (soKhoaHienLai > 0) thongBao += " Đã hiển thị lại " + soKhoaHienLai + " khóa học.";
            if (!mail.Success && maAdmin.HasValue) thongBao += " (Chưa gửi được email thông báo.)";
            return KetQuaQuanTri.Ok(thongBao);
        }

        /// <summary>Mở các tài khoản đã hết thời hạn khóa (một người hoặc tất cả). Gọi khi đăng nhập và khi Admin mở danh sách.</summary>
        public int MoKhoaHetHan(int? maNguoiDung = null)
        {
            if (!IsInstalled()) return 0;
            var ids = db.Database.SqlQuery<int>(@"
                SELECT maNguoiDung FROM dbo.NguoiDung
                WHERE trangThai = 0 AND khoaDenNgay IS NOT NULL AND khoaDenNgay <= GETDATE()
                  AND (@id IS NULL OR maNguoiDung = @id)",
                new SqlParameter("@id", (object)maNguoiDung ?? DBNull.Value)).ToList();
            int dem = 0;
            foreach (int id in ids)
            {
                if (MoKhoa(null, id, null).ThanhCong) dem++;
            }
            return dem;
        }

        /// <summary>Gọi trước khi kiểm tra trangThai lúc đăng nhập; lỗi (chưa chạy migration...) không chặn đăng nhập.</summary>
        public static void MoKhoaHetHanKhiDangNhap(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, int maNguoiDung)
        {
            try
            {
                new AccountAdminService(db).MoKhoaHetHan(maNguoiDung);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("MoKhoaHetHan error: " + ex.Message);
            }
        }

        /// <summary>Câu báo cho người bị khóa khi đăng nhập: lý do và thời hạn (nếu đã chạy migration 016).</summary>
        public static string CauBaoKhoa(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, int maNguoiDung)
        {
            const string macDinh = "Tài khoản của bạn đã bị khóa hoặc chưa được kích hoạt.";
            try
            {
                var svc = new AccountAdminService(db);
                if (!svc.IsInstalled()) return macDinh;
                var tk = svc.LayTrangThai(maNguoiDung);
                if (tk == null || string.IsNullOrEmpty(tk.LyDoKhoa)) return macDinh;
                return "Tài khoản của bạn đã bị khóa " + (tk.KhoaDenNgay.HasValue ? "đến " + tk.KhoaDenNgay.Value.ToString("HH:mm dd/MM/yyyy") : "vĩnh viễn")
                     + ". Lý do: " + tk.LyDoKhoa;
            }
            catch
            {
                return macDinh;
            }
        }

        private int DemAdminHoatDong()
        {
            return db.Database.SqlQuery<int>(
                "SELECT COUNT(*) FROM dbo.NguoiDung WHERE vaiTro = @ad AND ISNULL(trangThai, 1) = 1",
                new SqlParameter("@ad", Admin)).Single();
        }

        private int AnKhoaHocCuaGiaoVien(int maGiaoVien)
        {
            var dangBan = new[] { CourseStatus.DaXuatBan, "Đã duyệt", "Active", "Đang hoạt động" };
            var thamSo = dangBan.Select((v, i) => new SqlParameter("@s" + i, v)).ToList();
            string inList = string.Join(", ", thamSo.Select(p => p.ParameterName));
            thamSo.Add(new SqlParameter("@gv", maGiaoVien));

            db.Database.ExecuteSqlCommand(@"
                INSERT INTO dbo.KhoaHocTamAn (maKhoaHoc, maGiaoVien, trangThaiTruoc)
                SELECT k.maKhoaHoc, k.maGiaoVien, k.trangThai FROM dbo.KhoaHoc k
                WHERE k.maGiaoVien = @gv AND k.trangThai IN (" + inList + @")
                  AND NOT EXISTS (SELECT 1 FROM dbo.KhoaHocTamAn t WHERE t.maKhoaHoc = k.maKhoaHoc);", thamSo.ToArray());

            return db.Database.ExecuteSqlCommand(@"
                UPDATE k SET trangThai = @an
                FROM dbo.KhoaHoc k INNER JOIN dbo.KhoaHocTamAn t ON t.maKhoaHoc = k.maKhoaHoc
                WHERE t.maGiaoVien = @gv AND k.trangThai <> @an;",
                new SqlParameter("@gv", maGiaoVien), new SqlParameter("@an", CourseStatus.TamAn));
        }

        private int HienLaiKhoaHocCuaGiaoVien(int maGiaoVien)
        {
            // Chỉ khôi phục khóa còn ở "Tạm ẩn"; khóa đã đổi trạng thái khác trong lúc ẩn thì giữ nguyên
            int n = db.Database.ExecuteSqlCommand(@"
                UPDATE k SET trangThai = t.trangThaiTruoc
                FROM dbo.KhoaHoc k INNER JOIN dbo.KhoaHocTamAn t ON t.maKhoaHoc = k.maKhoaHoc
                WHERE t.maGiaoVien = @gv AND k.trangThai = @an;",
                new SqlParameter("@gv", maGiaoVien), new SqlParameter("@an", CourseStatus.TamAn));
            db.Database.ExecuteSqlCommand("DELETE FROM dbo.KhoaHocTamAn WHERE maGiaoVien = @gv", new SqlParameter("@gv", maGiaoVien));
            return n;
        }

        private void ThuHoiTokenApp(int maNguoiDung)
        {
            try
            {
                var tokens = new ApiTokenService(db);
                if (tokens.IsInstalled()) tokens.ThuHoiTatCa(maNguoiDung);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ThuHoiTokenApp error: " + ex.Message);
            }
        }

        // ================================================================== cảnh cáo

        public KetQuaQuanTri CanhCao(int maAdmin, int maNguoiDung, string noiDung)
        {
            noiDung = (noiDung ?? "").Trim();
            if (noiDung.Length < 10) return KetQuaQuanTri.Loi("Vui lòng nhập nội dung cảnh cáo (tối thiểu 10 ký tự).");
            if (noiDung.Length > 1000) return KetQuaQuanTri.Loi("Nội dung cảnh cáo tối đa 1000 ký tự.");

            var tk = LayTrangThai(maNguoiDung);
            if (tk == null) return KetQuaQuanTri.Loi("Không tìm thấy người dùng.");
            if (maAdmin == maNguoiDung) return KetQuaQuanTri.Loi("Bạn không thể tự cảnh cáo mình.");

            var mail = EmailService.SendAdminNoticeEmail(tk.Email, tk.HoTen, "[E-Learning] Cảnh cáo từ Ban quản trị",
                "<p>Ban quản trị E-Learning gửi đến bạn cảnh cáo sau. Vui lòng điều chỉnh để tránh bị khóa tài khoản hoặc khóa học.</p>",
                "Nội dung cảnh cáo", WebUtility.HtmlEncode(noiDung).Replace("\n", "<br/>"), "warning");
            if (!mail.Success) return KetQuaQuanTri.Loi("Không gửi được email cảnh cáo: " + mail.Message);

            GhiNhatKy(maAdmin, HdCanhCao, DoiTuongNguoiDung, maNguoiDung.ToString(), noiDung);
            return KetQuaQuanTri.Ok("Đã gửi cảnh cáo đến " + tk.Email + ".");
        }

        // ================================================================== nhân viên / mật khẩu tạm

        public KetQuaQuanTri TaoNhanVien(int maAdmin, string hoTen, string email, string vaiTro)
        {
            // Bộ gõ tổ hợp dấu (chữ + dấu rời) đổi về dạng dựng sẵn để kiểm tra "chỉ chữ cái" không báo sai
            hoTen = hoTen?.Normalize(System.Text.NormalizationForm.FormC);
            string loi = ApiRules.KiemTraHoTen(hoTen) ?? ApiRules.KiemTraEmail(email);
            if (loi != null) return KetQuaQuanTri.Loi(loi);
            if (vaiTro != Marketing && vaiTro != Admin) return KetQuaQuanTri.Loi("Chỉ tạo được tài khoản nhân viên Marketing hoặc Quản trị viên.");

            hoTen = hoTen.Trim();
            email = ApiRules.ChuanHoaEmail(email);
            if (db.NguoiDung.Any(u => u.email.ToLower() == email)) return KetQuaQuanTri.Loi("Email này đã có tài khoản trong hệ thống.");

            string matKhauTam = TaoMatKhauTam();
            var user = new NguoiDung
            {
                hoTen = hoTen,
                email = email,
                matKhau = PasswordHasher.HashPassword(matKhauTam),
                vaiTro = vaiTro,
                trangThai = true
            };
            db.NguoiDung.Add(user);
            db.SaveChanges();
            db.Database.ExecuteSqlCommand("UPDATE dbo.NguoiDung SET canDoiMatKhau = 1 WHERE maNguoiDung = @id", new SqlParameter("@id", user.maNguoiDung));

            string tenVaiTro = vaiTro == Admin ? "Quản trị viên" : "Nhân viên Marketing";
            var mail = GuiMatKhauTam(email, hoTen, matKhauTam,
                "<p>Ban quản trị E-Learning đã tạo cho bạn tài khoản <strong>" + tenVaiTro + "</strong> với email đăng nhập <strong>" + WebUtility.HtmlEncode(email) + "</strong>.</p>");
            if (!mail.Success)
            {
                // Không gửi được mật khẩu tạm thì tài khoản không dùng được: hủy để Admin tạo lại
                db.NguoiDung.Remove(user);
                db.SaveChanges();
                return KetQuaQuanTri.Loi("Không gửi được email mật khẩu tạm nên chưa tạo tài khoản: " + mail.Message);
            }

            GhiNhatKy(maAdmin, HdTaoNhanVien, DoiTuongNguoiDung, user.maNguoiDung.ToString(), "Tạo tài khoản " + tenVaiTro);
            return KetQuaQuanTri.Ok("Đã tạo tài khoản " + tenVaiTro + " " + email + " và gửi mật khẩu tạm qua email.", user.maNguoiDung);
        }

        public KetQuaQuanTri DatLaiMatKhau(int maAdmin, int maNguoiDung)
        {
            if (maAdmin == maNguoiDung) return KetQuaQuanTri.Loi("Hãy đổi mật khẩu của bạn ở trang Hồ sơ.");
            var user = db.NguoiDung.Find(maNguoiDung);
            if (user == null) return KetQuaQuanTri.Loi("Không tìm thấy người dùng.");

            string matKhauCu = user.matKhau;
            string matKhauTam = TaoMatKhauTam();
            user.matKhau = PasswordHasher.HashPassword(matKhauTam);
            db.SaveChanges();

            var mail = GuiMatKhauTam(user.email, user.hoTen, matKhauTam,
                "<p>Theo yêu cầu hỗ trợ, Ban quản trị E-Learning đã đặt lại mật khẩu cho tài khoản <strong>" + WebUtility.HtmlEncode(user.email) + "</strong>. Mật khẩu cũ không còn dùng được.</p>");
            if (!mail.Success)
            {
                user.matKhau = matKhauCu;
                db.SaveChanges();
                return KetQuaQuanTri.Loi("Không gửi được email nên chưa đặt lại mật khẩu: " + mail.Message);
            }

            db.Database.ExecuteSqlCommand("UPDATE dbo.NguoiDung SET canDoiMatKhau = 1 WHERE maNguoiDung = @id", new SqlParameter("@id", maNguoiDung));
            ThuHoiTokenApp(maNguoiDung);
            AccountStatusFilter.DanhDauThayDoi(maNguoiDung);
            GhiNhatKy(maAdmin, HdDatLaiMatKhau, DoiTuongNguoiDung, maNguoiDung.ToString(), "Đặt lại mật khẩu, gửi mật khẩu tạm qua email");
            return KetQuaQuanTri.Ok("Đã đặt lại mật khẩu và gửi mật khẩu tạm đến " + user.email + ".");
        }

        /// <summary>Người dùng đã tự đặt mật khẩu mới (đổi mật khẩu / quên mật khẩu): bỏ yêu cầu đổi mật khẩu tạm.</summary>
        public static void DaDoiMatKhau(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, int maNguoiDung)
        {
            try
            {
                db.Database.ExecuteSqlCommand(@"
                    IF COL_LENGTH('dbo.NguoiDung', 'canDoiMatKhau') IS NOT NULL
                        EXEC sp_executesql N'UPDATE dbo.NguoiDung SET canDoiMatKhau = 0 WHERE maNguoiDung = @id AND canDoiMatKhau = 1', N'@id INT', @id = @id",
                    new SqlParameter("@id", maNguoiDung));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DaDoiMatKhau error: " + ex.Message);
            }
        }

        private static (bool Success, string Message) GuiMatKhauTam(string email, string hoTen, string matKhauTam, string doanMoDau)
        {
            return EmailService.SendAdminNoticeEmail(email, hoTen, "[E-Learning] Mật khẩu tạm cho tài khoản của bạn",
                doanMoDau + "<p>Dùng mật khẩu tạm dưới đây để đăng nhập. Hệ thống sẽ yêu cầu bạn <strong>đổi mật khẩu mới ngay</strong> sau khi đăng nhập.</p>",
                "Mật khẩu tạm", "<span style='font-family: monospace; font-size: 20px; font-weight: 800; letter-spacing: 2px;'>" + WebUtility.HtmlEncode(matKhauTam) + "</span>", "info");
        }

        /// <summary>12 ký tự ngẫu nhiên (bộ sinh mật mã), luôn có chữ hoa, chữ thường, chữ số, ký tự đặc biệt; bỏ ký tự dễ nhầm (0/O, 1/l/I).</summary>
        public static string TaoMatKhauTam()
        {
            const string hoa = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string thuong = "abcdefghijkmnpqrstuvwxyz";
            const string so = "23456789";
            const string dacBiet = "@#$%&*!?";
            string tatCa = hoa + thuong + so + dacBiet;

            using (var rng = RandomNumberGenerator.Create())
            {
                Func<string, char> chon = bo =>
                {
                    var b = new byte[4];
                    rng.GetBytes(b);
                    return bo[(int)(BitConverter.ToUInt32(b, 0) % (uint)bo.Length)];
                };
                var kyTu = new List<char> { chon(hoa), chon(thuong), chon(so), chon(dacBiet) };
                while (kyTu.Count < 12) kyTu.Add(chon(tatCa));

                // Xáo trộn Fisher-Yates
                for (int i = kyTu.Count - 1; i > 0; i--)
                {
                    var b = new byte[4];
                    rng.GetBytes(b);
                    int j = (int)(BitConverter.ToUInt32(b, 0) % (uint)(i + 1));
                    var t = kyTu[i];
                    kyTu[i] = kyTu[j];
                    kyTu[j] = t;
                }
                return new string(kyTu.ToArray());
            }
        }

        private static string Cat(string s, int max) => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max));
    }

    public class KetQuaQuanTri
    {
        public bool ThanhCong { get; set; }
        public string ThongBao { get; set; }
        public int? MaMoi { get; set; }

        public static KetQuaQuanTri Ok(string thongBao, int? maMoi = null) => new KetQuaQuanTri { ThanhCong = true, ThongBao = thongBao, MaMoi = maMoi };
        public static KetQuaQuanTri Loi(string thongBao) => new KetQuaQuanTri { ThanhCong = false, ThongBao = thongBao };
    }

    public class TrangThaiTaiKhoan
    {
        public int MaNguoiDung { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string VaiTro { get; set; }
        public bool HoatDong { get; set; }
        public string LyDoKhoa { get; set; }
        public DateTime? NgayKhoa { get; set; }
        public DateTime? KhoaDenNgay { get; set; }
        public bool CanDoiMatKhau { get; set; }
        public string TenAdminKhoa { get; set; }
    }

    public class NhatKyQuanTriItem
    {
        public int MaNhatKy { get; set; }
        public string HanhDong { get; set; }
        public string LyDo { get; set; }
        public DateTime NgayTao { get; set; }
        public string TenAdmin { get; set; }

        public string TenHanhDong
        {
            get
            {
                switch (HanhDong)
                {
                    case AccountAdminService.HdKhoa: return "Khóa tài khoản";
                    case AccountAdminService.HdMoKhoa: return "Mở khóa";
                    case AccountAdminService.HdCanhCao: return "Cảnh cáo";
                    case AccountAdminService.HdTaoNhanVien: return "Tạo tài khoản";
                    case AccountAdminService.HdDatLaiMatKhau: return "Đặt lại mật khẩu";
                    case AccountAdminService.HdTamDungKhuyenMai: return "Admin tạm dừng";
                    case AccountAdminService.HdChoChayKhuyenMai: return "Admin cho chạy lại";
                    case AccountAdminService.HdThayPhien: return "Đăng nhập nơi khác";
                    default: return HanhDong;
                }
            }
        }

        public string MauBadge
        {
            get
            {
                switch (HanhDong)
                {
                    case AccountAdminService.HdKhoa:
                    case AccountAdminService.HdTamDungKhuyenMai: return "badge-danger";
                    case AccountAdminService.HdMoKhoa:
                    case AccountAdminService.HdChoChayKhuyenMai: return "badge-success";
                    case AccountAdminService.HdCanhCao:
                    case AccountAdminService.HdThayPhien: return "badge-warning";
                    default: return "badge-primary";
                }
            }
        }
    }
}
