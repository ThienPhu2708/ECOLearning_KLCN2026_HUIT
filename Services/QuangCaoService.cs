using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    /// <summary>
    /// Ảnh quảng cáo popup của chiến dịch và chương trình khuyến mãi (migration 023: cột anhQuangCao, hienPopup).
    /// Đọc / ghi bằng SQL vì cột thêm sau khi sinh model EF; chưa chạy migration thì IsInstalled() = false và mọi trang vẫn chạy như cũ.
    /// Ảnh tải lên đi qua CategoryImageService.ChuanBi (kiểm tra ảnh thật, tối đa 2 MB, thu nhỏ còn 1200 px) rồi lưu ở assets/images/promotions.
    /// </summary>
    public class QuangCaoService
    {
        public const string LoaiChienDich = "ChienDich";
        public const string LoaiChuongTrinh = "ChuongTrinh";
        public const string ThuMucAo = "~/assets/images/promotions/";
        private const string DuongDanGoc = "/assets/images/promotions/";
        private const int SoKhoaToiDaMoiChuongTrinh = 8;

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public QuangCaoService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public bool IsInstalled()
        {
            return db.Database.SqlQuery<int>(@"
                SELECT CASE WHEN COL_LENGTH('dbo.ChienDich', 'anhQuangCao') IS NOT NULL
                             AND COL_LENGTH('dbo.KhuyenMai', 'anhQuangCao') IS NOT NULL THEN 1 ELSE 0 END").Single() == 1;
        }

        private static string Bang(string loai)
        {
            if (loai == LoaiChienDich) return "dbo.ChienDich";
            if (loai == LoaiChuongTrinh) return "dbo.KhuyenMai";
            throw new ArgumentException("Loại quảng cáo không hợp lệ.");
        }

        private static string CotMa(string loai)
        {
            return loai == LoaiChienDich ? "maChienDich" : "maKhuyenMai";
        }

        public QuangCaoHienTai Lay(string loai, string ma)
        {
            if (string.IsNullOrEmpty(ma) || !IsInstalled()) return new QuangCaoHienTai();
            return db.Database.SqlQuery<QuangCaoHienTai>(
                "SELECT anhQuangCao AS Anh, CAST(hienPopup AS BIT) AS HienPopup FROM " + Bang(loai) + " WHERE " + CotMa(loai) + " = @ma",
                new SqlParameter("@ma", ma)).FirstOrDefault() ?? new QuangCaoHienTai();
        }

        /// <summary>Lưu ảnh (đã ghi ra đĩa) và cờ popup. Đường dẫn rỗng / null = bỏ ảnh.</summary>
        public void Dat(string loai, string ma, string duongDan, bool hienPopup)
        {
            db.Database.ExecuteSqlCommand(
                "UPDATE " + Bang(loai) + " SET anhQuangCao = @anh, hienPopup = @hien WHERE " + CotMa(loai) + " = @ma",
                new SqlParameter("@anh", string.IsNullOrWhiteSpace(duongDan) ? (object)DBNull.Value : duongDan),
                new SqlParameter("@hien", hienPopup && !string.IsNullOrWhiteSpace(duongDan)),
                new SqlParameter("@ma", ma));
        }

        /// <summary>
        /// Xử lý phần ảnh của form Marketing sau khi chiến dịch / chương trình đã lưu: ghi ảnh mới (nếu có), xóa ảnh cũ khi thay hoặc tick "Xóa ảnh".
        /// <paramref name="anhMoi"/> đã qua CategoryImageService.ChuanBi ở bước kiểm tra form.
        /// </summary>
        public void LuuTuForm(string loai, string ma, AnhDaXuLy anhMoi, bool xoaAnh, bool hienPopup, HttpServerUtilityBase server)
        {
            if (!IsInstalled()) return;
            var cu = Lay(loai, ma);
            string duongDan = cu.Anh;
            if (anhMoi != null)
            {
                duongDan = GhiFile(anhMoi, ma, server);
                XoaFile(cu.Anh, server);
            }
            else if (xoaAnh)
            {
                XoaFile(cu.Anh, server);
                duongDan = null;
            }
            Dat(loai, ma, duongDan, hienPopup);
        }

        /// <summary>Lỗi kiểm tra phần ảnh của form (trước khi lưu): ảnh sai định dạng / quá lớn, hoặc bật popup mà chưa có ảnh.</summary>
        public string KiemTraForm(string loai, string ma, System.Web.HttpPostedFileBase file, bool xoaAnh, bool hienPopup, out AnhDaXuLy anh)
        {
            anh = null;
            if (!IsInstalled()) return null;
            bool coFileMoi = file != null && file.ContentLength > 0;
            if (coFileMoi)
            {
                string loi;
                anh = CategoryImageService.ChuanBi(file, out loi);
                if (anh == null) return loi;
            }
            bool coAnh = coFileMoi || (!xoaAnh && !string.IsNullOrEmpty(Lay(loai, ma).Anh));
            if (hienPopup && !coAnh) return "Cần chọn ảnh quảng cáo trước khi bật hiển thị popup.";
            return null;
        }

        private static string GhiFile(AnhDaXuLy anh, string ma, HttpServerUtilityBase server)
        {
            string thuMuc = server.MapPath(ThuMucAo);
            Directory.CreateDirectory(thuMuc);
            string ten = ma + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + anh.Duoi;
            File.WriteAllBytes(Path.Combine(thuMuc, ten), anh.DuLieu);
            return DuongDanGoc + ten;
        }

        private static void XoaFile(string duongDan, HttpServerUtilityBase server)
        {
            if (string.IsNullOrWhiteSpace(duongDan) || !duongDan.StartsWith(DuongDanGoc, StringComparison.OrdinalIgnoreCase)) return;
            string ten = Path.GetFileName(duongDan);
            if (string.IsNullOrEmpty(ten)) return;
            try
            {
                string full = Path.Combine(server.MapPath(ThuMucAo), ten);
                if (File.Exists(full)) File.Delete(full);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        // ================================================================== phía học viên

        /// <summary>
        /// Ảnh quảng cáo đang chạy: chiến dịch đang triển khai và chương trình đang chạy (không bị Admin / chiến dịch tạm dừng) có bật popup.
        /// Sắp hết hạn xếp trước.
        /// </summary>
        public List<QuangCaoItem> LayDangChay(int maHocVien = 0)
        {
            var ds = new List<QuangCaoItem>();
            if (!IsInstalled()) return ds;
            try
            {
                ds.AddRange(db.Database.SqlQuery<QuangCaoItem>(@"
                    SELECT N'ChienDich' AS Loai, cd.maChienDich AS Ma, cd.tenChienDich AS Ten, cd.anhQuangCao AS Anh, cd.ngayKetThuc AS NgayKetThuc
                    FROM dbo.ChienDich cd
                    WHERE cd.hienPopup = 1 AND cd.anhQuangCao IS NOT NULL AND cd.anhQuangCao <> N''
                      AND cd.trangThai = N'DangTrienKhai' AND cd.ngayBatDau <= GETDATE() AND cd.ngayKetThuc >= GETDATE()").ToList());
                ds.AddRange(db.Database.SqlQuery<QuangCaoItem>(@"
                    SELECT N'ChuongTrinh' AS Loai, km.maKhuyenMai AS Ma, km.tenKhuyenMai AS Ten, km.anhQuangCao AS Anh, km.ngayKetThuc AS NgayKetThuc
                    FROM dbo.KhuyenMai km
                    LEFT JOIN dbo.ChienDich cd ON cd.maChienDich = km.maChienDich
                    WHERE km.hienPopup = 1 AND km.anhQuangCao IS NOT NULL AND km.anhQuangCao <> N''
                      AND km.trangThai = N'HoatDong' AND ISNULL(km.adminTamDung, 0) = 0
                      AND km.ngayBatDau <= GETDATE() AND km.ngayKetThuc >= GETDATE()
                      AND (cd.maChienDich IS NULL OR cd.trangThai NOT IN (N'TamDung', N'DaHuy'))").ToList());
            }
            catch (Exception)
            {
                return new List<QuangCaoItem>();
            }
            // Ưu đãi dành riêng cho học viên thân thiết chỉ quảng cáo cho người đã đăng nhập và đủ điều kiện
            var promotions = new PromotionService(db);
            var hv = maHocVien > 0 ? promotions.ThongKeHocVien(maHocVien) : null;
            var now = DateTime.Now;
            ds = ds.Where(a => DuocXemQuangCao(promotions, a, hv, now)).ToList();
            return ds.OrderBy(a => a.NgayKetThuc).ThenBy(a => a.Ten).ToList();
        }

        private static bool NguoiXemDuocUuDai(ECOLearning.ViewModels.ChuongTrinhKhuyenMai p, HocVienThanThietItem hv)
        {
            return !p.LaThanThiet || (hv != null && PromotionService.DatTieuChi(p, hv));
        }

        private static bool DuocXemQuangCao(PromotionService promotions, QuangCaoItem a, HocVienThanThietItem hv, DateTime now)
        {
            if (a.Loai == LoaiChuongTrinh)
            {
                var p = promotions.LayTheoMa(a.Ma);
                return p != null && NguoiXemDuocUuDai(p, hv);
            }
            // Chiến dịch: hiện nếu có ít nhất một chương trình đang chạy mà người xem được hưởng (chiến dịch chưa gắn chương trình nào thì giữ lại)
            var dang = promotions.LayDanhSach(maChienDich: a.Ma).Where(p => p.TrangThaiHienThi(now) == "Đang chạy" && !p.ChienDichChan).ToList();
            return dang.Count == 0 || dang.Any(p => NguoiXemDuocUuDai(p, hv));
        }

        /// <summary>Dữ liệu trang ưu đãi; null nếu không có hoặc đã hết hiệu lực.</summary>
        public UuDaiTrangViewModel LayTrang(string loai, string ma, int maHocVien)
        {
            var promotions = new PromotionService(db);
            if (!promotions.IsInstalled() || string.IsNullOrEmpty(ma)) return null;
            var now = DateTime.Now;
            var trang = new UuDaiTrangViewModel { Loai = loai, Ma = ma, DaDangNhap = maHocVien > 0 };
            List<ECOLearning.ViewModels.ChuongTrinhKhuyenMai> chuongTrinh;

            if (loai == LoaiChienDich)
            {
                var cd = new CampaignService(db).LayTheoMa(ma);
                if (cd == null || cd.TrangThaiLuu != "DangTrienKhai" || cd.NgayBatDau > now || cd.NgayKetThuc < now) return null;
                trang.TieuDe = cd.TenChienDich;
                trang.MoTa = cd.NoiDung;
                trang.NgayBatDau = cd.NgayBatDau;
                trang.NgayKetThuc = cd.NgayKetThuc;
                chuongTrinh = promotions.LayDanhSach(maChienDich: ma);
            }
            else if (loai == LoaiChuongTrinh)
            {
                var p = promotions.LayTheoMa(ma);
                if (p == null) return null;
                trang.TieuDe = p.TenKhuyenMai;
                trang.MoTa = string.IsNullOrWhiteSpace(p.ChinhSach) ? p.DieuKienApDung : p.ChinhSach;
                trang.NgayBatDau = p.NgayBatDau;
                trang.NgayKetThuc = p.NgayKetThuc;
                chuongTrinh = new List<ECOLearning.ViewModels.ChuongTrinhKhuyenMai> { p };
            }
            else return null;

            trang.Anh = IsInstalled() ? Lay(loai, ma).Anh : null;

            var adminTamDung = new HashSet<string>(db.Database.SqlQuery<string>(
                "SELECT maKhuyenMai FROM dbo.KhuyenMai WHERE ISNULL(adminTamDung, 0) = 1").ToList());
            var maCoThe = maHocVien > 0
                ? promotions.LayMaChoHocVien(maHocVien).ToDictionary(m => m.MaKhuyenMai, m => m)
                : new Dictionary<string, MaGiamGiaHienThi>();
            HocVienThanThietItem hv = maHocVien > 0 ? promotions.ThongKeHocVien(maHocVien) : null;

            foreach (var p in chuongTrinh.OrderBy(x => x.NgayKetThuc))
            {
                if (p.TrangThaiHienThi(now) != "Đang chạy" || p.ChienDichChan || adminTamDung.Contains(p.MaKhuyenMai)) continue;
                if (!p.LaGiamTrucTiep && !p.CongKhai) continue; // mã kín chỉ gửi riêng, không quảng cáo

                var muc = new UuDaiTrangMuc
                {
                    MaKhuyenMai = p.MaKhuyenMai,
                    Ten = p.TenKhuyenMai,
                    GiaTri = PromotionService.GiaTriNgan(p),
                    GhiChu = p.LaPhanTram && p.GiamToiDa.HasValue ? "Tối đa " + PromotionService.SoTienNgan(p.GiamToiDa.Value) : null,
                    DieuKien = PromotionService.DieuKienNgan(p),
                    NgayKetThuc = p.NgayKetThuc,
                    LaGiamTrucTiep = p.LaGiamTrucTiep,
                    LaThanThiet = p.LaThanThiet,
                    DonToiThieu = p.DonHangToiThieu,
                    MaCode = p.LaGiamTrucTiep ? null : p.MaCode
                };

                if (!NguoiXemDuocUuDai(p, hv))
                {
                    // Ưu đãi thân thiết: khách hoặc học viên chưa đủ điều kiện không thấy mã
                    muc.MaCode = null;
                    muc.ThongBao = maHocVien > 0
                        ? "Ưu đãi dành riêng cho học viên thân thiết. Bạn cần " + PromotionService.PhanConThieu(p, hv) + " để nhận ưu đãi này."
                        : "Ưu đãi dành riêng cho học viên thân thiết. Đăng nhập để kiểm tra điều kiện của bạn.";
                }

                if (!p.LaGiamTrucTiep && muc.MaCode != null)
                {
                    MaGiamGiaHienThi ma2;
                    if (maCoThe.TryGetValue(p.MaKhuyenMai, out ma2))
                    {
                        muc.DaLuu = ma2.DaLuu;
                        muc.CoTheLuu = !ma2.DaLuu;
                    }
                }

                DienKhoaHoc(muc, p);
                trang.Muc.Add(muc);
            }
            return trang;
        }

        private void DienKhoaHoc(UuDaiTrangMuc muc, ECOLearning.ViewModels.ChuongTrinhKhuyenMai p)
        {
            IQueryable<KhoaHoc> q = db.KhoaHoc.Where(k => k.trangThai == CourseStatus.DaXuatBan);
            if (p.PhamVi == "KhoaHoc")
            {
                var ids = new PromotionService(db).LayKhoaHocApDung(p.MaKhuyenMai).Select(k => k.MaKhoaHoc).ToList();
                q = q.Where(k => ids.Contains(k.maKhoaHoc));
            }
            else if (p.PhamVi == "DanhMuc")
            {
                string dm = p.MaDanhMucApDung;
                q = q.Where(k => k.maDanhMuc == dm);
            }
            else
            {
                muc.ApDungMoiKhoa = true;
                return;
            }

            muc.TongSoKhoa = q.Count();
            foreach (var k in q.OrderBy(k => k.tenKhoaHoc).Take(SoKhoaToiDaMoiChuongTrinh).ToList())
            {
                var gia = CourseDiscountService.Tinh(k);
                muc.Khoa.Add(new UuDaiKhoa
                {
                    MaKhoaHoc = k.maKhoaHoc,
                    Ten = k.tenKhoaHoc,
                    AnhBia = k.anhBia,
                    GiaGoc = gia.GiaGoc,
                    Gia = gia.Gia,
                    PhanTram = gia.PhanTram
                });
            }
        }
    }
}
