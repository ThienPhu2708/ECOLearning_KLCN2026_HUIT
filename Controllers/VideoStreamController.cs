using System;
using System.IO;
using System.Web.Mvc;
using System.Web.SessionState;

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
        public ActionResult Play(string file)
        {
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

            string ext = Path.GetExtension(fullPath).ToLowerInvariant();
            string contentType;
            switch (ext)
            {
                case ".webm": contentType = "video/webm"; break;
                case ".mov": contentType = "video/quicktime"; break;
                case ".mkv": contentType = "video/x-matroska"; break;
                case ".ogv": contentType = "video/ogg"; break;
                default: contentType = "video/mp4"; break;
            }

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

            using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
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
