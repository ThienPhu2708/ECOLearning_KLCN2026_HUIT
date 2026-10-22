using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
namespace ECOLearning.Areas.Instructor.Controllers
{
    public class LibraryFile
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public long Size { get; set; }
        public int Duration { get; set; }
        public List<string> Courses { get; set; } = new List<string>();
        /// <summary>Link phát cho thẻ video (video trên R2 đi qua VideoStream/Play). Không lưu vào file thư viện.</summary>
        [JsonIgnore]
        public string PlayUrl { get { return ECOLearning.Services.VideoStorageService.LinkPhat(Url); } }
    }
    public class ResourceController : InstructorBaseController
    {
        private static readonly object LibraryLock = new object();
        public static void Register(HttpServerUtilityBase server, int teacherId, string kind, string name, string url, long size)
        {
            lock (LibraryLock) {
                string path = server.MapPath("~/App_Data/ResourceLibrary_" + teacherId + ".json");
                var files = System.IO.File.Exists(path) ? JsonConvert.DeserializeObject<List<LibraryFile>>(System.IO.File.ReadAllText(path)) : new List<LibraryFile>();
                files = files ?? new List<LibraryFile>();
                if (!files.Any(f => f.Url == url)) files.Add(new LibraryFile { Id = Guid.NewGuid().ToString("N"), Kind = kind, Name = name, Url = url, Size = size });
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(files));
            }
        }
        /// <summary>Video cũ được chuyển lên R2: đổi đường dẫn trong thư viện của mọi giáo viên.</summary>
        public static void DoiDuongDanTep(string cu, string moi)
        {
            string thuMuc = System.Web.Hosting.HostingEnvironment.MapPath("~/App_Data/");
            if (thuMuc == null || !Directory.Exists(thuMuc)) return;
            lock (LibraryLock)
            {
                foreach (string path in Directory.GetFiles(thuMuc, "ResourceLibrary_*.json"))
                {
                    var files = JsonConvert.DeserializeObject<List<LibraryFile>>(System.IO.File.ReadAllText(path));
                    if (files == null || !files.Any(f => f.Url == cu)) continue;
                    if (files.Any(f => f.Url == moi)) files.RemoveAll(f => f.Url == cu);
                    else foreach (var f in files.Where(f => f.Url == cu)) f.Url = moi;
                    System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(files));
                }
            }
        }
        private List<LibraryFile> FilesForTeacher()
        {
            int teacher = CurrentTeacherId;
            string path = Server.MapPath("~/App_Data/ResourceLibrary_" + teacher + ".json");
            List<LibraryFile> files;
            lock (LibraryLock) { files = System.IO.File.Exists(path) ? JsonConvert.DeserializeObject<List<LibraryFile>>(System.IO.File.ReadAllText(path)) : new List<LibraryFile>(); }
            files = files ?? new List<LibraryFile>();
            var courses = db.KhoaHoc.Where(c => c.maGiaoVien == teacher).ToList();
            foreach (var course in courses) {
                var lessons = db.BaiHoc.Where(l => (l.ChuongHoc != null && l.ChuongHoc.maKhoaHoc == course.maKhoaHoc) || (l.maChuong == null && l.maBaiHoc.StartsWith(course.maKhoaHoc + "_"))).ToList();
                foreach (var lesson in lessons) {
                    if (!string.IsNullOrWhiteSpace(lesson.duongDanVideo)) AddFile(files, "video", Path.GetFileName(lesson.duongDanVideo), lesson.duongDanVideo, lesson.thoiLuong ?? 0, course.tenKhoaHoc);
                    foreach (var resource in lesson.TaiNguyen) AddFile(files, "document", resource.tenTaiNguyen, resource.duongDan, 0, course.tenKhoaHoc);
                }
            }
            foreach (var file in files) {
                if (file.Url.StartsWith("/") && !file.Url.StartsWith("//")) {
                    string physical = Server.MapPath("~" + file.Url);
                    if (System.IO.File.Exists(physical)) file.Size = new FileInfo(physical).Length;
                }
                else if (ECOLearning.Services.VideoStorageService.LaR2(file.Url)) {
                    long? size = ECOLearning.Services.VideoStorageService.KichThuoc(file.Url);
                    if (size.HasValue) file.Size = size.Value;
                }
            }
            return files.OrderBy(f => f.Name).ToList();
        }
        private void AddFile(List<LibraryFile> files, string kind, string name, string url, int duration, string course)
        {
            var file = files.FirstOrDefault(f => f.Url == url);
            if (file == null) { file = new LibraryFile { Id = Guid.NewGuid().ToString("N"), Kind = kind, Name = name, Url = url }; files.Add(file); }
            file.Duration = Math.Max(file.Duration, duration);
            if (!file.Courses.Contains(course)) file.Courses.Add(course);
        }
        public ActionResult Index()
        {
            ViewBag.BreadcrumbCurrent = "Thư viện tài nguyên";
            return View(FilesForTeacher());
        }
        [HttpGet]
        public JsonResult List(string kind = "")
        {
            return Json(new { success = true, files = FilesForTeacher().Where(f => string.IsNullOrEmpty(kind) || f.Kind == kind) }, JsonRequestBehavior.AllowGet);
        }
        [HttpGet]
        public ActionResult Preview(string url)
        {
            var file = FilesForTeacher().FirstOrDefault(f => f.Url == url);
            if (file == null || !file.Url.StartsWith("/") || file.Url.StartsWith("//")) return HttpNotFound();
            string root = Path.GetFullPath(Server.MapPath("~/"));
            string physical = Path.GetFullPath(Server.MapPath("~" + file.Url));
            if (!physical.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !System.IO.File.Exists(physical)) return HttpNotFound();
            string extension = Path.GetExtension(physical).ToLowerInvariant();
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (extension == ".pdf") return File(physical, "application/pdf");
            if (extension == ".txt" || extension == ".csv") return File(physical, "text/plain; charset=utf-8");
            if (extension == ".docx")
            {
                try
                {
                    using (var archive = System.IO.Compression.ZipFile.OpenRead(physical))
                    {
                        var entry = archive.GetEntry("word/document.xml");
                        if (entry == null || entry.Length > 10000000) return new HttpStatusCodeResult(400);
                        using (var stream = entry.Open())
                        using (var reader = System.Xml.XmlReader.Create(stream, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null }))
                        {
                            var document = System.Xml.Linq.XDocument.Load(reader);
                            System.Xml.Linq.XNamespace ns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                            string text = string.Join(Environment.NewLine, document.Descendants(ns + "p")
                                .Select(p => string.Concat(p.Descendants(ns + "t").Select(t => t.Value))));
                            string paragraphs = string.Join("", text.Split(new[] { Environment.NewLine }, StringSplitOptions.None)
                                .Select(line => "<p>" + System.Web.HttpUtility.HtmlEncode(line) + "</p>"));
                            string html = "<!DOCTYPE html><html lang='vi'><head><meta charset='utf-8'><meta name='color-scheme' content='light'><style>html{background:#f1f3f7;color-scheme:light}body{box-sizing:border-box;max-width:780px;min-height:90vh;margin:24px auto;padding:40px 48px;background:#fff;color:#202938;font:16px/1.75 Arial,sans-serif;box-shadow:0 2px 12px rgba(15,23,42,.08)}p{margin:0 0 12px;min-height:12px;white-space:pre-wrap;overflow-wrap:anywhere}@media(max-width:600px){body{margin:12px;padding:24px}}</style></head><body>" + paragraphs + "</body></html>";
                            return Content(html, "text/html", System.Text.Encoding.UTF8);
                        }
                    }
                }
                catch (System.IO.InvalidDataException) { return new HttpStatusCodeResult(400, "Không thể xem trước tệp này."); }
                catch (System.Xml.XmlException) { return new HttpStatusCodeResult(400, "Không thể xem trước tệp này."); }
            }
            return new HttpStatusCodeResult(415);
        }
        [HttpGet]
        public ActionResult Download(string url)
        {
            var file = FilesForTeacher().FirstOrDefault(f => f.Url == url);
            if (file == null) return HttpNotFound();
            if (!file.Url.StartsWith("/") || file.Url.StartsWith("//")) return new HttpStatusCodeResult(400, "Tệp không nằm trong kho lưu trữ.");
            string root = Path.GetFullPath(Server.MapPath("~/"));
            string physical = Path.GetFullPath(Server.MapPath("~" + file.Url));
            if (!physical.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || !System.IO.File.Exists(physical)) return HttpNotFound("Không tìm thấy tệp.");
            string name = Path.GetFileName(file.Name);
            if (string.IsNullOrWhiteSpace(Path.GetExtension(name))) name += Path.GetExtension(physical);
            return File(physical, "application/octet-stream", name);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Rename(string url, string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || name.Length > 200 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return Json(new { success = false, message = "Tên tệp phải từ 1–200 ký tự và không chứa ký tự không hợp lệ." });
            lock (LibraryLock)
            {
                var file = FilesForTeacher().FirstOrDefault(f => f.Url == url);
                if (file == null) return Json(new { success = false, message = "Không tìm thấy tệp trong thư viện của bạn." });
                var courses = db.KhoaHoc.Where(c => c.maGiaoVien == CurrentTeacherId).ToList();
                var ids = courses.Select(c => c.maKhoaHoc).ToList();
                var lessons = db.BaiHoc.Where(l => (l.ChuongHoc != null && ids.Contains(l.ChuongHoc.maKhoaHoc)) || l.maChuong == null).ToList()
                    .Where(l => l.maChuong != null || ids.Any(id => l.maBaiHoc.StartsWith(id + "_"))).ToList();
                var usingLessons = lessons.Where(l => l.duongDanVideo == url || l.TaiNguyen.Any(t => t.duongDan == url)).ToList();
                if (courses.Any(c => (c.trangThai == "Chờ duyệt" || c.trangThai == "ChoDuyet") && usingLessons.Any(l =>
                    l.ChuongHoc != null ? l.ChuongHoc.maKhoaHoc == c.maKhoaHoc : l.maBaiHoc.StartsWith(c.maKhoaHoc + "_"))))
                    return Json(new { success = false, message = "Tệp đang được khóa học chờ duyệt sử dụng nên chưa thể đổi tên." });
                string extension = Path.GetExtension(file.Url);
                if (!string.IsNullOrEmpty(extension) && !name.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) name += extension;
                if (name.Length > 200) return Json(new { success = false, message = "Tên tệp quá dài." });
                foreach (var lesson in usingLessons)
                    foreach (var resource in lesson.TaiNguyen.Where(t => t.duongDan == url)) resource.tenTaiNguyen = name;
                db.SaveChanges();
                string path = Server.MapPath("~/App_Data/ResourceLibrary_" + CurrentTeacherId + ".json");
                var files = System.IO.File.Exists(path) ? JsonConvert.DeserializeObject<List<LibraryFile>>(System.IO.File.ReadAllText(path)) : new List<LibraryFile>();
                files = files ?? new List<LibraryFile>();
                var saved = files.FirstOrDefault(f => f.Url == url);
                if (saved == null) { saved = file; files.Add(saved); }
                saved.Name = name;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(files));
                var uploads = Session["CourseWizardUploads"] as Dictionary<string, string[]>;
                if (uploads != null) foreach (var value in uploads.Values.Where(v => v[1] == url)) value[2] = name;
                return Json(new { success = true });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Delete(string url)
        {
            lock (LibraryLock)
            {
                var file = FilesForTeacher().FirstOrDefault(f => f.Url == url);
                if (file == null) return Json(new { success = false, message = "Không tìm thấy tệp." });
                if (file.Courses.Any()) return Json(new { success = false, message = "Tệp đang được khóa học sử dụng. Hãy gỡ tệp khỏi bài học trước khi xóa khỏi thư viện." });
                string path = Server.MapPath("~/App_Data/ResourceLibrary_" + CurrentTeacherId + ".json");
                var files = System.IO.File.Exists(path) ? JsonConvert.DeserializeObject<List<LibraryFile>>(System.IO.File.ReadAllText(path)) : new List<LibraryFile>();
                files = files ?? new List<LibraryFile>();
                files.RemoveAll(f => f.Url == url);
                System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(files));
                var uploads = Session["CourseWizardUploads"] as Dictionary<string, string[]>;
                if (uploads != null) foreach (var token in uploads.Where(p => p.Value[1] == url).Select(p => p.Key).ToList()) uploads.Remove(token);
                return Json(new { success = true });
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Select(string url)
        {
            var file = FilesForTeacher().FirstOrDefault(f => f.Url == url);
            if (file == null) return Json(new { success = false, message = "Không tìm thấy tệp trong thư viện của bạn." });
            var uploads = Session["CourseWizardUploads"] as Dictionary<string, string[]> ?? new Dictionary<string, string[]>();
            string token = Guid.NewGuid().ToString("N");
            uploads[token] = new[] { file.Kind, file.Url, file.Name }; Session["CourseWizardUploads"] = uploads;
            return Json(new { success = true, token, url = file.Url, name = file.Name, duration = file.Duration });
        }
    }
}
