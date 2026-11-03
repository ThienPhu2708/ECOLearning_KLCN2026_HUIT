using System;
using System.Linq;
using ECOLearning.Models;
using ECOLearning.Services;

public static class AdminActivityIntegration
{
    private static int checks;
    private static void Check(bool pass, string name)
    { if (!pass) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    public static int Main()
    {
        using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
        using (var transaction = db.Database.BeginTransaction())
        {
            try
            {
                var audit = new AccountAdminService(db);
                Check(audit.IsInstalled(), "Audit schema available");
                string key = "AUD" + Guid.NewGuid().ToString("N").Substring(0, 10);
                var actor = new NguoiDung { hoTen = "Audit Admin fixture", email = key + "a@test.invalid", matKhau = "fixture-only", vaiTro = "Admin", trangThai = true };
                var other = new NguoiDung { hoTen = "Other Admin fixture", email = key + "b@test.invalid", matKhau = "fixture-only", vaiTro = "Admin", trangThai = true };
                var teacher = new NguoiDung { hoTen = "Audit Teacher fixture", email = key + "t@test.invalid", matKhau = "fixture-only", vaiTro = "GiaoVien", trangThai = true };
                db.NguoiDung.Add(actor); db.NguoiDung.Add(other); db.NguoiDung.Add(teacher); db.SaveChanges();
                db.KhoaHoc.Add(new KhoaHoc { maKhoaHoc = key, tenKhoaHoc = "Audit fixture course", maGiaoVien = teacher.maNguoiDung,
                    maDanhMuc = db.DanhMuc.Select(c => c.maDanhMuc).First(), giaBan = 0, trangThai = "Đã xuất bản" });
                db.KiemDuyetKhoaHoc.Add(new KiemDuyetKhoaHoc { maKiemDuyet = "KD" + key, maKhoaHoc = key, maAdmin = actor.maNguoiDung,
                    ngayGui = DateTime.Now.AddMinutes(-2), ngayKiemDuyet = DateTime.Now.AddMinutes(-1), trangThai = "Đã duyệt" });
                db.SaveChanges();
                var list = audit.LayHoatDongAdmin(actor.maNguoiDung);
                Check(list.Count == 1 && list[0].HanhDong == AccountAdminService.HdDuyetKhoaHoc, "Existing approval visible without rewriting database history");
                Check(list[0].TenDoiTuong == "Audit fixture course" && list[0].TenAdmin == actor.hoTen, "Activity includes actor and course title");
                Check(audit.LayHoatDongAdmin(other.maNguoiDung).Count == 0, "Another administrator does not inherit activity");
                audit.GhiNhatKy(actor.maNguoiDung, AccountAdminService.HdDuyetKhoaHoc, "KhoaHoc", key, "Fixture approval");
                list = audit.LayHoatDongAdmin(actor.maNguoiDung);
                Check(list.Count == 1 && list[0].MaNhatKy > 0, "New audit replaces legacy projection without duplicates");
                audit.GhiNhatKy(actor.maNguoiDung, AccountAdminService.HdTuChoiKhoaHoc, "KhoaHoc", key, "Fixture rejection");
                Check(audit.LayHoatDongAdmin(actor.maNguoiDung).Count == 2, "Separate decisions remain separate history");
                audit.GhiNhatKy(actor.maNguoiDung, AccountAdminService.HdKhoa, AccountAdminService.DoiTuongNguoiDung, other.maNguoiDung.ToString(), "Fixture lock");
                list = audit.LayHoatDongAdmin(actor.maNguoiDung);
                Check(list.Count == 3 && list.Any(n => n.TenDoiTuong == other.hoTen), "Performed account actions appear on actor profile");
                Check(audit.LayNhatKy(AccountAdminService.DoiTuongNguoiDung, other.maNguoiDung.ToString()).Count == 1, "Target-account audit remains intact");
                Check(audit.LayHoatDongAdmin(actor.maNguoiDung, 1).Count == 1, "Requested history limit respected");
                db.Database.ExecuteSqlCommand(@"INSERT dbo.DangKyGiaoVien(maDon,maNguoiDung,chuyenMon,kinhNghiem,tenNganHang,soTaiKhoan,tenChuTaiKhoan,ngayGui,trangThai,ngayDuyet,maAdmin,lyDoTuChoi)
                    VALUES(@p0,@p1,N'Fixture',N'Fixture',N'ACB','123456','FIXTURE',GETDATE(),'Rejected',GETDATE(),@p2,N'Fixture rejection')", key, teacher.maNguoiDung, actor.maNguoiDung);
                list = audit.LayHoatDongAdmin(actor.maNguoiDung);
                Check(list.Any(n => n.HanhDong == "TuChoiGiaoVien" && n.MaDoiTuong == key && n.TenDoiTuong == teacher.hoTen), "Previously rejected teacher application visible with reviewer identity");
                audit.GhiNhatKy(actor.maNguoiDung,"TuChoiGiaoVien","HoSoGiaoVien",key,"Fixture rejection");
                list = audit.LayHoatDongAdmin(actor.maNguoiDung);
                Check(list.Count(n => n.HanhDong == "TuChoiGiaoVien" && n.MaDoiTuong == key) == 1, "Teacher decision not duplicated when central audit exists");
                Check(list.Single(n => n.HanhDong == "TuChoiGiaoVien").TenDoiTuong == teacher.hoTen, "Stored application enriches central audit with applicant name");
                db.Database.ExecuteSqlCommand(@"INSERT dbo.TeacherSpecialtyRequest(TeacherId,Categories,EvidenceUrl,Description,Status,ReviewedAt,ReviewedBy,Reason)
                    VALUES(@p0,'[]','fixture','fixture','Rejected',GETDATE(),@p1,N'Fixture reason')",teacher.maNguoiDung,actor.maNguoiDung);
                Check(audit.LayHoatDongAdmin(actor.maNguoiDung).Any(n => n.HanhDong == "TuChoiChuyenMon"), "Specialty decision included");
                db.Database.ExecuteSqlCommand(@"INSERT dbo.NhanThanhToan(maPhieuChi,tiLeChietKhau,soTienYeuCau,thucNhan,ngayYeuCau,nganHang,trangThai,maGiaoVien,soTaiKhoan,tenChuTaiKhoan,ngayXuLy,maAdminXuLy,lyDoTuChoi)
                    VALUES(@p0,0,100000,100000,GETDATE(),'ACB',N'Từ chối',@p1,'123456','FIXTURE',GETDATE(),@p2,N'Fixture reason')",key,teacher.maNguoiDung,actor.maNguoiDung);
                Check(audit.LayHoatDongAdmin(actor.maNguoiDung).Any(n => n.HanhDong == "TuChoiChiTra" && n.MaDoiTuong == key), "Payout decision included");
                db.Database.ExecuteSqlCommand(@"INSERT dbo.XuLyViPham(maXuLy,maKhoaHoc,hanhDong,lyDo,maAdmin)
                    VALUES(@p0,@p0,N'Tạm khóa',N'Fixture reason',@p1)",key,actor.maNguoiDung);
                Check(audit.LayHoatDongAdmin(actor.maNguoiDung).Any(n => n.HanhDong == "TamKhoaKhoaHoc"), "Violation handling included");
                db.Database.ExecuteSqlCommand(@"INSERT dbo.PhanHoiViPham(maPhanHoi,maXuLy,maNguoiGui,vaiTroGui,loai,noiDung)
                    VALUES(@p0,@p0,@p1,'Admin',N'Yêu cầu sửa thêm',N'Fixture follow-up')",key,actor.maNguoiDung);
                Check(audit.LayHoatDongAdmin(actor.maNguoiDung).Any(n => n.HanhDong == "YeuCauSuaViPham"), "Admin follow-up to teacher included");
                Check(audit.LayHoatDongAdmin(other.maNguoiDung).Count == 0, "Full history remains restricted to correct actor");
                Console.WriteLine(checks + " checks passed; fixture transaction rolled back.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally { transaction.Rollback(); }
        }
    }
}
