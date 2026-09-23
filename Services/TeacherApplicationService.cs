using System;
using System.Collections.Generic;
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

        public static List<TeacherApplication> GetAll()
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
                    System.Diagnostics.Debug.WriteLine("GetAll TeacherApplications Error: " + ex.Message);
                    return new List<TeacherApplication>();
                }
            }
        }

        public static TeacherApplication GetByUserId(int userId)
        {
            return GetAll().OrderByDescending(a => a.CreatedAt).FirstOrDefault(a => a.UserId == userId);
        }

        public static TeacherApplication GetById(string appId)
        {
            return GetAll().FirstOrDefault(a => a.ApplicationId == appId);
        }

        public static void Save(TeacherApplication app)
        {
            lock (_lock)
            {
                var list = GetAll();
                var existing = list.FirstOrDefault(a => a.ApplicationId == app.ApplicationId);
                if (existing != null)
                {
                    list.Remove(existing);
                }
                list.Add(app);

                string json = _serializer.Serialize(list);
                File.WriteAllText(FilePath, json);
            }
        }

        public static bool UpdateStatus(string appId, string status, string rejectionReason = null)
        {
            lock (_lock)
            {
                var list = GetAll();
                var app = list.FirstOrDefault(a => a.ApplicationId == appId);
                if (app == null) return false;

                app.Status = status;
                app.ReviewedAt = DateTime.Now;
                if (!string.IsNullOrEmpty(rejectionReason))
                {
                    app.RejectionReason = rejectionReason;
                }

                string json = _serializer.Serialize(list);
                File.WriteAllText(FilePath, json);
                return true;
            }
        }
    }
}
