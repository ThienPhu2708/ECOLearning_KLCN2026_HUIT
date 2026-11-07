using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    public class TeacherPublishingRestriction
    {
        public int Id { get; set; }
        public int TeacherId { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime? EndsAtUtc { get; set; }
        public string Reason { get; set; }
        public string EndText => EndsAtUtc.HasValue ? EndsAtUtc.Value.AddHours(7).ToString("HH:mm dd/MM/yyyy") + " (giờ Việt Nam)" : "đến khi quản trị viên gỡ hạn chế";
        public string Message => "Bạn đang bị hạn chế đăng khóa học mới " + (EndsAtUtc.HasValue ? "đến " : "") + EndText + ". Lý do: " + Reason;
    }

    public class TeacherPublishingRestrictionService
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
        private readonly Func<string,string,string,string,string,string,string,bool> sendNotice;
        public TeacherPublishingRestrictionService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
            : this(db, (email, name, subject, body, title, highlight, tone) => EmailService.SendAdminNoticeEmail(email,name,subject,body,title,highlight,tone).Success) { }
        public TeacherPublishingRestrictionService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, Func<string,string,string,string,string,string,string,bool> sendNotice)
        { this.db = db; this.sendNotice = sendNotice ?? throw new ArgumentNullException(nameof(sendNotice)); }
        public bool Installed() => db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID(N'dbo.TeacherPublishingRestriction',N'U') IS NULL THEN 0 ELSE 1 END").Single() == 1;
        public TeacherPublishingRestriction Active(int teacherId)
        {
            if (!Installed()) return null;
            return db.Database.SqlQuery<TeacherPublishingRestriction>(@"SELECT TOP 1 Id,TeacherId,StartedAtUtc,EndsAtUtc,Reason
FROM dbo.TeacherPublishingRestriction WHERE TeacherId=@p0 AND LiftedAtUtc IS NULL
AND (EndsAtUtc IS NULL OR EndsAtUtc>SYSUTCDATETIME()) ORDER BY Id DESC", teacherId).FirstOrDefault();
        }
        public void LockTeacher(int teacherId)
        {
            db.Database.ExecuteSqlCommand(@"DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@p0,@LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
IF @result<0 THROW 51000,N'Không thể khóa dữ liệu hạn chế. Vui lòng thử lại.',1;", "TeacherPublishingRestriction:" + teacherId);
        }
        public KetQuaQuanTri Apply(int adminId, int teacherId, string duration, string reason)
        {
            if (!Installed()) return KetQuaQuanTri.Loi("Chưa khởi tạo chức năng. Cần chạy migration 034.");
            reason = (reason ?? "").Trim();
            if (reason.Length < 5 || reason.Length > 500) return KetQuaQuanTri.Loi("Lý do cần từ 5 đến 500 ký tự.");
            var user = db.NguoiDung.Find(teacherId);
            if (user == null || !(user.vaiTro == "GiaoVien" || user.vaiTro == "Giáo viên" || user.vaiTro == "Instructor")) return KetQuaQuanTri.Loi("Không tìm thấy tài khoản giáo viên.");
            DateTime now = DateTime.UtcNow;
            DateTime? end;
            switch (duration)
            {
                case "2d": end = now.AddDays(2); break; case "5d": end = now.AddDays(5); break;
                case "7d": end = now.AddDays(7); break; case "20d": end = now.AddDays(20); break;
                case "1m": end = now.AddHours(7).AddMonths(1).AddHours(-7); break; case "3m": end = now.AddHours(7).AddMonths(3).AddHours(-7); break;
                case "6m": end = now.AddHours(7).AddMonths(6).AddHours(-7); break; case "indefinite": end = null; break;
                default: return KetQuaQuanTri.Loi("Thời hạn hạn chế không hợp lệ.");
            }
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                LockTeacher(teacherId);
                if (Active(teacherId) != null) return KetQuaQuanTri.Loi("Giáo viên đang bị hạn chế. Hãy gỡ hạn chế trước khi áp dụng thời hạn mới.");
                db.Database.ExecuteSqlCommand(@"UPDATE dbo.TeacherPublishingRestriction SET LiftedAtUtc=SYSUTCDATETIME(),LiftReason=N'Hết thời hạn'
WHERE TeacherId=@p0 AND LiftedAtUtc IS NULL AND EndsAtUtc<=SYSUTCDATETIME();", teacherId);
                db.Database.ExecuteSqlCommand(@"INSERT dbo.TeacherPublishingRestriction(TeacherId,CreatedBy,StartedAtUtc,EndsAtUtc,Reason)
VALUES(@teacher,@admin,@now,@end,@reason)", new SqlParameter("@teacher",teacherId),new SqlParameter("@admin",adminId),
                    new SqlParameter("@now",now),new SqlParameter("@end",(object)end ?? DBNull.Value),new SqlParameter("@reason",reason));
                new AccountAdminService(db).GhiNhatKy(adminId,AccountAdminService.HdHanCheDangKhoaHoc,AccountAdminService.DoiTuongNguoiDung,teacherId.ToString(),duration+": "+reason);
                tx.Commit();
            }
            string endText = end.HasValue ? end.Value.AddHours(7).ToString("HH:mm dd/MM/yyyy") + " (giờ Việt Nam)" : "đến khi quản trị viên gỡ hạn chế";
            bool mail = sendNotice(user.email,user.hoTen,"[E-Learning] Thông báo hạn chế đăng khóa học mới",
                "<p>Bạn tạm thời không thể tạo hoặc gửi duyệt khóa học mới " + (end.HasValue ? "đến " : "") + WebUtility.HtmlEncode(endText) + ". Các khóa đã xuất bản và việc học của học viên vẫn hoạt động bình thường.</p><p>Nếu cần hỗ trợ, vui lòng liên hệ ban quản trị.</p>",
                "Lý do",WebUtility.HtmlEncode(reason),"warning");
            return KetQuaQuanTri.Ok("Đã hạn chế đăng khóa học mới." + (mail ? "" : " Chưa gửi được email thông báo; giáo viên vẫn thấy thông báo trên hệ thống."));
        }
        public KetQuaQuanTri Lift(int adminId, int teacherId)
        {
            if (!Installed()) return KetQuaQuanTri.Loi("Chưa khởi tạo chức năng. Cần chạy migration 034.");
            var user = db.NguoiDung.Find(teacherId);
            if (user == null) return KetQuaQuanTri.Loi("Không tìm thấy giáo viên.");
            using (var tx = db.Database.BeginTransaction(IsolationLevel.Serializable))
            {
                LockTeacher(teacherId);
                int changed = db.Database.ExecuteSqlCommand(@"UPDATE dbo.TeacherPublishingRestriction SET LiftedAtUtc=SYSUTCDATETIME(),LiftedBy=@p0,LiftReason=N'Quản trị viên gỡ hạn chế'
WHERE TeacherId=@p1 AND LiftedAtUtc IS NULL AND (EndsAtUtc IS NULL OR EndsAtUtc>SYSUTCDATETIME())",adminId,teacherId);
                if (changed == 0) return KetQuaQuanTri.Loi("Giáo viên hiện không bị hạn chế.");
                new AccountAdminService(db).GhiNhatKy(adminId,AccountAdminService.HdGoHanCheDangKhoaHoc,AccountAdminService.DoiTuongNguoiDung,teacherId.ToString(),"Quản trị viên gỡ hạn chế đăng khóa học mới.");
                tx.Commit();
            }
            bool mail = sendNotice(user.email,user.hoTen,"[E-Learning] Đã gỡ hạn chế đăng khóa học mới",
                "<p>Ban quản trị đã gỡ hạn chế. Bạn có thể tạo và gửi duyệt khóa học mới trở lại.</p>",null,null,"success");
            return KetQuaQuanTri.Ok("Đã gỡ hạn chế." + (mail ? "" : " Chưa gửi được email thông báo."));
        }
    }
}
