using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    public static class TeacherApplicationService
    {
        private static readonly object _lock = new object();
        private static readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        private static string FilePath
        {
            get
            {
                string dir = HttpContext.Current != null 
                    ? HttpContext.Current.Server.MapPath("~/App_Data") 
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");

                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return Path.Combine(dir, "TeacherApplications.json");
            }
        }

        private const string SelectBaseQuery = @"
            SELECT 
                d.maDon AS ApplicationId,
                d.maNguoiDung AS UserId,
                u.hoTen AS FullName,
                u.email AS Email,
                u.soDienThoai AS Phone,
                d.chuyenMon AS Specialization,
                d.kinhNghiem AS BioExperience,
                d.tenNganHang AS BankName,
                d.soTaiKhoan AS AccountNumber,
                d.tenChuTaiKhoan AS AccountName,
                d.tepChungChi AS CertificateFilePath,
                d.tenTepChungChi AS CertificateFileName,
                d.ngayGui AS CreatedAt,
                d.ngayDuyet AS ReviewedAt,
                d.trangThai AS Status,
                d.lyDoTuChoi AS RejectionReason,
                d.maAdmin AS AdminId
            FROM DangKyGiaoVien d
            INNER JOIN NguoiDung u ON d.maNguoiDung = u.maNguoiDung";

        public static List<TeacherApplication> GetAll()
        {
            try
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    string sql = SelectBaseQuery + " ORDER BY d.ngayGui DESC";
                    var list = db.Database.SqlQuery<TeacherApplication>(sql).ToList();
                    return list;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetAll from SQL Error: " + ex.Message + " -> Falling back to JSON");
                return GetAllFromJson();
            }
        }

        public static TeacherApplication GetByUserId(int userId)
        {
            try
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    string sql = SelectBaseQuery + " WHERE d.maNguoiDung = @userId ORDER BY d.ngayGui DESC";
                    var app = db.Database.SqlQuery<TeacherApplication>(sql, new SqlParameter("@userId", userId)).FirstOrDefault();
                    if (app != null) return app;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetByUserId from SQL Error: " + ex.Message);
            }

            return GetAllFromJson().OrderByDescending(a => a.CreatedAt).FirstOrDefault(a => a.UserId == userId);
        }

        public static TeacherApplication GetById(string appId)
        {
            try
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    string sql = SelectBaseQuery + " WHERE d.maDon = @appId";
                    var app = db.Database.SqlQuery<TeacherApplication>(sql, new SqlParameter("@appId", appId)).FirstOrDefault();
                    if (app != null) return app;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetById from SQL Error: " + ex.Message);
            }

            return GetAllFromJson().FirstOrDefault(a => a.ApplicationId == appId);
        }

        public static void Save(TeacherApplication app)
        {
            // 1. Luu xuong CSDL SQL Server (Bang DangKyGiaoVien)
            try
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    string checkSql = "SELECT COUNT(1) FROM DangKyGiaoVien WHERE maDon = @maDon";
                    int count = db.Database.SqlQuery<int>(checkSql, new SqlParameter("@maDon", app.ApplicationId)).FirstOrDefault();

                    if (count > 0)
                    {
                        string updateSql = @"
                            UPDATE DangKyGiaoVien SET
                                chuyenMon = @chuyenMon,
                                kinhNghiem = @kinhNghiem,
                                tenNganHang = @tenNganHang,
                                soTaiKhoan = @soTaiKhoan,
                                tenChuTaiKhoan = @tenChuTaiKhoan,
                                tepChungChi = @tepChungChi,
                                tenTepChungChi = @tenTepChungChi,
                                trangThai = @trangThai,
                                lyDoTuChoi = @lyDoTuChoi
                            WHERE maDon = @maDon";

                        db.Database.ExecuteSqlCommand(updateSql,
                            new SqlParameter("@chuyenMon", (object)app.Specialization ?? DBNull.Value),
                            new SqlParameter("@kinhNghiem", (object)app.BioExperience ?? DBNull.Value),
                            new SqlParameter("@tenNganHang", (object)app.BankName ?? DBNull.Value),
                            new SqlParameter("@soTaiKhoan", (object)app.AccountNumber ?? DBNull.Value),
                            new SqlParameter("@tenChuTaiKhoan", (object)app.AccountName ?? DBNull.Value),
                            new SqlParameter("@tepChungChi", (object)app.CertificateFilePath ?? DBNull.Value),
                            new SqlParameter("@tenTepChungChi", (object)app.CertificateFileName ?? DBNull.Value),
                            new SqlParameter("@trangThai", (object)app.Status ?? "Pending"),
                            new SqlParameter("@lyDoTuChoi", (object)app.RejectionReason ?? DBNull.Value),
                            new SqlParameter("@maDon", app.ApplicationId)
                        );
                    }
                    else
                    {
                        string insertSql = @"
                            INSERT INTO DangKyGiaoVien (
                                maDon, maNguoiDung, chuyenMon, kinhNghiem, tenNganHang, soTaiKhoan,
                                tenChuTaiKhoan, tepChungChi, tenTepChungChi, ngayGui, ngayDuyet, trangThai, lyDoTuChoi, maAdmin
                            ) VALUES (
                                @maDon, @maNguoiDung, @chuyenMon, @kinhNghiem, @tenNganHang, @soTaiKhoan,
                                @tenChuTaiKhoan, @tepChungChi, @tenTepChungChi, @ngayGui, @ngayDuyet, @trangThai, @lyDoTuChoi, @maAdmin
                            )";

                        db.Database.ExecuteSqlCommand(insertSql,
                            new SqlParameter("@maDon", app.ApplicationId),
                            new SqlParameter("@maNguoiDung", app.UserId),
                            new SqlParameter("@chuyenMon", (object)app.Specialization ?? DBNull.Value),
                            new SqlParameter("@kinhNghiem", (object)app.BioExperience ?? DBNull.Value),
                            new SqlParameter("@tenNganHang", (object)app.BankName ?? DBNull.Value),
                            new SqlParameter("@soTaiKhoan", (object)app.AccountNumber ?? DBNull.Value),
                            new SqlParameter("@tenChuTaiKhoan", (object)app.AccountName ?? DBNull.Value),
                            new SqlParameter("@tepChungChi", (object)app.CertificateFilePath ?? DBNull.Value),
                            new SqlParameter("@tenTepChungChi", (object)app.CertificateFileName ?? DBNull.Value),
                            new SqlParameter("@ngayGui", app.CreatedAt),
                            new SqlParameter("@ngayDuyet", (object)app.ReviewedAt ?? DBNull.Value),
                            new SqlParameter("@trangThai", (object)app.Status ?? "Pending"),
                            new SqlParameter("@lyDoTuChoi", (object)app.RejectionReason ?? DBNull.Value),
                            new SqlParameter("@maAdmin", (object)app.AdminId ?? DBNull.Value)
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Save to SQL Error: " + ex.Message);
            }

            // 2. Luu du phong vao JSON (Dam bao tinh toan ven du lieu)
            SaveToJson(app);
        }

        public static bool UpdateStatus(string appId, string status, string rejectionReason = null, int? adminId = null)
        {
            bool sqlUpdated = false;
            try
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    string updateSql = @"
                        UPDATE DangKyGiaoVien SET
                            trangThai = @trangThai,
                            ngayDuyet = @ngayDuyet,
                            lyDoTuChoi = @lyDoTuChoi,
                            maAdmin = @maAdmin
                        WHERE maDon = @maDon";

                    int affected = db.Database.ExecuteSqlCommand(updateSql,
                        new SqlParameter("@trangThai", status),
                        new SqlParameter("@ngayDuyet", DateTime.Now),
                        new SqlParameter("@lyDoTuChoi", (object)rejectionReason ?? DBNull.Value),
                        new SqlParameter("@maAdmin", (object)adminId ?? DBNull.Value),
                        new SqlParameter("@maDon", appId)
                    );

                    sqlUpdated = (affected > 0);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("UpdateStatus in SQL Error: " + ex.Message);
            }

            // Dong bo luon file JSON du phong
            bool jsonUpdated = UpdateStatusInJson(appId, status, rejectionReason, adminId);

            return sqlUpdated || jsonUpdated;
        }

        #region JSON Helper (Du phong)
        private static List<TeacherApplication> GetAllFromJson()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(FilePath))
                    {
                        return new List<TeacherApplication>();
                    }

                    string json = File.ReadAllText(FilePath);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return new List<TeacherApplication>();
                    }

                    var list = _serializer.Deserialize<List<TeacherApplication>>(json);
                    return list ?? new List<TeacherApplication>();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("GetAll TeacherApplications JSON Error: " + ex.Message);
                    return new List<TeacherApplication>();
                }
            }
        }

        private static void SaveToJson(TeacherApplication app)
        {
            lock (_lock)
            {
                try
                {
                    var list = GetAllFromJson();
                    var existing = list.FirstOrDefault(a => a.ApplicationId == app.ApplicationId);
                    if (existing != null)
                    {
                        list.Remove(existing);
                    }
                    list.Add(app);

                    string json = _serializer.Serialize(list);
                    File.WriteAllText(FilePath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("SaveToJson Error: " + ex.Message);
                }
            }
        }

        private static bool UpdateStatusInJson(string appId, string status, string rejectionReason = null, int? adminId = null)
        {
            lock (_lock)
            {
                try
                {
                    var list = GetAllFromJson();
                    var app = list.FirstOrDefault(a => a.ApplicationId == appId);
                    if (app != null)
                    {
                        app.Status = status;
                        app.ReviewedAt = DateTime.Now;
                        if (!string.IsNullOrEmpty(rejectionReason))
                        {
                            app.RejectionReason = rejectionReason;
                        }
                        if (adminId.HasValue)
                        {
                            app.AdminId = adminId.Value;
                        }

                        string json = _serializer.Serialize(list);
                        File.WriteAllText(FilePath, json);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("UpdateStatusInJson Error: " + ex.Message);
                }
                return false;
            }
        }
        #endregion
    }
}
