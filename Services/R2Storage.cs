using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Net;
using System.Web;
using System.Web.Caching;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace ECOLearning.Services
{
    /// <summary>
    /// Kho video trên Cloudflare R2 (giao thức S3). Khóa cấu hình R2:* nằm trong Secrets.config (mẫu: Secrets.config.example).
    /// Bucket để riêng tư: người xem nhận link có hạn dùng (presigned) do máy chủ ký; điền R2:PublicUrl nếu bật truy cập công khai.
    /// Độ dài video (giây) lưu trong metadata "duration" của object để không phải tải video về khi cần.
    /// </summary>
    public static class R2Storage
    {
        public class ThongTinObject
        {
            public long KichThuoc { get; set; }
            public int? SoGiay { get; set; }
        }

        private static readonly object KhoaClient = new object();
        private static AmazonS3Client client;
        private static string clientCauHinh;

        private static string Cfg(string ten)
        {
            return (ConfigurationManager.AppSettings["R2:" + ten] ?? "").Trim();
        }

        private static bool CoGiaTri(string giaTri)
        {
            return giaTri.Length > 0 && !giaTri.StartsWith("DIEN_", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Đã điền đủ AccountId, BucketName, AccessKeyId, SecretAccessKey (không còn giá trị mẫu DIEN_...).</summary>
        public static bool DaCauHinh
        {
            get { return CoGiaTri(Cfg("AccountId")) && CoGiaTri(Cfg("BucketName")) && CoGiaTri(Cfg("AccessKeyId")) && CoGiaTri(Cfg("SecretAccessKey")); }
        }

        public static string TenBucket { get { return Cfg("BucketName"); } }

        private static AmazonS3Client Client
        {
            get
            {
                string cauHinh = Cfg("AccountId") + "|" + Cfg("AccessKeyId") + "|" + Cfg("SecretAccessKey");
                lock (KhoaClient)
                {
                    if (client == null || clientCauHinh != cauHinh)
                    {
                        // R2: endpoint theo Account ID, vùng "auto", đường dẫn dạng /bucket/key; R2 không nhận checksum mặc định của SDK v4
                        var config = new AmazonS3Config
                        {
                            ServiceURL = "https://" + Cfg("AccountId") + ".r2.cloudflarestorage.com",
                            ForcePathStyle = true,
                            AuthenticationRegion = "auto",
                            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                            Timeout = TimeSpan.FromMinutes(30),
                            MaxErrorRetry = 3
                        };
                        if (client != null) client.Dispose();
                        client = new AmazonS3Client(new BasicAWSCredentials(Cfg("AccessKeyId"), Cfg("SecretAccessKey")), config);
                        clientCauHinh = cauHinh;
                    }
                    return client;
                }
            }
        }

        /// <summary>Tải file lên R2 (một lần PUT, tối đa 5GB).</summary>
        public static void TaiLen(string duongDanFile, string key, string contentType, int? soGiay)
        {
            var req = new PutObjectRequest
            {
                BucketName = TenBucket,
                Key = key,
                FilePath = duongDanFile,
                ContentType = contentType,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true
            };
            if (soGiay.HasValue && soGiay.Value > 0) req.Metadata.Add("duration", soGiay.Value.ToString(CultureInfo.InvariantCulture));
            Client.PutObject(req);
            HttpRuntime.Cache.Remove(KhoaCache(key));
        }

        /// <summary>
        /// Tải một object riêng tư từ R2 về file tạm trên máy chủ. Dùng cho các tác vụ nền
        /// như tạo phụ đề; không tạo URL công khai và không đi qua trình duyệt học viên.
        /// </summary>
        public static void TaiXuong(string key, string duongDanDich)
        {
            if (!DaCauHinh) throw new InvalidOperationException("Chưa cấu hình Cloudflare R2.");
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Thiếu key R2.", "key");
            if (string.IsNullOrWhiteSpace(duongDanDich)) throw new ArgumentException("Thiếu đường dẫn đích.", "duongDanDich");

            string thuMuc = Path.GetDirectoryName(duongDanDich);
            if (!string.IsNullOrEmpty(thuMuc)) Directory.CreateDirectory(thuMuc);
            using (var response = Client.GetObject(new GetObjectRequest { BucketName = TenBucket, Key = key }))
            using (var input = response.ResponseStream)
            using (var output = new FileStream(duongDanDich, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
            }
        }

        /// <summary>Thông tin object (HEAD), null nếu không có. Cache 6 giờ khi có, 2 phút khi không có hoặc lỗi mạng.</summary>
        public static ThongTinObject LayThongTin(string key)
        {
            if (!DaCauHinh || string.IsNullOrEmpty(key)) return null;
            string khoa = KhoaCache(key);
            object daCo = HttpRuntime.Cache[khoa];
            if (daCo != null) return daCo as ThongTinObject;

            ThongTinObject ketQua = null;
            try
            {
                var res = Client.GetObjectMetadata(new GetObjectMetadataRequest { BucketName = TenBucket, Key = key });
                int giay;
                string dur = res.Metadata["duration"];
                ketQua = new ThongTinObject
                {
                    KichThuoc = res.ContentLength,
                    SoGiay = int.TryParse(dur, NumberStyles.Integer, CultureInfo.InvariantCulture, out giay) && giay > 0 ? giay : (int?)null
                };
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) { }
            catch (Exception ex)
            {
                AppLog.Ghi("R2", "Không đọc được thông tin " + key + ": " + ex.Message);
            }

            HttpRuntime.Cache.Insert(khoa, (object)ketQua ?? false, null,
                DateTime.UtcNow.Add(ketQua != null ? TimeSpan.FromHours(6) : TimeSpan.FromMinutes(2)), Cache.NoSlidingExpiration);
            return ketQua;
        }

        /// <summary>Link xem video: link công khai nếu có R2:PublicUrl, ngược lại link ký có hạn dùng.</summary>
        public static string LinkXem(string key, TimeSpan hanDung)
        {
            string congKhai = Cfg("PublicUrl");
            if (congKhai.Length > 0) return congKhai.TrimEnd('/') + "/" + key;
            return Client.GetPreSignedURL(new GetPreSignedUrlRequest
            {
                BucketName = TenBucket,
                Key = key,
                Verb = HttpVerb.GET,
                Protocol = Protocol.HTTPS,
                Expires = DateTime.UtcNow.Add(hanDung)
            });
        }

        public static void Xoa(string key)
        {
            Client.DeleteObject(new DeleteObjectRequest { BucketName = TenBucket, Key = key });
            HttpRuntime.Cache.Remove(KhoaCache(key));
        }

        private static string KhoaCache(string key)
        {
            return "r2head:" + key;
        }
    }
}
