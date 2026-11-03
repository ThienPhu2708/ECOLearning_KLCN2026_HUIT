using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ECOLearning.Models;
using Newtonsoft.Json;

namespace ECOLearning.Services
{
    /// <summary>
    /// Tích hợp cổng VNPay (API 2.1.0) ở môi trường SANDBOX. Cấu hình trong Secrets.config (không đưa lên Git):
    ///   Payment:Mode = VNPay (khác giá trị này thì dùng cổng thử nghiệm nội bộ /Payment/Gateway)
    ///   VNPay:TmnCode, VNPay:HashSecret, VNPay:PaymentUrl, VNPay:ApiUrl
    /// Luồng: đơn "Chờ thanh toán" → TaoUrlThanhToan (ký HMAC-SHA512) → trang VNPay → VNPay chuyển về /Payment/VnpayReturn
    /// và gọi IPN /Payment/VnpayIpn. Trên máy phát triển VNPay không gọi được IPN vào localhost, nên trang quay về
    /// xác nhận lại bằng API truy vấn giao dịch (querydr) rồi mới ghi nhận. Ghi nhận dùng chung PaymentService.GhiNhanKetQua.
    /// </summary>
    public class VnPayService
    {
        public const string TenCong = "VNPay Sandbox";
        private const string PhienBan = "2.1.0";

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
        private readonly PaymentService payment;

        public VnPayService(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db)
        {
            this.db = db;
            payment = new PaymentService(db);
        }

        private static string CauHinh(string key) => (ConfigurationManager.AppSettings[key] ?? "").Trim();
        private static string TmnCode => CauHinh("VNPay:TmnCode");
        private static string HashSecret => CauHinh("VNPay:HashSecret");
        private static string PaymentUrl => string.IsNullOrEmpty(CauHinh("VNPay:PaymentUrl")) ? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html" : CauHinh("VNPay:PaymentUrl");
        private static string ApiUrl => string.IsNullOrEmpty(CauHinh("VNPay:ApiUrl")) ? "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction" : CauHinh("VNPay:ApiUrl");

        /// <summary>Đang dùng VNPay: Payment:Mode = VNPay và đã có đủ TmnCode / HashSecret.</summary>
        public static bool DangBat =>
            string.Equals(CauHinh("Payment:Mode"), "VNPay", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(TmnCode) && !string.IsNullOrEmpty(HashSecret);

        /// <summary>
        /// Phương thức hoàn tất được trên VNPay Sandbox: chỉ thẻ ATM nội địa (thẻ test NCB).
        /// Ví / quét mã (VNPAY-QR, App VNPAY): app ngân hàng, ví thật không thanh toán mã QR của môi trường test.
        /// Thẻ quốc tế: tài khoản sandbox của hệ thống chưa được VNPay mở kênh thẻ quốc tế.
        /// </summary>
        public static bool HoTroPhuongThuc(string phuongThuc) => phuongThuc == "Napas";

        public const string ThongBaoChuaHoTro = "Phương thức này hiện chưa được hỗ trợ. "
            + "Vui lòng chọn Thẻ ATM nội địa.";

        // ------------------------------------------------------------------ thời gian GMT+7

        private static DateTime GioVietNam(DateTime local)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                return TimeZoneInfo.ConvertTime(local, TimeZoneInfo.Local, tz);
            }
            catch (TimeZoneNotFoundException)
            {
                return local.ToUniversalTime().AddHours(7);
            }
        }

        private static string DinhDang(DateTime local) => GioVietNam(local).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------ chữ ký

        private static string Hmac(string data)
        {
            using (var h = new HMACSHA512(Encoding.UTF8.GetBytes(HashSecret)))
            {
                return BitConverter.ToString(h.ComputeHash(Encoding.UTF8.GetBytes(data))).Replace("-", "").ToLowerInvariant();
            }
        }

        /// <summary>Chuỗi tham số vnp_* sắp theo tên (thứ tự byte), mã hóa URL từng tên và giá trị (giống mã mẫu .NET của VNPay).</summary>
        private static string ChuoiThamSo(IEnumerable<KeyValuePair<string, string>> thamSo)
        {
            return string.Join("&", thamSo
                .Where(kv => !string.IsNullOrEmpty(kv.Value))
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value)));
        }

        private static bool BangNhau(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int khac = 0;
            for (int i = 0; i < a.Length; i++) khac |= char.ToLowerInvariant(a[i]) ^ char.ToLowerInvariant(b[i]);
            return khac == 0;
        }

        /// <summary>Kiểm tra chữ ký dữ liệu VNPay gửi về (trang quay về hoặc IPN): ký lại các tham số vnp_* trừ vnp_SecureHash / vnp_SecureHashType.</summary>
        public static bool ChuKyHopLe(IDictionary<string, string> thamSo)
        {
            string chuKy;
            if (!thamSo.TryGetValue("vnp_SecureHash", out chuKy) || string.IsNullOrEmpty(chuKy)) return false;
            var duLieu = thamSo.Where(kv => kv.Key.StartsWith("vnp_", StringComparison.Ordinal)
                                         && kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType");
            return BangNhau(Hmac(ChuoiThamSo(duLieu)), chuKy);
        }

        // ------------------------------------------------------------------ tạo URL thanh toán

        /// <summary>
        /// URL chuyển học viên sang VNPay. vnp_TxnRef = mã đơn + "_" + giờ phút giây để mỗi lần mở cổng là một giao dịch mới
        /// (VNPay không nhận trùng TxnRef trong ngày). Thời điểm tạo được ghi nhật ký để API truy vấn dùng lại.
        /// </summary>
        public string TaoUrlThanhToan(DonThanhToan don, string ipKhach, string returnUrl)
        {
            var bayGio = DateTime.Now;
            string txnRef = don.MaDonHang + "_" + bayGio.ToString("HHmmss");
            string taoLuc = DinhDang(bayGio);
            var thamSo = new Dictionary<string, string>
            {
                { "vnp_Version", PhienBan },
                { "vnp_Command", "pay" },
                { "vnp_TmnCode", TmnCode },
                { "vnp_Amount", ((long)decimal.Round(don.TongTien, 0) * 100).ToString(CultureInfo.InvariantCulture) },
                { "vnp_CreateDate", taoLuc },
                { "vnp_ExpireDate", DinhDang(don.NgayHetHan ?? bayGio.AddMinutes(PaymentService.PhutHetHan)) },
                { "vnp_CurrCode", "VND" },
                { "vnp_IpAddr", ChuanHoaIp(ipKhach) },
                { "vnp_Locale", "vn" },
                { "vnp_OrderInfo", "Thanh toan don hang " + don.MaDonHang },
                { "vnp_OrderType", "other" },
                { "vnp_ReturnUrl", returnUrl },
                { "vnp_TxnRef", txnRef }
            };
            // Luôn gửi vnp_BankCode = VNBANK để VNPay mở thẳng danh sách ngân hàng, không hiện trang chọn phương thức
            // có các mục chưa dùng được (đơn cũ chọn ví / thẻ quốc tế được chuyển sang thẻ nội địa)
            thamSo["vnp_BankCode"] = "VNBANK";

            string query = ChuoiThamSo(thamSo);
            payment.GhiNhatKy(don.MaDonHang, "TAO_URL", "vnp_TxnRef=" + txnRef + "&vnp_CreateDate=" + taoLuc, null, null,
                "Chuyển sang " + TenCong + " (" + thamSo["vnp_BankCode"] + ")");
            return PaymentUrl + "?" + query + "&vnp_SecureHash=" + Hmac(query);
        }

        private static string ChuanHoaIp(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip) || ip == "::1") return "127.0.0.1";
            return ip.Length > 45 ? ip.Substring(0, 45) : ip;
        }

        /// <summary>Mã đơn trong hệ thống từ vnp_TxnRef (phần trước dấu _).</summary>
        public static string MaDonTuTxnRef(string txnRef)
        {
            if (string.IsNullOrEmpty(txnRef)) return null;
            int i = txnRef.IndexOf('_');
            return i > 0 ? txnRef.Substring(0, i) : txnRef;
        }

        // ------------------------------------------------------------------ ghi nhận kết quả

        /// <summary>
        /// Xử lý dữ liệu VNPay gửi về (IPN hoặc trang quay về): kiểm chữ ký → ghi nhận qua PaymentService.GhiNhanKetQua.
        /// Trả mã theo quy ước IPN của VNPay: 00, 01, 02, 04, 97, 99.
        /// </summary>
        public KetQuaIpn XuLyKetQua(IDictionary<string, string> thamSo, string loai)
        {
            string txnRef = Lay(thamSo, "vnp_TxnRef");
            string maDon = MaDonTuTxnRef(txnRef);
            string duLieu = ChuoiThamSo(thamSo.Where(kv => kv.Key != "vnp_SecureHash"));
            if (!ChuKyHopLe(thamSo))
            {
                payment.GhiNhatKy(maDon, loai, duLieu, false, "97", "Sai chữ ký VNPay");
                return KetQuaIpn.Tra("97", "Invalid signature");
            }
            if (Lay(thamSo, "vnp_TmnCode") != TmnCode)
            {
                payment.GhiNhatKy(maDon, loai, duLieu, true, "97", "Sai mã website");
                return KetQuaIpn.Tra("97", "Invalid TmnCode");
            }

            long soTien;
            long.TryParse(Lay(thamSo, "vnp_Amount"), out soTien);
            string maKq = Lay(thamSo, "vnp_ResponseCode");
            bool thanhCong = maKq == "00" && Lay(thamSo, "vnp_TransactionStatus") == "00";

            // Trang quay về trên máy phát triển: hỏi lại VNPay qua API truy vấn trước khi ghi nhận thành công
            if (thanhCong && loai == "RETURN")
            {
                var xacNhan = TruyVan(txnRef, maDon);
                if (xacNhan != null)
                {
                    thanhCong = xacNhan.ThanhCong && xacNhan.SoTien == soTien;
                    if (!thanhCong) maKq = xacNhan.TrangThai ?? "99";
                    duLieu += " | querydr: " + xacNhan.MoTa;
                }
                else
                {
                    // Không gọi được API (mất mạng / VNPay lỗi): dữ liệu quay về đã đúng chữ ký VNPay nên vẫn tin được
                    duLieu += " | querydr: không gọi được (" + LoiTruyVan + "), dùng dữ liệu quay về đã xác thực chữ ký";
                }
            }

            return payment.GhiNhanKetQua(maDon, soTien / 100, Lay(thamSo, "vnp_TransactionNo"), thanhCong, maKq,
                PhuongThuc(thamSo), TenCong, duLieu, loai);
        }

        private static string PhuongThuc(IDictionary<string, string> thamSo)
        {
            string loaiThe = Lay(thamSo, "vnp_CardType");
            string nganHang = Lay(thamSo, "vnp_BankCode");
            string ten = loaiThe == "ATM" ? "Thẻ ATM nội địa" : loaiThe == "QRCODE" ? "VNPay-QR" : loaiThe == "INTCARD" || loaiThe == "VISA" ? "Thẻ quốc tế" : "VNPay";
            return string.IsNullOrEmpty(nganHang) ? ten : ten + " " + nganHang;
        }

        // ------------------------------------------------------------------ API truy vấn giao dịch (querydr)

        public class KetQuaTruyVan
        {
            public bool ThanhCong { get; set; }
            public long SoTien { get; set; }
            public string TrangThai { get; set; }
            public string MoTa { get; set; }
        }

        /// <summary>Hỏi VNPay trạng thái giao dịch (server gọi ra, không cần VNPay gọi vào máy). Null nếu không gọi được.</summary>
        /// <summary>Lý do lần truy vấn gần nhất thất bại (ghi vào nhật ký thanh toán).</summary>
        public string LoiTruyVan { get; private set; }

        public KetQuaTruyVan TruyVan(string txnRef, string maDon)
        {
            // vnp_TransactionDate phải đúng vnp_CreateDate đã gửi khi tạo giao dịch (lưu ở nhật ký TAO_URL)
            string ghiLuc = db.Database.SqlQuery<string>(@"
                SELECT TOP 1 duLieu FROM dbo.NhatKyThanhToan
                WHERE loai = 'TAO_URL' AND maDonHang = @dh AND duLieu LIKE @ref ORDER BY maNhatKy DESC",
                new System.Data.SqlClient.SqlParameter("@dh", maDon),
                new System.Data.SqlClient.SqlParameter("@ref", "vnp_TxnRef=" + txnRef + "&%")).FirstOrDefault();
            var m = ghiLuc == null ? null : Regex.Match(ghiLuc, @"vnp_CreateDate=(\d{14})");
            if (m == null || !m.Success) { LoiTruyVan = "Không tìm thấy thời điểm tạo giao dịch trong nhật ký"; return null; }

            string requestId = Guid.NewGuid().ToString("N").Substring(0, 20);
            string taoLuc = DinhDang(DateTime.Now);
            string ip = "127.0.0.1";
            string orderInfo = "Truy van giao dich " + txnRef;
            string chuKy = Hmac(string.Join("|", requestId, PhienBan, "querydr", TmnCode, txnRef, m.Groups[1].Value, taoLuc, ip, orderInfo));
            var body = JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { "vnp_RequestId", requestId }, { "vnp_Version", PhienBan }, { "vnp_Command", "querydr" },
                { "vnp_TmnCode", TmnCode }, { "vnp_TxnRef", txnRef }, { "vnp_OrderInfo", orderInfo },
                { "vnp_TransactionDate", m.Groups[1].Value }, { "vnp_CreateDate", taoLuc },
                { "vnp_IpAddr", ip }, { "vnp_SecureHash", chuKy }
            });

            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(ApiUrl);
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Accept = "application/json";
                // Tường lửa của VNPay chặn (403) request không có User-Agent; bỏ "Expect: 100-continue" mặc định của .NET
                req.UserAgent = "ECOLearning/1.0 (+https://localhost)";
                req.ServicePoint.Expect100Continue = false;
                req.Timeout = 15000;
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                using (var st = req.GetRequestStream()) st.Write(bytes, 0, bytes.Length);
                string json;
                using (var res = (HttpWebResponse)req.GetResponse())
                using (var rd = new StreamReader(res.GetResponseStream(), Encoding.UTF8)) json = rd.ReadToEnd();

                var r = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                string g(string k) { string v; return r != null && r.TryGetValue(k, out v) ? v ?? "" : ""; }
                // Kiểm tra chữ ký phản hồi của VNPay
                string duLieuPh = string.Join("|", g("vnp_ResponseId"), g("vnp_Command"), g("vnp_ResponseCode"), g("vnp_Message"),
                    g("vnp_TmnCode"), g("vnp_TxnRef"), g("vnp_Amount"), g("vnp_BankCode"), g("vnp_PayDate"), g("vnp_TransactionNo"),
                    g("vnp_TransactionType"), g("vnp_TransactionStatus"), g("vnp_OrderInfo"), g("vnp_PromotionCode"), g("vnp_PromotionAmount"));
                bool chuKyDung = BangNhau(Hmac(duLieuPh), g("vnp_SecureHash"));
                long soTien;
                long.TryParse(g("vnp_Amount"), out soTien);
                return new KetQuaTruyVan
                {
                    ThanhCong = chuKyDung && g("vnp_ResponseCode") == "00" && g("vnp_TransactionStatus") == "00",
                    SoTien = soTien,
                    TrangThai = g("vnp_TransactionStatus"),
                    MoTa = "ResponseCode=" + g("vnp_ResponseCode") + ", TransactionStatus=" + g("vnp_TransactionStatus")
                           + ", chữ ký " + (chuKyDung ? "hợp lệ" : "SAI") + ", " + g("vnp_Message")
                };
            }
            catch (WebException ex) { LoiTruyVan = "WebException: " + ex.Status + " " + ex.Message; return null; }
            catch (IOException ex) { LoiTruyVan = "IOException: " + ex.Message; return null; }
            catch (JsonException ex) { LoiTruyVan = "JsonException: " + ex.Message; return null; }
        }

        private static string Lay(IDictionary<string, string> p, string k)
        {
            string v;
            return p.TryGetValue(k, out v) ? v : null;
        }
    }
}
