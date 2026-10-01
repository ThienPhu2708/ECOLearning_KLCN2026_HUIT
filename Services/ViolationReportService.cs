using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Nghiệp vụ Báo cáo vi phạm khóa học (hậu kiểm nội dung):
    /// Học viên báo cáo -> Admin bác bỏ, Tạm khóa (kèm hạn phản hồi), Mở lại hoặc Khóa vĩnh viễn.
    /// (Phần giáo viên xem và phản hồi vi phạm chưa làm.)
    /// Bảng được tạo bởi Database/Migrations/002_BaoCaoViPham.sql và 003_BaoCaoViPham_BaiHoc.sql.
    /// Các phương thức thay đổi dữ liệu trả về null nếu thành công, ngược lại trả về thông báo lỗi.
    /// </summary>
    public class ViolationReportService
    {
        public const string ReportPending = "Chờ xử lý";
        public const string ReportHandled = "Đã xử lý";
        public const string ReportDismissed = "Đã bác bỏ";

        public const string ActionDismiss = "Bác bỏ";
        public const string ActionLock = "Tạm khóa";
        public const string ActionReopen = "Mở lại";
        public const string ActionPermanentLock = "Khóa vĩnh viễn";

        public const int MinDeadlineDays = 1;
        public const int MaxDeadlineDays = 30;

        public static readonly string[] ViolationTypes =
        {
            "Nội dung không phù hợp",
            "Sai sót kiến thức / thông tin",
            "Nội dung không đúng mô tả",
            "Video / tài liệu lỗi, không xem được",
            "Vi phạm bản quyền",
            "Khác"
        };

        public const string NotInstalledMessage =
            "Chưa khởi tạo dữ liệu báo cáo vi phạm. Vui lòng chạy lần lượt Database/Migrations/002_BaoCaoViPham.sql và 003_BaoCaoViPham_BaiHoc.sql!";

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public ViolationReportService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        /// <summary>Kiểm tra đã chạy đủ script 002 và 003 hay chưa.</summary>
        public bool IsInstalled()
        {
            try
            {
                return db.Database.SqlQuery<int>(@"
                    SELECT CASE WHEN OBJECT_ID(N'dbo.BaoCaoViPham', N'U') IS NOT NULL
                                 AND OBJECT_ID(N'dbo.XuLyViPham', N'U') IS NOT NULL
                                 AND COL_LENGTH(N'dbo.BaoCaoViPham', N'maBaiHoc') IS NOT NULL
                           THEN 1 ELSE 0 END").FirstOrDefault() == 1;
            }
            catch
            {
                return false;
            }
        }

        #region Học viên báo cáo

        public bool HasPendingReport(string courseId, int userId)
        {
            return db.Database.SqlQuery<int>(
                "SELECT COUNT(1) FROM BaoCaoViPham WHERE maKhoaHoc = @maKhoaHoc AND maNguoiBaoCao = @userId AND trangThai = @trangThai",
                new SqlParameter("@maKhoaHoc", courseId),
                new SqlParameter("@userId", userId),
                new SqlParameter("@trangThai", ReportPending)
            ).FirstOrDefault() > 0;
        }

        /// <summary>Bài học có thuộc khóa học không (kể cả bài học không phân chương).</summary>
        public bool LessonBelongsToCourse(string lessonId, string courseId)
        {
            return db.BaiHoc.Any(b => b.maBaiHoc == lessonId
                && ((b.ChuongHoc != null && b.ChuongHoc.maKhoaHoc == courseId)
                    || (b.maChuong == null && b.maBaiHoc.StartsWith(courseId + "_"))));
        }

        public string CreateReport(string courseId, int userId, string loaiViPham, string moTa, string maBaiHoc = null)
        {
            if (!ViolationTypes.Contains(loaiViPham))
            {
                return "Vui lòng chọn loại vi phạm hợp lệ!";
            }

            string cleanMoTa = (moTa ?? "").Trim();
            if (cleanMoTa.Length < 10)
            {
                return "Vui lòng mô tả cụ thể vi phạm (tối thiểu 10 ký tự)!";
            }
            if (cleanMoTa.Length > 2000)
            {
                return "Mô tả tối đa 2000 ký tự!";
            }

            var course = db.KhoaHoc.FirstOrDefault(k => k.maKhoaHoc == courseId);
            if (course == null)
            {
                return "Không tìm thấy khóa học!";
            }
            if (course.maGiaoVien == userId)
            {
                return "Bạn không thể báo cáo khóa học do chính mình tạo!";
            }
            if (!db.DangKyKhoaHoc.Any(d => d.maKhoaHoc == courseId && d.maHocVien == userId))
            {
                return "Chỉ học viên đã đăng ký khóa học mới được gửi báo cáo vi phạm!";
            }
            if (HasPendingReport(courseId, userId))
            {
                return "Bạn đã có một báo cáo đang chờ xử lý cho khóa học này. Vui lòng chờ quản trị viên xem xét!";
            }

            string lessonId = string.IsNullOrWhiteSpace(maBaiHoc) ? null : maBaiHoc.Trim();
            if (lessonId != null && !LessonBelongsToCourse(lessonId, courseId))
            {
                return "Bài học được chọn không thuộc khóa học này!";
            }

            db.Database.ExecuteSqlCommand(
                @"INSERT INTO BaoCaoViPham (maBaoCao, maKhoaHoc, maNguoiBaoCao, loaiViPham, moTa, maBaiHoc, trangThai, ngayTao)
                  VALUES (@maBaoCao, @maKhoaHoc, @maNguoiBaoCao, @loaiViPham, @moTa, @maBaiHoc, @trangThai, GETDATE())",
                new SqlParameter("@maBaoCao", NewId("BC")),
                new SqlParameter("@maKhoaHoc", courseId),
                new SqlParameter("@maNguoiBaoCao", userId),
                new SqlParameter("@loaiViPham", loaiViPham),
                new SqlParameter("@moTa", cleanMoTa),
                new SqlParameter("@maBaiHoc", (object)lessonId ?? DBNull.Value),
                new SqlParameter("@trangThai", ReportPending)
            );
            return null;
        }

        #endregion

        #region Admin xem danh sách

        public int CountPendingReports()
        {
            if (!IsInstalled()) return 0;
            return db.Database.SqlQuery<int>(
                "SELECT COUNT(1) FROM BaoCaoViPham WHERE trangThai = @trangThai",
                new SqlParameter("@trangThai", ReportPending)
            ).FirstOrDefault();
        }

        /// <summary>
        /// Danh sách khóa học có báo cáo vi phạm hoặc đang bị khóa, gom theo khóa học.
        /// filter: pending (còn báo cáo chờ xử lý) | locked (đang tạm khóa) | banned (khóa vĩnh viễn) | all
        /// </summary>
        public List<ViolationCourseSummary> GetCourseSummaries(string filter)
        {
            string sql = @"
                SELECT k.maKhoaHoc, k.tenKhoaHoc, k.trangThai AS trangThaiKhoaHoc, u.hoTen AS tenGiaoVien,
                       ISNULL(SUM(CASE WHEN b.trangThai = @pending THEN 1 ELSE 0 END), 0) AS soChoXuLy,
                       COUNT(b.maBaoCao) AS tongBaoCao,
                       MAX(b.ngayTao) AS baoCaoGanNhat,
                       lk.hanPhanHoi
                FROM KhoaHoc k
                INNER JOIN NguoiDung u ON u.maNguoiDung = k.maGiaoVien
                LEFT JOIN BaoCaoViPham b ON b.maKhoaHoc = k.maKhoaHoc
                OUTER APPLY (
                    SELECT TOP 1 x.hanPhanHoi
                    FROM XuLyViPham x
                    WHERE x.maKhoaHoc = k.maKhoaHoc AND x.hanhDong = @actionLock
                    ORDER BY x.ngayXuLy DESC
                ) lk
                WHERE EXISTS (SELECT 1 FROM BaoCaoViPham b2 WHERE b2.maKhoaHoc = k.maKhoaHoc)
                   OR k.trangThai IN (@statusLocked, @statusBanned)
                GROUP BY k.maKhoaHoc, k.tenKhoaHoc, k.trangThai, u.hoTen, lk.hanPhanHoi";

            var list = db.Database.SqlQuery<ViolationCourseSummary>(sql,
                new SqlParameter("@pending", ReportPending),
                new SqlParameter("@actionLock", ActionLock),
                new SqlParameter("@statusLocked", CourseStatus.TamKhoa),
                new SqlParameter("@statusBanned", CourseStatus.KhoaVinhVien)
            ).ToList();

            switch ((filter ?? "pending").ToLower())
            {
                case "pending":
                    list = list.Where(c => c.soChoXuLy > 0).ToList();
                    break;
                case "locked":
                    list = list.Where(c => c.trangThaiKhoaHoc == CourseStatus.TamKhoa).ToList();
                    break;
                case "banned":
                    list = list.Where(c => c.trangThaiKhoaHoc == CourseStatus.KhoaVinhVien).ToList();
                    break;
            }

            return list
                .OrderByDescending(c => c.soChoXuLy)
                .ThenByDescending(c => c.baoCaoGanNhat)
                .ToList();
        }

        public List<ViolationReport> GetReports(string courseId)
        {
            return db.Database.SqlQuery<ViolationReport>(@"
                SELECT b.maBaoCao, b.maKhoaHoc, b.maNguoiBaoCao, u.hoTen AS tenNguoiBaoCao, u.email AS emailNguoiBaoCao,
                       b.loaiViPham, b.moTa, b.maBaiHoc, bh.tenBaiHoc, b.trangThai, b.ngayTao, b.ngayXuLy, b.ghiChuXuLy
                FROM BaoCaoViPham b
                INNER JOIN NguoiDung u ON u.maNguoiDung = b.maNguoiBaoCao
                LEFT JOIN BaiHoc bh ON bh.maBaiHoc = b.maBaiHoc
                WHERE b.maKhoaHoc = @maKhoaHoc
                ORDER BY CASE WHEN b.trangThai = @pending THEN 0 ELSE 1 END, b.ngayTao DESC",
                new SqlParameter("@maKhoaHoc", courseId),
                new SqlParameter("@pending", ReportPending)
            ).ToList();
        }

        public List<ViolationAction> GetActions(string courseId)
        {
            return db.Database.SqlQuery<ViolationAction>(@"
                SELECT x.maXuLy, x.maKhoaHoc, x.hanhDong, x.lyDo, x.hanPhanHoi, x.trangThaiTruoc,
                       x.maAdmin, u.hoTen AS tenAdmin, x.ngayXuLy
                FROM XuLyViPham x
                INNER JOIN NguoiDung u ON u.maNguoiDung = x.maAdmin
                WHERE x.maKhoaHoc = @maKhoaHoc
                ORDER BY x.ngayXuLy DESC",
                new SqlParameter("@maKhoaHoc", courseId)
            ).ToList();
        }

        /// <summary>Lần Tạm khóa gần nhất của khóa học.</summary>
        public ViolationAction GetLatestLock(string courseId)
        {
            return GetActions(courseId).FirstOrDefault(a => a.hanhDong == ActionLock);
        }

        #endregion

        #region Admin xử lý

        public string Dismiss(string courseId, int adminId, string ghiChu)
        {
            string cleanNote = (ghiChu ?? "").Trim();
            if (cleanNote.Length == 0) return "Vui lòng nhập lý do bác bỏ báo cáo!";
            if (cleanNote.Length > 1000) return "Lý do tối đa 1000 ký tự!";

            using (var tx = db.Database.BeginTransaction())
            {
                int affected = db.Database.ExecuteSqlCommand(
                    @"UPDATE BaoCaoViPham SET trangThai = @dismissed, ngayXuLy = GETDATE(), maAdmin = @maAdmin, ghiChuXuLy = @ghiChu
                      WHERE maKhoaHoc = @maKhoaHoc AND trangThai = @pending",
                    new SqlParameter("@dismissed", ReportDismissed),
                    new SqlParameter("@maAdmin", adminId),
                    new SqlParameter("@ghiChu", cleanNote),
                    new SqlParameter("@maKhoaHoc", courseId),
                    new SqlParameter("@pending", ReportPending));

                if (affected == 0)
                {
                    tx.Rollback();
                    return "Khóa học này không còn báo cáo nào đang chờ xử lý!";
                }

                InsertAction(courseId, ActionDismiss, cleanNote, null, null, adminId);
                tx.Commit();
            }
            return null;
        }

        public string Lock(string courseId, int adminId, string lyDo, int soNgayPhanHoi)
        {
            string cleanReason = (lyDo ?? "").Trim();
            if (cleanReason.Length < 10) return "Vui lòng nhập lý do tạm khóa cụ thể (tối thiểu 10 ký tự)!";
            if (cleanReason.Length > 2000) return "Lý do tối đa 2000 ký tự!";
            if (soNgayPhanHoi < MinDeadlineDays || soNgayPhanHoi > MaxDeadlineDays)
                return $"Thời hạn phản hồi phải từ {MinDeadlineDays} đến {MaxDeadlineDays} ngày!";

            string currentStatus = GetCourseStatus(courseId);
            if (currentStatus == null) return "Không tìm thấy khóa học!";
            if (!CourseStatus.IsPublished(currentStatus))
                return "Chỉ có thể tạm khóa khóa học đang được bán (đã xuất bản). Trạng thái hiện tại: " + currentStatus;

            using (var tx = db.Database.BeginTransaction())
            {
                UpdateCourseStatus(courseId, CourseStatus.TamKhoa);
                InsertAction(courseId, ActionLock, cleanReason, DateTime.Now.AddDays(soNgayPhanHoi), currentStatus, adminId);
                MarkPendingReportsHandled(courseId, adminId, "Đã tạm khóa khóa học: " + Truncate(cleanReason, 900));
                tx.Commit();
            }
            return null;
        }

        public string Reopen(string courseId, int adminId, string ghiChu)
        {
            string cleanNote = (ghiChu ?? "").Trim();
            if (cleanNote.Length == 0) return "Vui lòng nhập ghi chú khi mở lại khóa học!";
            if (cleanNote.Length > 2000) return "Ghi chú tối đa 2000 ký tự!";

            string currentStatus = GetCourseStatus(courseId);
            if (currentStatus == null) return "Không tìm thấy khóa học!";
            if (currentStatus != CourseStatus.TamKhoa) return "Chỉ có thể mở lại khóa học đang ở trạng thái Tạm khóa!";

            // Khôi phục trạng thái trước khi bị tạm khóa
            var lastLock = GetLatestLock(courseId);
            string restoreStatus = lastLock != null && !string.IsNullOrEmpty(lastLock.trangThaiTruoc)
                ? lastLock.trangThaiTruoc
                : CourseStatus.DaXuatBan;

            using (var tx = db.Database.BeginTransaction())
            {
                UpdateCourseStatus(courseId, restoreStatus);
                InsertAction(courseId, ActionReopen, cleanNote, null, currentStatus, adminId);
                tx.Commit();
            }
            return null;
        }

        public string PermanentLock(string courseId, int adminId, string lyDo)
        {
            string cleanReason = (lyDo ?? "").Trim();
            if (cleanReason.Length < 10) return "Vui lòng nhập lý do khóa vĩnh viễn cụ thể (tối thiểu 10 ký tự)!";
            if (cleanReason.Length > 2000) return "Lý do tối đa 2000 ký tự!";

            string currentStatus = GetCourseStatus(courseId);
            if (currentStatus == null) return "Không tìm thấy khóa học!";
            if (currentStatus == CourseStatus.KhoaVinhVien) return "Khóa học đã bị khóa vĩnh viễn!";
            if (currentStatus != CourseStatus.TamKhoa && !CourseStatus.IsPublished(currentStatus))
                return "Chỉ có thể khóa vĩnh viễn khóa học đang bán hoặc đang tạm khóa!";

            using (var tx = db.Database.BeginTransaction())
            {
                UpdateCourseStatus(courseId, CourseStatus.KhoaVinhVien);
                InsertAction(courseId, ActionPermanentLock, cleanReason, null, currentStatus, adminId);
                MarkPendingReportsHandled(courseId, adminId, "Đã khóa vĩnh viễn khóa học: " + Truncate(cleanReason, 900));
                tx.Commit();
            }
            return null;
        }

        #endregion

        #region Helpers

        private string GetCourseStatus(string courseId)
        {
            var course = db.KhoaHoc.AsNoTracking().FirstOrDefault(k => k.maKhoaHoc == courseId);
            if (course == null) return null;
            return course.trangThai ?? CourseStatus.BanNhap;
        }

        private void UpdateCourseStatus(string courseId, string status)
        {
            db.Database.ExecuteSqlCommand(
                "UPDATE KhoaHoc SET trangThai = @trangThai WHERE maKhoaHoc = @maKhoaHoc",
                new SqlParameter("@trangThai", status),
                new SqlParameter("@maKhoaHoc", courseId));
        }

        private void InsertAction(string courseId, string hanhDong, string lyDo, DateTime? hanPhanHoi, string trangThaiTruoc, int adminId)
        {
            db.Database.ExecuteSqlCommand(
                @"INSERT INTO XuLyViPham (maXuLy, maKhoaHoc, hanhDong, lyDo, hanPhanHoi, trangThaiTruoc, maAdmin, ngayXuLy)
                  VALUES (@maXuLy, @maKhoaHoc, @hanhDong, @lyDo, @hanPhanHoi, @trangThaiTruoc, @maAdmin, GETDATE())",
                new SqlParameter("@maXuLy", NewId("XL")),
                new SqlParameter("@maKhoaHoc", courseId),
                new SqlParameter("@hanhDong", hanhDong),
                new SqlParameter("@lyDo", lyDo),
                new SqlParameter("@hanPhanHoi", (object)hanPhanHoi ?? DBNull.Value),
                new SqlParameter("@trangThaiTruoc", (object)trangThaiTruoc ?? DBNull.Value),
                new SqlParameter("@maAdmin", adminId));
        }

        private void MarkPendingReportsHandled(string courseId, int adminId, string ghiChu)
        {
            db.Database.ExecuteSqlCommand(
                @"UPDATE BaoCaoViPham SET trangThai = @handled, ngayXuLy = GETDATE(), maAdmin = @maAdmin, ghiChuXuLy = @ghiChu
                  WHERE maKhoaHoc = @maKhoaHoc AND trangThai = @pending",
                new SqlParameter("@handled", ReportHandled),
                new SqlParameter("@maAdmin", adminId),
                new SqlParameter("@ghiChu", ghiChu),
                new SqlParameter("@maKhoaHoc", courseId),
                new SqlParameter("@pending", ReportPending));
        }

        private static readonly Random random = new Random();

        private static string NewId(string prefix)
        {
            // Ví dụ: BC261002003012345 + 2 số ngẫu nhiên (19 ký tự, cột varchar(30))
            int suffix;
            lock (random) { suffix = random.Next(10, 99); }
            return prefix + DateTime.Now.ToString("yyMMddHHmmssfff") + suffix;
        }

        private static string Truncate(string value, int max)
        {
            return value.Length <= max ? value : value.Substring(0, max);
        }

        #endregion
    }
}
