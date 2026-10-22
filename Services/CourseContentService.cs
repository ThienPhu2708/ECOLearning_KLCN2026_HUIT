using System;
using System.Collections.Generic;
using System.Linq;
using ECOLearning.Models;

namespace ECOLearning.Services
{
    /// <summary>
    /// Kiểm tra nội dung giảng dạy trước khi gửi duyệt / xuất bản (kiểm tra ở server).
    /// Quy tắc nhóm đã chốt: khóa học phải có chương học và bài học; MỖI bài học bắt buộc có video bài giảng
    /// (tài liệu là tùy chọn). Video lưu trên máy chủ thì file phải còn tồn tại; video là liên kết ngoài (http/https)
    /// thì đường dẫn phải là URL hợp lệ.
    /// </summary>
    public class CourseContentService
    {
        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
        private readonly Func<string, string> mapPath;

        /// <param name="mapPath">Chuyển đường dẫn ứng dụng sang đường dẫn vật lý (Server.MapPath).</param>
        public CourseContentService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, Func<string, string> mapPath)
        {
            this.db = db;
            this.mapPath = mapPath;
        }

        /// <summary>Đường dẫn video có dùng được không: URL ngoài hợp lệ, hoặc file trên máy chủ còn tồn tại.</summary>
        public bool VideoHopLe(string duongDan)
        {
            if (string.IsNullOrWhiteSpace(duongDan)) return false;
            string path = duongDan.Trim();

            if (VideoStorageService.LaR2(path)) return VideoStorageService.TonTai(path);

            Uri uri;
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return Uri.TryCreate(path, UriKind.Absolute, out uri);
            }

            try
            {
                string appPath = path.StartsWith("~/") ? path : "~/" + path.TrimStart('/');
                int query = appPath.IndexOfAny(new[] { '?', '#' });
                if (query >= 0) appPath = appPath.Substring(0, query);
                return System.IO.File.Exists(mapPath(appPath));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Các vấn đề còn thiếu của khóa học đã lưu trong CSDL (rỗng = đủ điều kiện gửi duyệt / xuất bản).
        /// Dùng khi gửi duyệt từ danh sách khóa học và khi Admin phê duyệt (tài nguyên có thể bị xóa sau khi gửi).
        /// </summary>
        public List<string> KiemTraKhoaHoc(string maKhoaHoc)
        {
            var chuong = db.ChuongHoc.Where(c => c.maKhoaHoc == maKhoaHoc).OrderBy(c => c.thuTu).ToList();
            var chuongIds = chuong.Select(c => c.maChuong).ToList();
            string tienTo = maKhoaHoc + "_";
            var bai = db.BaiHoc
                .Where(b => (b.maChuong != null && chuongIds.Contains(b.maChuong)) || (b.maChuong == null && b.maBaiHoc.StartsWith(tienTo)))
                .OrderBy(b => b.thuTu)
                .ToList();

            var loi = new List<string>();
            if (chuong.Count == 0) loi.Add("Khóa học chưa có chương học nào.");
            if (bai.Count == 0) loi.Add("Khóa học chưa có bài học nào.");
            foreach (var c in chuong.Where(c => bai.All(b => b.maChuong != c.maChuong)))
            {
                loi.Add("Chương \"" + c.tenChuong + "\" chưa có bài học.");
            }
            foreach (var b in bai)
            {
                if (string.IsNullOrWhiteSpace(b.duongDanVideo)) loi.Add("Bài \"" + b.tenBaiHoc + "\" chưa có video bài giảng.");
                else if (!VideoHopLe(b.duongDanVideo)) loi.Add("Video của bài \"" + b.tenBaiHoc + "\" không còn trên máy chủ hoặc đường dẫn không hợp lệ.");
            }
            return loi;
        }

        /// <summary>Gộp danh sách vấn đề thành một thông báo ngắn (tối đa vài dòng đầu).</summary>
        public static string TomTat(List<string> loi, int toiDa = 5)
        {
            if (loi == null || loi.Count == 0) return null;
            string s = string.Join(" ", loi.Take(toiDa));
            if (loi.Count > toiDa) s += " (và " + (loi.Count - toiDa) + " vấn đề khác)";
            return s;
        }
    }
}
