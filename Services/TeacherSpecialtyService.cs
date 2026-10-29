using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using ECOLearning.Models;
using Newtonsoft.Json;

namespace ECOLearning.Services {
 public class TeacherSpecialtyRequest {
  public int Id {get;set;} public int TeacherId {get;set;}
  public string Categories {get;set;} public string EvidenceUrl {get;set;}
  public string Description {get;set;} public string Status {get;set;}
  public string ApprovedCategories {get;set;}
  public List<string> GrantedCategoryIds {get {return string.IsNullOrEmpty(ApprovedCategories)?new List<string>():JsonConvert.DeserializeObject<List<string>>(ApprovedCategories);}}
  public DateTime CreatedAt {get;set;} public DateTime? ReviewedAt {get;set;}
  public int? ReviewedBy {get;set;} public string Reason {get;set;}
  public List<string> CategoryIds {get {return JsonConvert.DeserializeObject<List<string>>(Categories) ?? new List<string>();}}
 }
 public class TeacherSpecialtyService {
  readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
  public TeacherSpecialtyService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db) {this.db=db;}
  public bool Installed() {return db.Database.SqlQuery<int>("SELECT CASE WHEN OBJECT_ID('dbo.TeacherSpecialtyPolicy') IS NOT NULL AND OBJECT_ID('dbo.TeacherSpecialty') IS NOT NULL AND OBJECT_ID('dbo.TeacherApplicationSpecialty') IS NOT NULL AND OBJECT_ID('dbo.TeacherSpecialtyRequest') IS NOT NULL AND COL_LENGTH('dbo.TeacherSpecialtyRequest','ApprovedCategories') IS NOT NULL THEN 1 ELSE 0 END").Single()==1;}
  void Ready() {if(!Installed()) throw new InvalidOperationException("Chưa cài đặt quản lý chuyên môn. Cần chạy migration 022_TeacherSpecialties.sql.");}
  public List<string> Approved(int teacher) {return !Installed()?new List<string>():db.Database.SqlQuery<string>("SELECT CategoryId FROM dbo.TeacherSpecialty WHERE TeacherId=@p0",teacher).ToList();}
  public bool IsLegacy(int teacher) {return Installed() && db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.TeacherSpecialtyPolicy WHERE TeacherId=@p0 AND LegacyAccess=1",teacher).Single()==1;}
  public List<string> Allowed(int teacher) {return IsLegacy(teacher)?db.DanhMuc.Where(c=>c.trangThai!=false).Select(c=>c.maDanhMuc).ToList():Approved(teacher);}
  public List<string> Requested(string app) {return !Installed()?new List<string>():db.Database.SqlQuery<string>("SELECT CategoryId FROM dbo.TeacherApplicationSpecialty WHERE ApplicationId=@p0",app).ToList();}
  List<string> Validate(IEnumerable<string> categories) {
   Ready();var ids=(categories??Enumerable.Empty<string>()).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct().ToList();
   if(ids.Count==0 || ids.Count>50) throw new InvalidOperationException("Vui lòng chọn ít nhất một lĩnh vực giảng dạy.");
   if(db.DanhMuc.Count(c=>ids.Contains(c.maDanhMuc)&&c.trangThai!=false)!=ids.Count) throw new InvalidOperationException("Lĩnh vực không tồn tại hoặc đã tạm ngưng.");
   return ids;
  }
  void Admin(int id) {if(!db.NguoiDung.Any(u=>u.maNguoiDung==id&&u.vaiTro=="Admin"&&u.trangThai!=false)) throw new InvalidOperationException("Không có quyền duyệt chuyên môn.");}
  void Teacher(int id) {if(!db.NguoiDung.Any(u=>u.maNguoiDung==id&&(u.vaiTro=="GiaoVien"||u.vaiTro=="Giáo viên")&&u.trangThai!=false)) throw new InvalidOperationException("Giáo viên không còn hoạt động.");}
  public void Require(int teacher,string category) {
   Ready();
   if(!Allowed(teacher).Contains(category) || !db.DanhMuc.Any(c=>c.maDanhMuc==category&&c.trangThai!=false))
    throw new InvalidOperationException("Danh mục khóa học chưa thuộc lĩnh vực được duyệt của bạn. Vào Chuyên môn giảng dạy để gửi yêu cầu hoặc liên hệ quản trị viên.");
  }
  public void ValidateSelection(IEnumerable<string> ids) {Validate(ids);}
  public void SaveRequested(string app,int user,IEnumerable<string> categories) {
   var ids=Validate(categories);
   using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
    if(db.Database.SqlQuery<int>("SELECT COUNT(*) FROM dbo.DangKyGiaoVien WITH(UPDLOCK,HOLDLOCK) WHERE maDon=@p0 AND maNguoiDung=@p1 AND trangThai='Pending'",app,user).Single()!=1)
     throw new InvalidOperationException("Chưa lưu được hồ sơ đăng ký vào cơ sở dữ liệu.");
    db.Database.ExecuteSqlCommand("DELETE dbo.TeacherApplicationSpecialty WHERE ApplicationId=@p0",app);
    foreach(var id in ids) db.Database.ExecuteSqlCommand("INSERT dbo.TeacherApplicationSpecialty VALUES(@p0,@p1)",app,id);
    tx.Commit();
   }
  }
  void Grant(int teacher,List<string> ids,int admin) {
   foreach(var id in ids) db.Database.ExecuteSqlCommand("IF NOT EXISTS(SELECT 1 FROM dbo.TeacherSpecialty WITH(UPDLOCK,HOLDLOCK) WHERE TeacherId=@p0 AND CategoryId=@p1) INSERT dbo.TeacherSpecialty(TeacherId,CategoryId,ApprovedBy) VALUES(@p0,@p1,@p2)",teacher,id,admin);
  }
  public void ApproveApplication(string app,IEnumerable<string> categories,int admin) {
   var ids=Validate(categories);Admin(admin);
   using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
    var users=db.Database.SqlQuery<int>("SELECT maNguoiDung FROM dbo.DangKyGiaoVien WITH(UPDLOCK,HOLDLOCK) WHERE maDon=@p0 AND trangThai='Pending'",app).ToList();
    if(users.Count!=1) throw new InvalidOperationException("Hồ sơ không còn chờ duyệt. Tải lại trang.");
    var requested=Requested(app);
    if(!requested.Any()) throw new InvalidOperationException("Hồ sơ cũ chưa chọn lĩnh vực. Vui lòng yêu cầu đăng ký lại bằng danh sách lĩnh vực mới.");
    if(ids.Except(requested).Any()) throw new InvalidOperationException("Chỉ được duyệt lĩnh vực đã đăng ký.");
    int user=users[0];
    if(!db.NguoiDung.Any(u=>u.maNguoiDung==user&&u.trangThai!=false&&(u.vaiTro=="HocVien"||u.vaiTro=="Học viên"))) throw new InvalidOperationException("Tài khoản không còn là học viên hoạt động. Vui lòng kiểm tra lại hồ sơ.");
    Grant(user,ids,admin);
    db.Database.ExecuteSqlCommand("IF EXISTS(SELECT 1 FROM dbo.TeacherSpecialtyPolicy WHERE TeacherId=@p0) UPDATE dbo.TeacherSpecialtyPolicy SET LegacyAccess=0 WHERE TeacherId=@p0 ELSE INSERT dbo.TeacherSpecialtyPolicy(TeacherId,LegacyAccess) VALUES(@p0,0)",user);
    db.Database.ExecuteSqlCommand("UPDATE dbo.NguoiDung SET vaiTro=N'GiaoVien' WHERE maNguoiDung=@p0",user);
    db.Database.ExecuteSqlCommand("UPDATE dbo.DangKyGiaoVien SET trangThai='Approved',ngayDuyet=GETDATE(),maAdmin=@p1,lyDoTuChoi=NULL WHERE maDon=@p0",app,admin);
    tx.Commit();AccountStatusFilter.DanhDauThayDoi(user);
   }
  }
  public List<TeacherSpecialtyRequest> Requests(int? teacher=null) {
   if(!Installed()) return new List<TeacherSpecialtyRequest>();
   return teacher.HasValue?db.Database.SqlQuery<TeacherSpecialtyRequest>("SELECT * FROM dbo.TeacherSpecialtyRequest WHERE TeacherId=@p0 ORDER BY CreatedAt DESC",teacher.Value).ToList():db.Database.SqlQuery<TeacherSpecialtyRequest>("SELECT * FROM dbo.TeacherSpecialtyRequest WHERE Status='Pending' ORDER BY CreatedAt").ToList();
  }
  public void Submit(int teacher,IEnumerable<string> categories,string evidence,string description) {
   var ids=Validate(categories);Teacher(teacher);
   if(string.IsNullOrWhiteSpace(evidence)||string.IsNullOrWhiteSpace(description)||description.Length>2000) throw new InvalidOperationException("Vui lòng bổ sung minh chứng và mô tả (tối đa 2000 ký tự).");
   using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
    if(Requests(teacher).Any(r=>r.Status=="Pending")) throw new InvalidOperationException("Bạn đã có yêu cầu đang chờ duyệt.");
    if(ids.Intersect(Approved(teacher)).Any()) throw new InvalidOperationException("Chỉ chọn lĩnh vực chưa được duyệt.");
    db.Database.ExecuteSqlCommand("INSERT dbo.TeacherSpecialtyRequest(TeacherId,Categories,EvidenceUrl,Description) VALUES(@p0,@p1,@p2,@p3)",teacher,JsonConvert.SerializeObject(ids),evidence,description.Trim());
    tx.Commit();
   }
  }
  public void Review(int id,int teacher,IEnumerable<string> categories,int admin,bool approve,string reason) {
   Ready();Admin(admin);
   using(var tx=db.Database.BeginTransaction(IsolationLevel.Serializable)) {
    var row=db.Database.SqlQuery<TeacherSpecialtyRequest>("SELECT * FROM dbo.TeacherSpecialtyRequest WITH(UPDLOCK,HOLDLOCK) WHERE Id=@p0 AND TeacherId=@p1 AND Status='Pending'",id,teacher).SingleOrDefault();
    if(row==null) throw new InvalidOperationException("Yêu cầu đã được xử lý hoặc không tồn tại.");
    List<string> granted=null;
    if(approve) {Teacher(teacher);var ids=Validate(categories);granted=ids;if(ids.Except(row.CategoryIds).Any()) throw new InvalidOperationException("Lĩnh vực được duyệt phải thuộc yêu cầu.");Grant(teacher,ids,admin);}
    else if(string.IsNullOrWhiteSpace(reason)||reason.Length>2000) throw new InvalidOperationException("Vui lòng nhập lý do từ chối (tối đa 2000 ký tự).");
    db.Database.ExecuteSqlCommand("UPDATE dbo.TeacherSpecialtyRequest SET Status=@p1,Reason=@p2,ReviewedBy=@p3,ReviewedAt=GETDATE(),ApprovedCategories=@p4 WHERE Id=@p0",id,approve?"Approved":"Rejected",(object)reason??DBNull.Value,admin,(object)(granted==null?null:JsonConvert.SerializeObject(granted))??DBNull.Value);
    tx.Commit();
   }
  }
 }
}
