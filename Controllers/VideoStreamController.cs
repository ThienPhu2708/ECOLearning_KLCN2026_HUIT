using System;
using System.IO;
using System.Web.Mvc;
using System.Web.SessionState;
using ECOLearning.Services;

namespace ECOLearning.Controllers
{
    /// <summary>
    /// Phát video bài học. Tách khỏi LearningController và TẮT Session: ASP.NET chỉ xử lý lần lượt các request dùng Session
    /// của cùng một người, mà request tải video kéo dài suốt lúc xem. Khi còn nằm trong LearningController, request video giữ
    /// khóa Session nên các request lưu tiến độ (UpdateProgress) và cả việc chuyển trang phải xếp hàng chờ, tiến độ không được lưu.
    /// </summary>
    [SessionState(SessionStateBehavior.Disabled)]
    public class VideoStreamController : Controller
    {
        // GET: /VideoStream/Play?file=... (Phát video nội bộ có hỗ trợ HTTP Range Request để tua/streaming mượt,
        // vì IIS Express không phục vụ đúng Range cho static file, gây hiện tượng có tiếng nhưng hình đứng)
        // GET: /VideoStream/Play?src=r2:videos/... (video trên Cloudflare R2: còn chờ đẩy lên thì phát từ file tạm,
        // đã lên thì chuyển hướng sang link ký có hạn dùng 6 giờ; trình phát tự xin link mới khi tải lại)
        public ActionResult Play(string file, string src)
        {
            if (!string.IsNullOrEmpty(src))
            {
                string key = src.StartsWith(VideoStorageService.TienToR2, StringComparison.OrdinalIgnoreCase) ? src.Substring(VideoStorageService.TienToR2.Length) : null;
                if (!VideoStorageService.LaKeyHopLe(key)) return new HttpStatusCodeResult(400, "Đường dẫn video không hợp lệ");
                string tam = VideoStorageService.FileTamDangCho(key);
                if (tam != null) return PhatFile(tam);
                if (!R2Storage.DaCauHinh) return HttpNotFound();
                Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
                return Redirect(R2Storage.LinkXem(key, TimeSpan.FromHours(6)));
            }

            if (string.IsNullOrEmpty(file) || file.Contains("..") || file.Contains("/") || file.Contains("\\"))
            {
                return new HttpStatusCodeResult(400, "Tên file không hợp lệ");
            }

            string videoDir = Server.MapPath("~/assets/uploads/videos/");
            string fullPath = Path.Combine(videoDir, file);

            if (!fullPath.StartsWith(videoDir, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(fullPath))
            {
                return HttpNotFound();
            }
            return PhatFile(fullPath);
        }

        private ActionResult PhatFile(string fullPath)
        {
            string contentType = VideoStorageService.LoaiNoiDung(Path.GetExtension(fullPath));

            long totalLength = new FileInfo(fullPath).Length;
            long start = 0;
            long end = totalLength - 1;

            Response.Clear();
            Response.BufferOutput = false;
            Response.AddHeader("Accept-Ranges", "bytes");
            Response.ContentType = contentType;

            string rangeHeader = Request.Headers["Range"];
            if (!string.IsNullOrEmpty(rangeHeader) && rangeHeader.StartsWith("bytes="))
            {
                string[] parts = rangeHeader.Substring("bytes=".Length).Split('-');
                if (!long.TryParse(parts[0], out start)) start = 0;
                if (parts.Length > 1 && parts[1].Length > 0 && long.TryParse(parts[1], out long parsedEnd))
                {
                    end = parsedEnd;
                }
                if (end >= totalLength) end = totalLength - 1;

                if (start < 0 || start > end)
                {
                    Response.StatusCode = 416;
                    Response.AddHeader("Content-Range", "bytes */" + totalLength);
                    return new EmptyResult();
                }

                Response.StatusCode = 206;
                Response.StatusDescription = "Partial Content";
                Response.AddHeader("Content-Range", string.Format("bytes {0}-{1}/{2}", start, end, totalLength));
            }

            long contentLength = end - start + 1;
            Response.AddHeader("Content-Length", contentLength.ToString());

            const int bufferSize = 65536;
            byte[] buffer = new byte[bufferSize];

            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                stream.Seek(start, SeekOrigin.Begin);
                long bytesRemaining = contentLength;

                while (bytesRemaining > 0 && Response.IsClientConnected)
                {
                    int bytesToRead = (int)Math.Min(bufferSize, bytesRemaining);
                    int bytesRead = stream.Read(buffer, 0, bytesToRead);
                    if (bytesRead == 0) break;

                    try
                    {
                        Response.OutputStream.Write(buffer, 0, bytesRead);
                    }
                    catch (System.Web.HttpException ex) when (
                        ex.ErrorCode == unchecked((int)0x800703E3) || !Response.IsClientConnected)
                    {
                        // The browser can cancel a video request after the connection check
                        // (for example when seeking or leaving the page).
                        break;
                    }
                    bytesRemaining -= bytesRead;
                }
            }

            return new EmptyResult();
        }
    }
}
