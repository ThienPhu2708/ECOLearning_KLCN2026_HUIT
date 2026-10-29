using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using ECOLearning.Models;
using ECOLearning.Areas.Instructor.Models;
using Newtonsoft.Json;

namespace ECOLearning.Services {
 public class CourseRevisionPayload {
  public CourseCreateOrEditViewModel Model { get; set; }
  public Dictionary<string,string[]> Uploads { get; set; }
 }
 public class CourseRevisionRow {
  public string CourseId {get;set;} public int TeacherId {get;set;}
  public string Payload {get;set;} public string Status {get;set;}
  public string Version {get;set;} public DateTime UpdatedAt {get;set;}
  public DateTime? SubmittedAt {get;set;} public string Reason {get;set;}
  public CourseRevisionPayload Read() { return JsonConvert.DeserializeObject<CourseRevisionPayload>(Payload); }
 }
 public class CourseCertificateSnapshot {
  public string CourseId {get;set;} public string CourseTitle {get;set;}
  public string Instructor {get;set;} public string StudentName {get;set;}
  public int DurationSeconds {get;set;}
 }
 public class CourseRevisionService {
  private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
  private readonly Func<string,string> map;
  public CourseRevisionService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, Func<string,string> map) {this.db=db;this.map=map;}
  public bool Installed() { return db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.CourseRevision') IS NULL THEN 0 ELSE 1 END").Single()==1; }
  public static bool Published(string status) {return CourseStatus.IsPublished(status) || status=="DaDuyet";}
  public CourseRevisionRow Get(string id) {return !Installed()?null:db.Database.SqlQuery<CourseRevisionRow>("SELECT * FROM dbo.CourseRevision WHERE CourseId=@p0",id).SingleOrDefault();}
  public Dictionary<string,CourseRevisionRow> ForTeacher(int teacher) {
   return !Installed()?new Dictionary<string,CourseRevisionRow>():db.Database.SqlQuery<CourseRevisionRow>("SELECT * FROM dbo.CourseRevision WHERE TeacherId=@p0 AND Status<>'Approved'",teacher).ToDictionary(r=>r.CourseId);
  }
  public List<CourseRevisionRow> Pending() {return !Installed()?new List<CourseRevisionRow>():db.Database.SqlQuery<CourseRevisionRow>("SELECT * FROM dbo.CourseRevision WHERE Status='Pending' ORDER BY SubmittedAt").ToList();}
  public void Save(KhoaHoc course, CourseCreateOrEditViewModel model, Dictionary<string,string[]> uploads, bool submit) {
   new TeacherSpecialtyService(db).Require(course.maGiaoVien,model.MaDanhMuc,course.maDanhMuc);
   if(!Installed()) throw new InvalidOperationException("Cần chạy migration 019 trước khi lưu bản sửa. Khóa đang hoạt động không bị thay đổi.");
   using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
    var old=db.Database.SqlQuery<CourseRevisionRow>("SELECT * FROM dbo.CourseRevision WITH (UPDLOCK,HOLDLOCK) WHERE CourseId=@p0",course.maKhoaHoc).SingleOrDefault();
    if(old!=null && old.Status=="Pending") throw new InvalidOperationException("Bản sửa đang chờ duyệt. Hủy gửi duyệt trước khi sửa tiếp.");
    if((old?.Version ?? "") != (model.RevisionVersion ?? "")) throw new InvalidOperationException("Bản nháp đã thay đổi ở nơi khác. Mở lại trang trước khi lưu.");
    model.CoverImageFile=null; model.Categories=null;
    foreach(var l in model.Lessons) {l.VideoFile=null;l.AttachmentFile=null;}
    var used=new HashSet<string>(model.Lessons.SelectMany(l=>(l.AttachmentTokens??new List<string>()).Concat(new[]{l.VideoToken})).Where(t=>!string.IsNullOrEmpty(t)));
    var payload=new CourseRevisionPayload {Model=model,Uploads=uploads.Where(p=>used.Contains(p.Key)).ToDictionary(p=>p.Key,p=>p.Value)};
    if(submit) Validate(payload);
    string json=JsonConvert.SerializeObject(payload), version=Guid.NewGuid().ToString("N"), status=submit?"Pending":"Draft";
    if(old==null) db.Database.ExecuteSqlCommand("INSERT dbo.CourseRevision(CourseId,TeacherId,Payload,Status,Version,UpdatedAt,SubmittedAt) VALUES(@p0,@p1,@p2,@p3,@p4,GETDATE(),CASE WHEN @p3='Pending' THEN GETDATE() END)",course.maKhoaHoc,course.maGiaoVien,json,status,version);
    else db.Database.ExecuteSqlCommand("UPDATE dbo.CourseRevision SET Payload=@p1,Status=@p2,Version=@p3,UpdatedAt=GETDATE(),SubmittedAt=CASE WHEN @p2='Pending' THEN GETDATE() END,Reason=NULL WHERE CourseId=@p0",course.maKhoaHoc,json,status,version);
    tx.Commit();
   }
  }
  public void Validate(CourseRevisionPayload p) {
   if(p.Model.Lessons.Count==0) throw new InvalidOperationException("Bản sửa cần ít nhất một bài học.");
   var content=new CourseContentService(db,map);
   for(int i=0;i<p.Model.Chapters.Count;i++) if(!p.Model.Lessons.Any(l=>l.ChapterIndex==i)) throw new InvalidOperationException("Mỗi chương phải có bài học.");
   foreach(var l in p.Model.Lessons) {
    string[] video;
    if(string.IsNullOrWhiteSpace(l.Title) || l.ChapterIndex.HasValue && (l.ChapterIndex<0 || l.ChapterIndex>=p.Model.Chapters.Count)) throw new InvalidOperationException("Bài học không hợp lệ.");
    if(string.IsNullOrEmpty(l.VideoToken) || !p.Uploads.TryGetValue(l.VideoToken,out video) || video[0]!="video" || !content.VideoHopLe(video[1])) throw new InvalidOperationException("Video của bài "+l.Title+" không còn hợp lệ.");
    foreach(var token in l.AttachmentTokens??new List<string>()) {string[] file;if(!p.Uploads.TryGetValue(token,out file)||file[0]!="document"||!content.VideoHopLe(file[1])) throw new InvalidOperationException("Tài liệu không còn tồn tại.");}
   }
  }
  public void Review(string id,string version,int admin,bool approve,string reason) {
   using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
    var row=db.Database.SqlQuery<CourseRevisionRow>("SELECT * FROM dbo.CourseRevision WITH(UPDLOCK,HOLDLOCK) WHERE CourseId=@p0",id).SingleOrDefault();
    if(row==null || row.Status!="Pending" || row.Version!=version) throw new InvalidOperationException("Bản sửa đã thay đổi hoặc đã được xử lý. Tải lại trang.");
    var course=db.KhoaHoc.Single(k=>k.maKhoaHoc==id);
    if(approve) {
     if(!Published(course.trangThai) || course.NguoiDung.trangThai==false) throw new InvalidOperationException("Khóa học hoặc giáo viên không còn được phép xuất bản.");
     var p=row.Read();Validate(p);
     new TeacherSpecialtyService(db).Require(course.maGiaoVien,p.Model.MaDanhMuc,course.maDanhMuc);
     if(!db.DanhMuc.Any(c=>c.maDanhMuc==p.Model.MaDanhMuc && c.trangThai!=false)) throw new InvalidOperationException("Danh mục không còn hoạt động.");
     db.Database.ExecuteSqlCommand(@"INSERT dbo.CourseCompletionProtection(CourseId,StudentId,CompletedAt)
SELECT @p0,d.maHocVien,GETDATE() FROM dbo.DangKyKhoaHoc d WHERE d.maKhoaHoc=@p0
AND EXISTS(SELECT 1 FROM dbo.BaiHoc b LEFT JOIN dbo.ChuongHoc c ON c.maChuong=b.maChuong WHERE c.maKhoaHoc=@p0 OR (b.maChuong IS NULL AND LEFT(b.maBaiHoc,LEN(@p0)+1)=@p0+'_'))
AND NOT EXISTS(SELECT 1 FROM dbo.BaiHoc b LEFT JOIN dbo.ChuongHoc c ON c.maChuong=b.maChuong
 WHERE (c.maKhoaHoc=@p0 OR (b.maChuong IS NULL AND LEFT(b.maBaiHoc,LEN(@p0)+1)=@p0+'_'))
 AND NOT EXISTS(SELECT 1 FROM dbo.TienDoBaiHoc t WHERE t.maBaiHoc=b.maBaiHoc AND t.maHocVien=d.maHocVien AND t.daHoanThanh=1))
AND NOT EXISTS(SELECT 1 FROM dbo.CourseCompletionProtection p WHERE p.CourseId=@p0 AND p.StudentId=d.maHocVien)
GROUP BY d.maHocVien",id);
     if(db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.QuizCertificate') IS NULL THEN 0 ELSE 1 END").Single()==1)
      db.Database.ExecuteSqlCommand(@"INSERT dbo.CourseCertificateSnapshot(CourseId,StudentId,CourseTitle,Instructor,StudentName,DurationSeconds)
SELECT q.CourseId,q.StudentId,k.tenKhoaHoc,gv.hoTen,hv.hoTen,
ISNULL((SELECT SUM(ISNULL(b.thoiLuong,0)) FROM dbo.BaiHoc b LEFT JOIN dbo.ChuongHoc c ON c.maChuong=b.maChuong WHERE c.maKhoaHoc=@p0 OR (b.maChuong IS NULL AND LEFT(b.maBaiHoc,LEN(@p0)+1)=@p0+'_')),0)
FROM dbo.QuizCertificate q JOIN dbo.KhoaHoc k ON k.maKhoaHoc=q.CourseId
JOIN dbo.NguoiDung hv ON hv.maNguoiDung=q.StudentId LEFT JOIN dbo.NguoiDung gv ON gv.maNguoiDung=k.maGiaoVien
WHERE q.CourseId=@p0 AND q.IssuedAt IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.CourseCertificateSnapshot s WHERE s.CourseId=q.CourseId AND s.StudentId=q.StudentId)",id);
     ApplyContent(db,course,p.Model,p.Uploads);db.SaveChanges();
    } else if(string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Vui lòng nhập lý do từ chối.");
    db.Database.ExecuteSqlCommand("UPDATE dbo.CourseRevision SET Status=@p1,Version=@p2,ReviewedAt=GETDATE(),AdminId=@p3,Reason=@p4 WHERE CourseId=@p0",id,approve?"Approved":"Rejected",Guid.NewGuid().ToString("N"),admin,(object)reason??DBNull.Value);
    tx.Commit();
   }
  }
  public Dictionary<string,CourseCertificateSnapshot> Certificates(int student) {
   if(db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.CourseCertificateSnapshot') IS NULL THEN 0 ELSE 1 END").Single()==0) return new Dictionary<string,CourseCertificateSnapshot>();
   return db.Database.SqlQuery<CourseCertificateSnapshot>("SELECT * FROM dbo.CourseCertificateSnapshot WHERE StudentId=@p0",student).ToDictionary(c=>c.CourseId);
  }
  public void Cancel(string id,int teacher,string version) {
   if(!Installed()) return;
   int n=db.Database.ExecuteSqlCommand("UPDATE dbo.CourseRevision SET Status='Draft',Version=@p3 WHERE CourseId=@p0 AND TeacherId=@p1 AND Status='Pending' AND Version=@p2",id,teacher,version,Guid.NewGuid().ToString("N"));
   if(n!=1) throw new InvalidOperationException("Bản sửa đã thay đổi. Tải lại trang.");
  }
  public static void ApplyContent(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db,KhoaHoc course,CourseCreateOrEditViewModel model,Dictionary<string,string[]> uploads) {
            var chapters = model.Chapters ?? new List<CourseChapterInput>();
            var lessons = model.Lessons ?? new List<CourseLessonInput>();
            var existingChapters = db.ChuongHoc.Where(c => c.maKhoaHoc == course.maKhoaHoc).ToList();
            var existingLessons = db.BaiHoc.Where(l => (l.ChuongHoc != null && l.ChuongHoc.maKhoaHoc == course.maKhoaHoc) || (l.maChuong == null && l.maBaiHoc.StartsWith(course.maKhoaHoc + "_"))).ToList();
            var deletedLessons = existingLessons.Where(l => !lessons.Any(input => input.Id == l.maBaiHoc)).ToList();
            if (deletedLessons.Any(l => l.TienDoBaiHoc.Any())) throw new InvalidOperationException("Không thể xóa bài đã có người học. Giữ bài này trong bản sửa.");
            var chapterIds = new List<string>();
            for (int i = 0; i < chapters.Count; i++) {
                var input = chapters[i];
                var chapter = existingChapters.FirstOrDefault(c => c.maChuong == input.Id);
                if (chapter == null) { chapter = new ChuongHoc { maChuong = "CH" + Guid.NewGuid().ToString("N").Substring(0,18), maKhoaHoc = course.maKhoaHoc }; db.ChuongHoc.Add(chapter); }
                chapter.tenChuong = input.Title.Trim(); chapter.thuTu = i + 1; chapterIds.Add(chapter.maChuong);
            }
            int nextSequence = 1;
            for (int i = 0; i < lessons.Count; i++) {
                var input = lessons[i]; var lesson = existingLessons.FirstOrDefault(l => l.maBaiHoc == input.Id);
                if (lesson == null) {
                    while (existingLessons.Any(l => l.maBaiHoc == course.maKhoaHoc + "_" + nextSequence)) nextSequence++;
                    lesson = new BaiHoc { maBaiHoc = course.maKhoaHoc + "_" + nextSequence++ }; db.BaiHoc.Add(lesson);
                }
                lesson.maChuong = input.ChapterIndex.HasValue ? chapterIds[input.ChapterIndex.Value] : null;
                lesson.tenBaiHoc = input.Title.Trim(); lesson.thuTu = i + 1; lesson.choXemThu = input.Preview; lesson.thoiLuong = Math.Max(0,input.Duration);
                lesson.duongDanVideo = string.IsNullOrEmpty(input.VideoToken) ? null : uploads[input.VideoToken][1];
                var wantedResources = (input.AttachmentTokens ?? new List<string>()).Select(token => uploads[token]).ToList();
                foreach (var resource in lesson.TaiNguyen.ToList())
                    if (!wantedResources.Any(r => r[1] == resource.duongDan && r[2] == resource.tenTaiNguyen)) db.TaiNguyen.Remove(resource);
                foreach (var resource in wantedResources)
                    if (!lesson.TaiNguyen.Any(r => r.duongDan == resource[1] && r.tenTaiNguyen == resource[2]))
                        db.TaiNguyen.Add(new TaiNguyen { maTaiNguyen = "TN" + Guid.NewGuid().ToString("N").Substring(0,18), maBaiHoc = lesson.maBaiHoc, tenTaiNguyen = resource[2], duongDan = resource[1], loaiTaiNguyen = System.IO.Path.GetExtension(resource[2]) });
            }
            foreach (var lesson in deletedLessons) { db.TaiNguyen.RemoveRange(lesson.TaiNguyen.ToList()); db.BaiHoc.Remove(lesson); }
            foreach (var chapter in existingChapters.Where(c => !chapters.Any(input => input.Id == c.maChuong))) db.ChuongHoc.Remove(chapter);

            course.tenKhoaHoc = model.TenKhoaHoc.Trim();
            course.moTa = model.MoTa;
            course.moTaNgan = model.MoTaNgan;
            course.mucTieuDauRa = model.MucTieuDauRa;
            course.giaBan = model.GiaBan;
            course.capDo = model.CapDo ?? "Cơ bản";
            course.maDanhMuc = model.MaDanhMuc;
            if (!string.IsNullOrWhiteSpace(model.AnhBia))
            {
                course.anhBia = model.AnhBia.Trim();
            }


  }
 }
}
