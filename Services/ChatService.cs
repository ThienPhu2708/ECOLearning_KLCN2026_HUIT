using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text.RegularExpressions;
using ECOLearning.Models;
using ECOLearning.ViewModels;

namespace ECOLearning.Services
{
    public class ChatService
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public ChatService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public bool DaCaiDat()
        {
            try
            {
                return db.Database.SqlQuery<int>(@"
                    SELECT CASE WHEN OBJECT_ID(N'dbo.HoiThoai', N'U') IS NOT NULL 
                                 AND OBJECT_ID(N'dbo.TinNhan', N'U') IS NOT NULL THEN 1 ELSE 0 END").Single() == 1;
            }
            catch
            {
                return false;
            }
        }

        public class KetQuaGuiTin
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public TinNhanItemViewModel TinNhan { get; set; }
            public bool CanhBaoThanhToanNgoai { get; set; }
            public int MaHoiThoai { get; set; }
            public int MaNguoiNhan { get; set; }
        }

        // ==========================================
        // 1. TÌM HOẶC KHỞI TẠO HỘI THOẠI (DÀNH CHO HỌC VIÊN)
        // ==========================================
        public HoiThoaiItemViewModel TimHoacMoHoiThoai(string maKhoaHoc, int maHocVien)
        {
            if (!DaCaiDat()) return null;

            // Kiểm tra học viên đã đăng ký khóa học này chưa
            var dk = db.DangKyKhoaHoc.FirstOrDefault(d => d.maKhoaHoc == maKhoaHoc && d.maHocVien == maHocVien);
            if (dk == null) return null;

            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == maKhoaHoc);
            if (course == null) return null;

            int maGiaoVien = course.maGiaoVien;

            // Tìm hội thoại sẵn có
            var hoiThoai = db.Database.SqlQuery<HoiThoaiItemViewModel>(@"
                SELECT h.maHoiThoai AS MaHoiThoai,
                       h.maKhoaHoc AS MaKhoaHoc,
                       k.tenKhoaHoc AS TenKhoaHoc,
                       k.anhBia AS AnhBiaKhoaHoc,
                       h.maHocVien AS MaHocVien,
                       uHv.hoTen AS TenHocVien,
                       uHv.anhDaiDien AS AnhDaiDienHocVien,
                       h.maGiaoVien AS MaGiaoVien,
                       uGv.hoTen AS TenGiaoVien,
                       uGv.anhDaiDien AS AnhDaiDienGiaoVien,
                       h.tinCuoiTomTat AS TinCuoiTomTat,
                       h.tinCuoiLuc AS TinCuoiLuc,
                       h.trangThai AS TrangThai
                FROM dbo.HoiThoai h
                JOIN dbo.KhoaHoc k ON h.maKhoaHoc = k.maKhoaHoc
                JOIN dbo.NguoiDung uHv ON h.maHocVien = uHv.maNguoiDung
                JOIN dbo.NguoiDung uGv ON h.maGiaoVien = uGv.maNguoiDung
                WHERE h.maKhoaHoc = @courseId AND h.maHocVien = @studentId",
                new SqlParameter("@courseId", maKhoaHoc),
                new SqlParameter("@studentId", maHocVien)).FirstOrDefault();

            if (hoiThoai == null)
            {
                // Tạo mới hội thoại giữa học viên và giáo viên
                string trangThai = (course.trangThai == CourseStatus.KhoaVinhVien) ? "ChiDoc" : "Mo";

                int newId = db.Database.SqlQuery<int>(@"
                    INSERT INTO dbo.HoiThoai (maKhoaHoc, maHocVien, maGiaoVien, ngayTao, trangThai, hocVienDocDen, giaoVienDocDen)
                    VALUES (@courseId, @studentId, @teacherId, GETDATE(), @status, 0, 0);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new SqlParameter("@courseId", maKhoaHoc),
                    new SqlParameter("@studentId", maHocVien),
                    new SqlParameter("@teacherId", maGiaoVien),
                    new SqlParameter("@status", trangThai)).Single();

                hoiThoai = LayChiTietHoiThoai(newId, maHocVien);
            }
            else
            {
                // Cập nhật trạng thái chỉ đọc nếu khóa học đã bị khóa vĩnh viễn
                if (course.trangThai == CourseStatus.KhoaVinhVien && hoiThoai.TrangThai != "ChiDoc")
                {
                    db.Database.ExecuteSqlCommand("UPDATE dbo.HoiThoai SET trangThai = 'ChiDoc' WHERE maHoiThoai = @id",
                        new SqlParameter("@id", hoiThoai.MaHoiThoai));
                    hoiThoai.TrangThai = "ChiDoc";
                }
            }

            return hoiThoai;
        }

        public HoiThoaiItemViewModel LayChiTietHoiThoai(int maHoiThoai, int currentUserId)
        {
            if (!DaCaiDat()) return null;

            var item = db.Database.SqlQuery<HoiThoaiItemViewModel>(@"
                SELECT h.maHoiThoai AS MaHoiThoai,
                       h.maKhoaHoc AS MaKhoaHoc,
                       k.tenKhoaHoc AS TenKhoaHoc,
                       k.anhBia AS AnhBiaKhoaHoc,
                       h.maHocVien AS MaHocVien,
                       uHv.hoTen AS TenHocVien,
                       uHv.anhDaiDien AS AnhDaiDienHocVien,
                       h.maGiaoVien AS MaGiaoVien,
                       uGv.hoTen AS TenGiaoVien,
                       uGv.anhDaiDien AS AnhDaiDienGiaoVien,
                       h.tinCuoiTomTat AS TinCuoiTomTat,
                       h.tinCuoiLuc AS TinCuoiLuc,
                       h.trangThai AS TrangThai,
                       ISNULL((
                           SELECT TOP 1 t.maNguoiGui 
                           FROM dbo.TinNhan t 
                           WHERE t.maHoiThoai = h.maHoiThoai 
                           ORDER BY t.maTinNhan DESC
                       ), 0) AS MaNguoiGuiCuoi
                FROM dbo.HoiThoai h
                JOIN dbo.KhoaHoc k ON h.maKhoaHoc = k.maKhoaHoc
                JOIN dbo.NguoiDung uHv ON h.maHocVien = uHv.maNguoiDung
                JOIN dbo.NguoiDung uGv ON h.maGiaoVien = uGv.maNguoiDung
                WHERE h.maHoiThoai = @id",
                new SqlParameter("@id", maHoiThoai)).FirstOrDefault();

            if (item != null && item.MaHocVien != currentUserId && item.MaGiaoVien != currentUserId)
                return null;

            if (item != null)
            {
                bool isStudent = item.MaHocVien == currentUserId;
                int docDen = LayMocDocDen(maHoiThoai, isStudent);
                item.SoTinChuaDoc = DemTinChuaDocHoiThoai(maHoiThoai, docDen, currentUserId);
                item.DaTraLoi = item.MaNguoiGuiCuoi == item.MaGiaoVien;
            }

            return item;
        }

        // ==========================================
        // 2. GỬI TIN NHẮN (KIỂM TRA NGHIỆP VỤ & RATE LIMIT)
        // ==========================================
        public static string TenAnh(string content)
        {
            var match = Regex.Match(content ?? "", @"^\[eco-image:([a-f0-9]{32}\.jpg)\]$");
            return match.Success ? match.Groups[1].Value : null;
        }

        public KetQuaGuiTin GuiAnh(int room, int user, string filename)
        {
            if (TenAnh("[eco-image:" + filename + "]") == null)
                return new KetQuaGuiTin { Success = false, Message = "Ảnh không hợp lệ." };
            return GuiTin(room, user, "[eco-image:" + filename + "]", true);
        }

        public KetQuaGuiTin GuiTinNhan(int maHoiThoai, int maNguoiGui, string noiDung)
        {
            return GuiTin(maHoiThoai, maNguoiGui, noiDung, false);
        }

        private KetQuaGuiTin GuiTin(int maHoiThoai, int maNguoiGui, string noiDung, bool laAnh)
        {
            if (!laAnh && (noiDung ?? "").Trim().StartsWith("[eco-image:", StringComparison.Ordinal))
                return new KetQuaGuiTin { Success = false, Message = "Hãy dùng nút gửi ảnh." };
            if (!DaCaiDat())
                return new KetQuaGuiTin { Success = false, Message = "Chức năng chat chưa sẵn sàng." };

            if (string.IsNullOrWhiteSpace(noiDung))
                return new KetQuaGuiTin { Success = false, Message = "Nội dung tin nhắn không được để trống." };

            noiDung = noiDung.Trim();
            if (noiDung.Length > 2000)
                return new KetQuaGuiTin { Success = false, Message = "Tin nhắn tối đa 2.000 ký tự." };

            // Rate limit: tối đa 20 tin / phút
            if (RateLimiter.VuotGioiHan("chat:u" + maNguoiGui, 20, TimeSpan.FromMinutes(1)))
            {
                return new KetQuaGuiTin { Success = false, Message = "Bạn đang gửi tin quá nhanh. Vui lòng thử lại sau giây lát." };
            }

            // Lấy thông tin hội thoại
            var ht = db.Database.SqlQuery<HoiThoaiItemViewModel>(@"
                SELECT h.maHoiThoai AS MaHoiThoai, h.maKhoaHoc AS MaKhoaHoc, h.maHocVien AS MaHocVien, 
                       h.maGiaoVien AS MaGiaoVien, h.trangThai AS TrangThai
                FROM dbo.HoiThoai h WHERE h.maHoiThoai = @id",
                new SqlParameter("@id", maHoiThoai)).FirstOrDefault();

            if (ht == null)
                return new KetQuaGuiTin { Success = false, Message = "Không tìm thấy cuộc hội thoại." };

            if (ht.TrangThai == "ChiDoc")
                return new KetQuaGuiTin { Success = false, Message = "Hội thoại này hiện ở chế độ chỉ đọc do khóa học hoặc tài khoản đã bị khóa." };

            if (ht.TrangThai == "BiBaoCao")
                return new KetQuaGuiTin { Success = false, Message = "Hội thoại đang tạm dừng để quản trị viên kiểm tra nội dung vi phạm." };

            // Xác định người gửi và người nhận
            bool laHocVien = maNguoiGui == ht.MaHocVien;
            bool laGiaoVien = maNguoiGui == ht.MaGiaoVien;

            if (!laHocVien && !laGiaoVien)
                return new KetQuaGuiTin { Success = false, Message = "Bạn không có quyền tham gia hội thoại này." };

            int maNguoiNhan = laHocVien ? ht.MaGiaoVien : ht.MaHocVien;

            // QUY TẮC THEN CHỐT: Giáo viên không thể nhắn trước. Học viên phải nhắn trước!
            if (laGiaoVien)
            {
                int soTinCuaHocVien = db.Database.SqlQuery<int>(@"
                    SELECT COUNT(*) FROM dbo.TinNhan 
                    WHERE maHoiThoai = @id AND maNguoiGui = @studentId",
                    new SqlParameter("@id", maHoiThoai),
                    new SqlParameter("@studentId", ht.MaHocVien)).Single();

                if (soTinCuaHocVien == 0)
                {
                    return new KetQuaGuiTin
                    {
                        Success = false,
                        Message = "Giáo viên không thể chủ động nhắn trước cho học viên. Vui lòng chờ học viên đặt câu hỏi."
                    };
                }
            }

            // Kiểm tra cảnh báo sđt, tài khoản ngân hàng (chống giao dịch ngoài nền tảng)
            bool canhBao = KiemTraThongTinNhayCam(noiDung);

            // Tóm tắt tin cuối (tối đa 90 ký tự)
            string tomTat = laAnh ? "Đã gửi một ảnh" : noiDung.Length > 90 ? noiDung.Substring(0, 87) + "..." : noiDung;

            // Ghi tin nhắn vào CSDL
            int maTinMoi = db.Database.SqlQuery<int>(@"
                INSERT INTO dbo.TinNhan (maHoiThoai, maNguoiGui, noiDung, ngayGui, daThuHoi)
                VALUES (@maHoiThoai, @maNguoiGui, @noiDung, GETDATE(), 0);
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new SqlParameter("@maHoiThoai", maHoiThoai),
                new SqlParameter("@maNguoiGui", maNguoiGui),
                new SqlParameter("@noiDung", noiDung)).Single();

            // Cập nhật thông tin hội thoại & mốc đã đọc của người gửi
            string updateSql = laHocVien
                ? @"UPDATE dbo.HoiThoai 
                    SET tinCuoiLuc = GETDATE(), tinCuoiTomTat = @tomTat, hocVienDocDen = @maTinMoi 
                    WHERE maHoiThoai = @id"
                : @"UPDATE dbo.HoiThoai 
                    SET tinCuoiLuc = GETDATE(), tinCuoiTomTat = @tomTat, giaoVienDocDen = @maTinMoi 
                    WHERE maHoiThoai = @id";

            db.Database.ExecuteSqlCommand(updateSql,
                new SqlParameter("@tomTat", tomTat),
                new SqlParameter("@maTinMoi", maTinMoi),
                new SqlParameter("@id", maHoiThoai));

            var user = db.NguoiDung.Find(maNguoiGui);

            var tinItem = new TinNhanItemViewModel
            {
                MaTinNhan = maTinMoi,
                MaHoiThoai = maHoiThoai,
                MaNguoiGui = maNguoiGui,
                TenNguoiGui = user != null ? user.hoTen : "Người dùng",
                AnhDaiDien = user?.anhDaiDien ?? SessionNguoiDung.AnhDaiDienMacDinh,
                NoiDung = noiDung,
                NgayGui = DateTime.Now,
                DaThuHoi = false,
                LaCuaToi = true,
                CoTheThuHoi = true
            };

            return new KetQuaGuiTin
            {
                Success = true,
                TinNhan = tinItem,
                CanhBaoThanhToanNgoai = canhBao,
                MaHoiThoai = maHoiThoai,
                MaNguoiNhan = maNguoiNhan
            };
        }

        // ==========================================
        // 3. THU HỒI TIN NHẮN (TRONG VÒNG 5 PHÚT)
        // ==========================================
        public bool ThuHoiTinNhan(int maTinNhan, int maNguoiYeuCau, int? maHoiThoai = null)
        {
            if (!DaCaiDat()) return false;

            var tin = db.Database.SqlQuery<TinNhanItemViewModel>(@"
                SELECT maTinNhan AS MaTinNhan, maHoiThoai AS MaHoiThoai, maNguoiGui AS MaNguoiGui, 
                       ngayGui AS NgayGui, daThuHoi AS DaThuHoi
                FROM dbo.TinNhan WHERE maTinNhan = @id",
                new SqlParameter("@id", maTinNhan)).FirstOrDefault();

            if (tin == null || tin.DaThuHoi || tin.MaNguoiGui != maNguoiYeuCau
                || (maHoiThoai.HasValue && tin.MaHoiThoai != maHoiThoai.Value))
                return false;

            // Kiểm tra thời gian: <= 5 phút (300 giây)
            if ((DateTime.Now - tin.NgayGui).TotalMinutes > 5)
                return false;

            db.Database.ExecuteSqlCommand(@"
                UPDATE dbo.TinNhan SET daThuHoi = 1 WHERE maTinNhan = @id",
                new SqlParameter("@id", maTinNhan));

            return true;
        }

        // ==========================================
        // 4. ĐÁNH DẤU ĐÃ ĐỌC
        // ==========================================
        public void DanhDauDaDoc(int maHoiThoai, int maNguoiDoc)
        {
            if (!DaCaiDat()) return;

            var ht = db.Database.SqlQuery<HoiThoaiItemViewModel>(@"
                SELECT maHocVien AS MaHocVien, maGiaoVien AS MaGiaoVien FROM dbo.HoiThoai WHERE maHoiThoai = @id",
                new SqlParameter("@id", maHoiThoai)).FirstOrDefault();

            if (ht == null) return;

            int maxTinId = db.Database.SqlQuery<int?>(@"
                SELECT MAX(maTinNhan) FROM dbo.TinNhan WHERE maHoiThoai = @id",
                new SqlParameter("@id", maHoiThoai)).FirstOrDefault() ?? 0;

            if (maxTinId <= 0) return;

            if (maNguoiDoc == ht.MaHocVien)
            {
                db.Database.ExecuteSqlCommand("UPDATE dbo.HoiThoai SET hocVienDocDen = @docDen WHERE maHoiThoai = @id",
                    new SqlParameter("@docDen", maxTinId),
                    new SqlParameter("@id", maHoiThoai));
            }
            else if (maNguoiDoc == ht.MaGiaoVien)
            {
                db.Database.ExecuteSqlCommand("UPDATE dbo.HoiThoai SET giaoVienDocDen = @docDen WHERE maHoiThoai = @id",
                    new SqlParameter("@docDen", maxTinId),
                    new SqlParameter("@id", maHoiThoai));
            }
        }

        // ==========================================
        // 5. LẤY DANH SÁCH HỘI THOẠI & TIN NHẮN
        // ==========================================
        public List<HoiThoaiItemViewModel> LayDanhSachHoiThoaiHocVien(int maHocVien)
        {
            if (!DaCaiDat()) return new List<HoiThoaiItemViewModel>();

            var list = db.Database.SqlQuery<HoiThoaiItemViewModel>(@"
                SELECT h.maHoiThoai AS MaHoiThoai,
                       h.maKhoaHoc AS MaKhoaHoc,
                       k.tenKhoaHoc AS TenKhoaHoc,
                       k.anhBia AS AnhBiaKhoaHoc,
                       h.maHocVien AS MaHocVien,
                       h.maGiaoVien AS MaGiaoVien,
                       uGv.hoTen AS TenGiaoVien,
                       uGv.anhDaiDien AS AnhDaiDienGiaoVien,
                       h.tinCuoiTomTat AS TinCuoiTomTat,
                       h.tinCuoiLuc AS TinCuoiLuc,
                       h.hocVienDocDen AS SoTinChuaDoc, -- tạm dùng để chứa mốc đọc
                       ISNULL((
                           SELECT TOP 1 t.maNguoiGui
                           FROM dbo.TinNhan t
                           WHERE t.maHoiThoai = h.maHoiThoai
                           ORDER BY t.maTinNhan DESC
                       ), 0) AS MaNguoiGuiCuoi,
                       h.trangThai AS TrangThai
                FROM dbo.HoiThoai h
                JOIN dbo.KhoaHoc k ON h.maKhoaHoc = k.maKhoaHoc
                JOIN dbo.NguoiDung uGv ON h.maGiaoVien = uGv.maNguoiDung
                WHERE h.maHocVien = @studentId
                ORDER BY ISNULL(h.tinCuoiLuc, h.ngayTao) DESC",
                new SqlParameter("@studentId", maHocVien)).ToList();

            foreach (var item in list)
            {
                int docDen = item.SoTinChuaDoc;
                item.SoTinChuaDoc = DemTinChuaDocHoiThoai(item.MaHoiThoai, docDen, maHocVien);
                item.DaTraLoi = item.MaNguoiGuiCuoi == item.MaGiaoVien;
            }

            return list;
        }

        public List<HoiThoaiItemViewModel> LayDanhSachHoiThoaiGiaoVien(int maGiaoVien, string maKhoaHoc = null, string status = "all")
        {
            if (!DaCaiDat()) return new List<HoiThoaiItemViewModel>();

            string sql = @"
                SELECT h.maHoiThoai AS MaHoiThoai,
                       h.maKhoaHoc AS MaKhoaHoc,
                       k.tenKhoaHoc AS TenKhoaHoc,
                       k.anhBia AS AnhBiaKhoaHoc,
                       h.maHocVien AS MaHocVien,
                       uHv.hoTen AS TenHocVien,
                       uHv.anhDaiDien AS AnhDaiDienHocVien,
                       h.maGiaoVien AS MaGiaoVien,
                       h.tinCuoiTomTat AS TinCuoiTomTat,
                       h.tinCuoiLuc AS TinCuoiLuc,
                       h.giaoVienDocDen AS SoTinChuaDoc, -- tạm dùng chứa mốc đọc
                       h.trangThai AS TrangThai,
                       ISNULL((
                           SELECT TOP 1 t.maNguoiGui 
                           FROM dbo.TinNhan t 
                           WHERE t.maHoiThoai = h.maHoiThoai 
                           ORDER BY t.maTinNhan DESC
                       ), 0) AS MaNguoiGuiCuoi
                FROM dbo.HoiThoai h
                JOIN dbo.KhoaHoc k ON h.maKhoaHoc = k.maKhoaHoc
                JOIN dbo.NguoiDung uHv ON h.maHocVien = uHv.maNguoiDung
                WHERE h.maGiaoVien = @teacherId";

            var pars = new List<SqlParameter> { new SqlParameter("@teacherId", maGiaoVien) };

            if (!string.IsNullOrEmpty(maKhoaHoc))
            {
                sql += " AND h.maKhoaHoc = @courseId";
                pars.Add(new SqlParameter("@courseId", maKhoaHoc));
            }

            sql += " ORDER BY ISNULL(h.tinCuoiLuc, h.ngayTao) DESC";

            var list = db.Database.SqlQuery<HoiThoaiItemViewModel>(sql, pars.ToArray()).ToList();

            foreach (var item in list)
            {
                int docDen = item.SoTinChuaDoc;
                item.SoTinChuaDoc = DemTinChuaDocHoiThoai(item.MaHoiThoai, docDen, maGiaoVien);
                item.DaTraLoi = item.MaNguoiGuiCuoi == maGiaoVien;
            }

            // Lọc theo status
            if (status == "unread")
            {
                list = list.Where(x => x.SoTinChuaDoc > 0).ToList();
            }
            else if (status == "unanswered")
            {
                list = list.Where(x => !x.DaTraLoi && x.TinCuoiLuc.HasValue).ToList();
            }

            return list;
        }

        public List<TinNhanItemViewModel> LayLichSuTinNhan(int maHoiThoai, int currentUserId, int limit = 50, int? truocMaTinNhan = null)
        {
            if (!DaCaiDat()) return new List<TinNhanItemViewModel>();
            if (LayChiTietHoiThoai(maHoiThoai, currentUserId) == null)
                return new List<TinNhanItemViewModel>();

            string sql = @"
                SELECT TOP (@limit)
                       t.maTinNhan AS MaTinNhan,
                       t.maHoiThoai AS MaHoiThoai,
                       t.maNguoiGui AS MaNguoiGui,
                       u.hoTen AS TenNguoiGui,
                       u.anhDaiDien AS AnhDaiDien,
                       t.noiDung AS NoiDung,
                       t.ngayGui AS NgayGui,
                       t.daThuHoi AS DaThuHoi
                FROM dbo.TinNhan t
                JOIN dbo.NguoiDung u ON t.maNguoiGui = u.maNguoiDung
                WHERE t.maHoiThoai = @id";

            var pars = new List<SqlParameter>
            {
                new SqlParameter("@limit", limit),
                new SqlParameter("@id", maHoiThoai)
            };

            if (truocMaTinNhan.HasValue && truocMaTinNhan.Value > 0)
            {
                sql += " AND t.maTinNhan < @beforeId";
                pars.Add(new SqlParameter("@beforeId", truocMaTinNhan.Value));
            }

            sql += " ORDER BY t.maTinNhan DESC";

            var list = db.Database.SqlQuery<TinNhanItemViewModel>(sql, pars.ToArray()).ToList();

            // Sắp xếp lại từ cũ tới mới để hiển thị tin nhắn từ trên xuống dưới
            list.Reverse();

            foreach (var t in list)
            {
                t.LaCuaToi = t.MaNguoiGui == currentUserId;
                t.CoTheThuHoi = t.LaCuaToi && !t.DaThuHoi && (DateTime.Now - t.NgayGui).TotalMinutes <= 5;
                if (t.DaThuHoi)
                {
                    t.NoiDung = "Tin nhắn đã được thu hồi";
                }
                if (string.IsNullOrEmpty(t.AnhDaiDien))
                {
                    t.AnhDaiDien = SessionNguoiDung.AnhDaiDienMacDinh;
                }
            }

            return list;
        }

        // ==========================================
        // 6. THÔNG TIN HỌC VIÊN (BẢNG BÊN CẠNH KHUNG CHAT GIÁO VIÊN)
        // ==========================================
        public ThongTinHocVienChatViewModel LayThongTinHocVien(string maKhoaHoc, int maHocVien)
        {
            var student = db.NguoiDung.Find(maHocVien);
            var course = db.KhoaHoc.Find(maKhoaHoc);
            if (student == null || course == null) return null;

            var dk = db.DangKyKhoaHoc.FirstOrDefault(d => d.maKhoaHoc == maKhoaHoc && d.maHocVien == maHocVien);

            // Tiến độ bài học
            var totalLessons = db.BaiHoc.Count(b => (b.maChuong != null && b.ChuongHoc.maKhoaHoc == maKhoaHoc)
                                                 || (b.maChuong == null && b.maBaiHoc.StartsWith(maKhoaHoc + "_")));
            var completedLessons = db.TienDoBaiHoc.Count(t => t.maHocVien == maHocVien && t.daHoanThanh == true
                                                          && ((t.BaiHoc.ChuongHoc != null && t.BaiHoc.ChuongHoc.maKhoaHoc == maKhoaHoc)
                                                              || (t.BaiHoc.maChuong == null && t.maBaiHoc.StartsWith(maKhoaHoc + "_"))));

            int progress = totalLessons > 0 ? (int)Math.Floor((double)completedLessons / totalLessons * 100) : 0;

            // Bài học gần nhất
            var lastLesson = db.TienDoBaiHoc
                .Where(t => t.maHocVien == maHocVien && t.BaiHoc.ChuongHoc.maKhoaHoc == maKhoaHoc)
                .OrderByDescending(t => t.lanTruyCapGanNhat)
                .Select(t => t.BaiHoc.tenBaiHoc)
                .FirstOrDefault() ?? "Chưa bắt đầu học";

            // Điểm quiz & chứng chỉ
            var quizSvc = new QuizService(db);
            decimal? bestScore = null;
            string quizStatus = "Chưa làm quiz";
            string certStatus = "Chưa cấp";

            if (quizSvc.Installed())
            {
                var quizState = quizSvc.Student(maKhoaHoc, maHocVien);
                if (quizState != null)
                {
                    bestScore = quizState.BestScore;
                    quizStatus = quizState.QuizStatus;
                    certStatus = quizState.CertificateStatus;
                }
            }

            return new ThongTinHocVienChatViewModel
            {
                MaHocVien = maHocVien,
                HoTen = student.hoTen,
                Email = student.email,
                SoDienThoai = student.soDienThoai ?? "Chưa cung cấp",
                AnhDaiDien = student.anhDaiDien ?? SessionNguoiDung.AnhDaiDienMacDinh,
                MaKhoaHoc = maKhoaHoc,
                TenKhoaHoc = course.tenKhoaHoc,
                NgayMua = dk?.ngayDangKy,
                TienDoPhanTram = Math.Min(100, progress),
                BaiDangHoc = lastLesson,
                DiemQuiz = bestScore,
                TrangThaiQuiz = quizStatus,
                TrangThaiChungChi = certStatus
            };
        }

        // ==========================================
        // 7. ĐẾM TIN CHƯA ĐỌC (CHO MENU & BADGE)
        // ==========================================
        public int DemTongTinChuaDoc(int maNguoiDung, bool laGiaoVien)
        {
            if (!DaCaiDat()) return 0;

            try
            {
                string sql = laGiaoVien
                    ? @"SELECT COUNT(*) 
                        FROM dbo.TinNhan t
                        JOIN dbo.HoiThoai h ON t.maHoiThoai = h.maHoiThoai
                        WHERE h.maGiaoVien = @userId 
                          AND t.maNguoiGui <> @userId
                          AND t.maTinNhan > h.giaoVienDocDen"
                    : @"SELECT COUNT(*) 
                        FROM dbo.TinNhan t
                        JOIN dbo.HoiThoai h ON t.maHoiThoai = h.maHoiThoai
                        WHERE h.maHocVien = @userId 
                          AND t.maNguoiGui <> @userId
                          AND t.maTinNhan > h.hocVienDocDen";

                return db.Database.SqlQuery<int>(sql, new SqlParameter("@userId", maNguoiDung)).Single();
            }
            catch
            {
                return 0;
            }
        }

        public bool BaoCaoHoiThoai(int maHoiThoai, int maNguoiBaoCao, string lyDo)
        {
            if (!DaCaiDat()) return false;
            if (LayChiTietHoiThoai(maHoiThoai, maNguoiBaoCao) == null) return false;

            db.Database.ExecuteSqlCommand("UPDATE dbo.HoiThoai SET trangThai = 'BiBaoCao' WHERE maHoiThoai = @id",
                new SqlParameter("@id", maHoiThoai));

            return true;
        }

        // ==========================================
        // HELPER FUNCTIONS
        // ==========================================
        private int LayMocDocDen(int maHoiThoai, bool isStudent)
        {
            string col = isStudent ? "hocVienDocDen" : "giaoVienDocDen";
            return db.Database.SqlQuery<int?>("SELECT " + col + " FROM dbo.HoiThoai WHERE maHoiThoai = @id",
                new SqlParameter("@id", maHoiThoai)).FirstOrDefault() ?? 0;
        }

        private int DemTinChuaDocHoiThoai(int maHoiThoai, int docDen, int currentUserId)
        {
            return db.Database.SqlQuery<int>(@"
                SELECT COUNT(*) FROM dbo.TinNhan 
                WHERE maHoiThoai = @id AND maTinNhan > @docDen AND maNguoiGui <> @userId",
                new SqlParameter("@id", maHoiThoai),
                new SqlParameter("@docDen", docDen),
                new SqlParameter("@userId", currentUserId)).Single();
        }

        private bool KiemTraThongTinNhayCam(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            // Kiểm tra số điện thoại (10-11 số liên tiếp hoặc có dấu cách/chấm)
            var phoneRegex = new Regex(@"(0|\+84)[3|5|7|8|9][0-9]{8}\b|(\d{3,4}[\.\s]\d{3,4}[\.\s]\d{3,4})");
            // Kiểm tra số tài khoản ngân hàng (dãy số 9-16 chữ số hoặc từ khóa stk, tk, ngân hàng, momo, vnpay)
            var bankRegex = new Regex(@"(stk|so\s*tai\s*khoan|tai\s*khoan|ngan\s*hang|momo|zalopay)\s*[:=\s]*\d{6,16}", RegexOptions.IgnoreCase);

            return phoneRegex.IsMatch(text) || bankRegex.IsMatch(text);
        }
    }
}
