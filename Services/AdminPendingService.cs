using System;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Đếm các yêu cầu từ người dùng đang chờ Admin xử lý, dùng cho chấm đỏ ở menu Admin:
    ///   Kiểm duyệt khóa học  - khóa học giáo viên gửi duyệt
    ///   Duyệt hồ sơ Giáo viên - hồ sơ đăng ký giáo viên "Pending"
    ///   Yêu cầu bổ sung chuyên môn - đề xuất chuyên môn của giáo viên "Pending"
    ///   Báo cáo vi phạm      - báo cáo "Chờ xử lý" + khóa học tạm khóa mà giáo viên đã phản hồi ("Chờ Admin xem xét")
    ///   Chi trả giáo viên    - phiếu rút tiền "Chờ duyệt"
    /// Mỗi mục đếm riêng: một bảng chưa tạo (chưa chạy migration) không làm hỏng các mục khác.
    /// </summary>
    public class AdminPendingService
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;

        public AdminPendingService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
        }

        public AdminPendingCounts Dem()
        {
            return new AdminPendingCounts
            {
                KhoaHoc = An(() => db.KhoaHoc.Count(k => k.trangThai == "Chờ duyệt" || k.trangThai == "Chờ kiểm duyệt" || k.trangThai == "Pending")) + An(() => new CourseRevisionService(db, null).Pending().Count),
                GiaoVien = An(() => TeacherApplicationService.GetAll().Count(a => a.Status == "Pending")),
                ChuyenMon = An(() => new TeacherSpecialtyService(db).Requests().Count),
                ViPham = An(DemViPham),
                ChiTra = An(() => db.Database.SqlQuery<int>(
                    "SELECT COUNT(1) FROM dbo.NhanThanhToan WHERE ISNULL(trangThai, N'Chờ duyệt') = N'Chờ duyệt'").FirstOrDefault())
            };
        }

        private int DemViPham()
        {
            var service = new ViolationReportService(db);
            int soBaoCao = service.CountPendingReports();
            if (!service.HasDiscussion()) return soBaoCao;

            // Lần tạm khóa gần nhất của khóa học đang chờ Admin xem xét phản hồi của giáo viên
            int soPhanHoi = db.Database.SqlQuery<int>(@"
                SELECT COUNT(1)
                FROM KhoaHoc k
                CROSS APPLY (
                    SELECT TOP 1 x.trangThaiXuLy
                    FROM XuLyViPham x
                    WHERE x.maKhoaHoc = k.maKhoaHoc AND x.hanhDong = @lock
                    ORDER BY x.ngayXuLy DESC
                ) lk
                WHERE k.trangThai = @tamKhoa AND lk.trangThaiXuLy = @cho",
                new SqlParameter("@lock", ViolationReportService.ActionLock),
                new SqlParameter("@tamKhoa", CourseStatus.TamKhoa),
                new SqlParameter("@cho", ViolationReportService.StepWaitingAdmin)).FirstOrDefault();
            return soBaoCao + soPhanHoi;
        }

        private static int An(Func<int> dem)
        {
            try
            {
                return dem();
            }
            catch
            {
                return 0;
            }
        }
    }

    public class AdminPendingCounts
    {
        public int KhoaHoc { get; set; }
        public int GiaoVien { get; set; }
        public int ChuyenMon { get; set; }
        public int ViPham { get; set; }
        public int ChiTra { get; set; }
    }
}
