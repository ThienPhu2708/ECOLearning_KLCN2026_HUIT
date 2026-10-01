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
    /// Học viên báo cáo -> Admin bác bỏ hoặc Tạm khóa (kèm hạn phản hồi)
    /// -> Giáo viên "Đã chỉnh sửa" hoặc "Khiếu nại" (Chờ Admin xem xét)
    /// -> Admin Mở lại / Yêu cầu sửa thêm (gia hạn, quay lại Chờ GV xử lý) / Khóa vĩnh viễn.
    /// Bảng được tạo bởi Database/Migrations/002, 003 (báo cáo + xử lý) và 004 (trao đổi với giáo viên).
    /// Khi chưa chạy 004, phần học viên báo cáo và Admin xử lý vẫn hoạt động; phần trao đổi được ẩn.
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

        // Bước của một lần Tạm khóa (XuLyViPham.trangThaiXuLy, script 004)
        public const string StepWaitingTeacher = "Chờ GV xử lý";
        public const string StepWaitingAdmin = "Chờ Admin xem xét";
        public const string StepClosed = "Đã kết thúc";

        // Loại tin trao đổi (PhanHoiViPham.loai, script 004)
        public const string MessageFixed = "Đã chỉnh sửa";
        public const string MessageAppeal = "Khiếu nại";
        public const string MessageRequestChanges = "Yêu cầu sửa thêm";

        public const string DiscussionNotInstalledMessage =
            "Chưa khởi tạo dữ liệu trao đổi vi phạm. Vui lòng chạy Database/Migrations/004_XuLyViPham_TraoDoi.sql!";

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

        private bool? hasDiscussion;

        /// <summary>Đã chạy script 004 (trao đổi giữa giáo viên và Admin) hay chưa.</summary>
        public bool HasDiscussion()
        {
            if (!hasDiscussion.HasValue)
            {
                try
                {
                    hasDiscussion = db.Database.SqlQuery<int>(@"
                        SELECT CASE WHEN OBJECT_ID(N'dbo.PhanHoiViPham', N'U') IS NOT NULL
                                     AND COL_LENGTH(N'dbo.XuLyViPham', N'trangThaiXuLy') IS NOT NULL
                               THEN 1 ELSE 0 END").FirstOrDefault() == 1;
                }
                catch
                {
                    hasDiscussion = false;
                }
            }
            return hasDiscussion.Value;
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
        /// filter: pending (còn báo cáo chờ xử lý) | review (GV đã phản hồi, chờ Admin) | locked (đang tạm khóa)
        ///         | banned (khóa vĩnh viễn) | all
        /// </summary>
        public List<ViolationCourseSummary> GetCourseSummaries(string filter)
        {
            string stepColumn = HasDiscussion() ? "x.trangThaiXuLy" : "CAST(NULL AS NVARCHAR(50))";
            string sql = @"
                SELECT k.maKhoaHoc, k.tenKhoaHoc, k.trangThai AS trangThaiKhoaHoc, u.hoTen AS tenGiaoVien,
                       ISNULL(SUM(CASE WHEN b.trangThai = @pending THEN 1 ELSE 0 END), 0) AS soChoXuLy,
                       COUNT(b.maBaoCao) AS tongBaoCao,
                       MAX(b.ngayTao) AS baoCaoGanNhat,
                       lk.hanPhanHoi,
                       lk.trangThaiXuLy
                FROM KhoaHoc k
                INNER JOIN NguoiDung u ON u.maNguoiDung = k.maGiaoVien
                LEFT JOIN BaoCaoViPham b ON b.maKhoaHoc = k.maKhoaHoc
                OUTER APPLY (
                    SELECT TOP 1 x.hanPhanHoi, " + stepColumn + @" AS trangThaiXuLy
                    FROM XuLyViPham x
                    WHERE x.maKhoaHoc = k.maKhoaHoc AND x.hanhDong = @actionLock
                    ORDER BY x.ngayXuLy DESC
                ) lk
                WHERE EXISTS (SELECT 1 FROM BaoCaoViPham b2 WHERE b2.maKhoaHoc = k.maKhoaHoc)
                   OR k.trangThai IN (@statusLocked, @statusBanned)
                GROUP BY k.maKhoaHoc, k.tenKhoaHoc, k.trangThai, u.hoTen, lk.hanPhanHoi, lk.trangThaiXuLy";

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
                case "review":
                    list = list.Where(c => c.trangThaiKhoaHoc == CourseStatus.TamKhoa && c.trangThaiXuLy == StepWaitingAdmin).ToList();
                    break;
                case "locked":
                    list = list.Where(c => c.trangThaiKhoaHoc == CourseStatus.TamKhoa).ToList();
                    break;
                case "banned":
                    list = list.Where(c => c.trangThaiKhoaHoc == CourseStatus.KhoaVinhVien).ToList();
                    break;
            }

            return list
                .OrderByDescending(c => c.trangThaiKhoaHoc == CourseStatus.TamKhoa && c.trangThaiXuLy == StepWaitingAdmin)
                .ThenByDescending(c => c.soChoXuLy)
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
            string stepColumn = HasDiscussion() ? "x.trangThaiXuLy" : "CAST(NULL AS NVARCHAR(50))";
            return db.Database.SqlQuery<ViolationAction>(@"
                SELECT x.maXuLy, x.maKhoaHoc, x.hanhDong, x.lyDo, x.hanPhanHoi, x.trangThaiTruoc,
                       x.maAdmin, u.hoTen AS tenAdmin, x.ngayXuLy, " + stepColumn + @" AS trangThaiXuLy
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

        /// <summary>Lịch sử trao đổi của một lần Tạm khóa, cũ nhất trước (rỗng nếu chưa chạy 004).</summary>
        public List<ViolationMessage> GetMessages(string maXuLy)
        {
            if (string.IsNullOrEmpty(maXuLy) || !HasDiscussion()) return new List<ViolationMessage>();
            return db.Database.SqlQuery<ViolationMessage>(@"
                SELECT p.maPhanHoi, p.maXuLy, p.maNguoiGui, u.hoTen AS tenNguoiGui, p.vaiTroGui, p.loai, p.noiDung,
                       p.hanPhanHoiMoi, p.ngayGui
                FROM PhanHoiViPham p
                INNER JOIN NguoiDung u ON u.maNguoiDung = p.maNguoiGui
                WHERE p.maXuLy = @maXuLy
                ORDER BY p.ngayGui",
                new SqlParameter("@maXuLy", maXuLy)
            ).ToList();
        }

        /// <summary>Số khóa học của giáo viên đang tạm khóa và chờ giáo viên xử lý (0 nếu chưa chạy 002-004).</summary>
        public int CountWaitingForTeacher(int teacherId)
        {
            if (!IsInstalled() || !HasDiscussion()) return 0;
            return db.Database.SqlQuery<int>(@"
                SELECT COUNT(DISTINCT k.maKhoaHoc)
                FROM KhoaHoc k
                INNER JOIN XuLyViPham x ON x.maKhoaHoc = k.maKhoaHoc AND x.hanhDong = @actionLock AND x.trangThaiXuLy = @step
                WHERE k.maGiaoVien = @teacherId AND k.trangThai = @statusLocked",
                new SqlParameter("@actionLock", ActionLock),
                new SqlParameter("@step", StepWaitingTeacher),
                new SqlParameter("@teacherId", teacherId),
                new SqlParameter("@statusLocked", CourseStatus.TamKhoa)
            ).FirstOrDefault();
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
                InsertAction(courseId, ActionLock, cleanReason, DateTime.Now.AddDays(soNgayPhanHoi), currentStatus, adminId, StepWaitingTeacher);
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
                CloseLock(lastLock);
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

            var lastLock = currentStatus == CourseStatus.TamKhoa ? GetLatestLock(courseId) : null;
            using (var tx = db.Database.BeginTransaction())
            {
                UpdateCourseStatus(courseId, CourseStatus.KhoaVinhVien);
                InsertAction(courseId, ActionPermanentLock, cleanReason, null, currentStatus, adminId);
                MarkPendingReportsHandled(courseId, adminId, "Đã khóa vĩnh viễn khóa học: " + Truncate(cleanReason, 900));
                CloseLock(lastLock);
                tx.Commit();
            }
            return null;
        }

        /// <summary>Admin chưa đồng ý với phản hồi của giáo viên: yêu cầu sửa thêm và gia hạn.</summary>
        public string RequestMoreChanges(string courseId, int adminId, string noiDung, int soNgayGiaHan)
        {
            if (!HasDiscussion()) return DiscussionNotInstalledMessage;

            string cleanContent = (noiDung ?? "").Trim();
            if (cleanContent.Length < 10) return "Vui lòng nêu cụ thể nội dung cần sửa thêm (tối thiểu 10 ký tự)!";
            if (cleanContent.Length > 2000) return "Nội dung tối đa 2000 ký tự!";
            if (soNgayGiaHan < MinDeadlineDays || soNgayGiaHan > MaxDeadlineDays)
                return $"Thời hạn phải từ {MinDeadlineDays} đến {MaxDeadlineDays} ngày!";

            if (GetCourseStatus(courseId) != CourseStatus.TamKhoa) return "Khóa học không ở trạng thái Tạm khóa!";
            var lastLock = GetLatestLock(courseId);
            if (lastLock == null) return "Không tìm thấy thông tin tạm khóa của khóa học!";
            if (lastLock.trangThaiXuLy != StepWaitingAdmin) return "Giáo viên chưa gửi phản hồi nên chưa thể yêu cầu sửa thêm!";

            DateTime newDeadline = DateTime.Now.AddDays(soNgayGiaHan);
            using (var tx = db.Database.BeginTransaction())
            {
                InsertMessage(lastLock.maXuLy, adminId, "Admin", MessageRequestChanges, cleanContent, newDeadline);
                db.Database.ExecuteSqlCommand(
                    "UPDATE XuLyViPham SET trangThaiXuLy = @step, hanPhanHoi = @han WHERE maXuLy = @maXuLy",
                    new SqlParameter("@step", StepWaitingTeacher),
                    new SqlParameter("@han", newDeadline),
                    new SqlParameter("@maXuLy", lastLock.maXuLy));
                tx.Commit();
            }
            return null;
        }

        #endregion

        #region Giáo viên phản hồi

        /// <summary>Giáo viên gửi "Đã chỉnh sửa" hoặc "Khiếu nại". Chuyển sang Chờ Admin xem xét.</summary>
        public string SubmitTeacherResponse(string courseId, int teacherId, string loai, string noiDung)
        {
            if (!HasDiscussion()) return DiscussionNotInstalledMessage;
            if (loai != MessageFixed && loai != MessageAppeal) return "Vui lòng chọn cách xử lý: Đã chỉnh sửa hoặc Khiếu nại!";

            string cleanContent = (noiDung ?? "").Trim();
            if (cleanContent.Length < 10) return "Vui lòng mô tả cụ thể (tối thiểu 10 ký tự)!";
            if (cleanContent.Length > 2000) return "Nội dung tối đa 2000 ký tự!";

            var course = db.KhoaHoc.AsNoTracking().FirstOrDefault(k => k.maKhoaHoc == courseId && k.maGiaoVien == teacherId);
            if (course == null) return "Không tìm thấy khóa học của bạn!";
            if (course.trangThai != CourseStatus.TamKhoa) return "Khóa học không ở trạng thái Tạm khóa nên không cần phản hồi!";

            var lastLock = GetLatestLock(courseId);
            if (lastLock == null) return "Không tìm thấy thông tin tạm khóa của khóa học!";
            if (lastLock.trangThaiXuLy == StepWaitingAdmin) return "Bạn đã gửi phản hồi, vui lòng chờ quản trị viên xem xét!";

            using (var tx = db.Database.BeginTransaction())
            {
                InsertMessage(lastLock.maXuLy, teacherId, "GiaoVien", loai, cleanContent, null);
                // phanHoiGiaoVien (cột của 002) giữ phản hồi mới nhất
                db.Database.ExecuteSqlCommand(
                    "UPDATE XuLyViPham SET phanHoiGiaoVien = @phanHoi, ngayPhanHoi = GETDATE(), trangThaiXuLy = @step WHERE maXuLy = @maXuLy",
                    new SqlParameter("@phanHoi", cleanContent),
                    new SqlParameter("@step", StepWaitingAdmin),
                    new SqlParameter("@maXuLy", lastLock.maXuLy));
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

        private void InsertAction(string courseId, string hanhDong, string lyDo, DateTime? hanPhanHoi, string trangThaiTruoc, int adminId, string trangThaiXuLy = null)
        {
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@maXuLy", NewId("XL")),
                new SqlParameter("@maKhoaHoc", courseId),
                new SqlParameter("@hanhDong", hanhDong),
                new SqlParameter("@lyDo", lyDo),
                new SqlParameter("@hanPhanHoi", (object)hanPhanHoi ?? DBNull.Value),
                new SqlParameter("@trangThaiTruoc", (object)trangThaiTruoc ?? DBNull.Value),
                new SqlParameter("@maAdmin", adminId)
            };

            if (HasDiscussion())
            {
                parameters.Add(new SqlParameter("@trangThaiXuLy", (object)trangThaiXuLy ?? DBNull.Value));
                db.Database.ExecuteSqlCommand(
                    @"INSERT INTO XuLyViPham (maXuLy, maKhoaHoc, hanhDong, lyDo, hanPhanHoi, trangThaiTruoc, maAdmin, ngayXuLy, trangThaiXuLy)
                      VALUES (@maXuLy, @maKhoaHoc, @hanhDong, @lyDo, @hanPhanHoi, @trangThaiTruoc, @maAdmin, GETDATE(), @trangThaiXuLy)",
                    parameters.ToArray());
            }
            else
            {
                db.Database.ExecuteSqlCommand(
                    @"INSERT INTO XuLyViPham (maXuLy, maKhoaHoc, hanhDong, lyDo, hanPhanHoi, trangThaiTruoc, maAdmin, ngayXuLy)
                      VALUES (@maXuLy, @maKhoaHoc, @hanhDong, @lyDo, @hanPhanHoi, @trangThaiTruoc, @maAdmin, GETDATE())",
                    parameters.ToArray());
            }
        }

        private void CloseLock(ViolationAction lockAction)
        {
            if (lockAction == null || !HasDiscussion()) return;
            db.Database.ExecuteSqlCommand(
                "UPDATE XuLyViPham SET trangThaiXuLy = @step WHERE maXuLy = @maXuLy",
                new SqlParameter("@step", StepClosed),
                new SqlParameter("@maXuLy", lockAction.maXuLy));
        }

        private void InsertMessage(string maXuLy, int senderId, string senderRole, string loai, string noiDung, DateTime? hanPhanHoiMoi)
        {
            db.Database.ExecuteSqlCommand(
                @"INSERT INTO PhanHoiViPham (maPhanHoi, maXuLy, maNguoiGui, vaiTroGui, loai, noiDung, hanPhanHoiMoi, ngayGui)
                  VALUES (@maPhanHoi, @maXuLy, @maNguoiGui, @vaiTroGui, @loai, @noiDung, @hanPhanHoiMoi, GETDATE())",
                new SqlParameter("@maPhanHoi", NewId("PH")),
                new SqlParameter("@maXuLy", maXuLy),
                new SqlParameter("@maNguoiGui", senderId),
                new SqlParameter("@vaiTroGui", senderRole),
                new SqlParameter("@loai", loai),
                new SqlParameter("@noiDung", noiDung),
                new SqlParameter("@hanPhanHoiMoi", (object)hanPhanHoiMoi ?? DBNull.Value));
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
