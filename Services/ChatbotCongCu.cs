using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ECOLearning.Models;
using Newtonsoft.Json.Linq;

namespace ECOLearning.Services
{
    /// <summary>Thẻ khóa học hiện dưới câu trả lời của bot (bấm vào mở trang chi tiết).</summary>
    public class ChatThe
    {
        public string MaKhoaHoc { get; set; }
        public string Ten { get; set; }
        public string AnhBia { get; set; }
        public string DanhMuc { get; set; }
        public string GiaoVien { get; set; }
        public decimal Gia { get; set; }
        public double Diem { get; set; }
        public int SoDanhGia { get; set; }
    }

    /// <summary>
    /// Các công cụ chatbot được phép gọi. Gemini chỉ "đề nghị" gọi công cụ (tên + tham số); chính server chạy, nên:
    ///   - mã người dùng lấy từ phiên đăng nhập (userId truyền vào), không bao giờ nhận từ Gemini;
    ///   - công cụ chỉ đọc dữ liệu, không ghi;
    ///   - kết quả trả về ít trường nhất đủ dùng.
    /// Thêm công cụ mới: khai báo trong <see cref="KhaiBao"/> và thêm một nhánh trong <see cref="Chay"/>.
    /// </summary>
    public class ChatbotCongCu
    {
        private const int SoKhoaToiDa = 8;
        private const int SoKhoaMacDinh = 5;

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
        private readonly int userId;
        private readonly List<ChatThe> the = new List<ChatThe>();

        public ChatbotCongCu(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, int userId)
        {
            this.db = db;
            this.userId = userId;
        }

        /// <summary>Các khóa học công cụ đã tìm thấy trong lượt hỏi này (để hiện thẻ bấm được).</summary>
        public List<ChatThe> The => the;

        public static JArray KhaiBao()
        {
            return JArray.Parse(@"[
  {
    ""name"": ""tim_khoa_hoc"",
    ""description"": ""Tìm khóa học đang bán trên E-Learning theo từ khóa chủ đề, giá tối đa, cấp độ. Chỉ trả khóa mà người dùng chưa sở hữu. Dùng cho mọi câu hỏi tìm / gợi ý / so sánh giá khóa học. Từ khóa là chủ đề ngắn (Python, thiết kế, tiếng Anh...), không phải cả câu hỏi. Bỏ trống từ khóa để xem các khóa phổ biến hoặc rẻ nhất."",
    ""parameters"": {
      ""type"": ""object"",
      ""properties"": {
        ""tuKhoa"": { ""type"": ""string"", ""description"": ""Chủ đề cần tìm, ví dụ: Python, Photoshop, kế toán. Có thể để trống."" },
        ""giaToiDa"": { ""type"": ""integer"", ""description"": ""Giá tối đa, đơn vị đồng (300000 nghĩa là 300 nghìn). 0 là chỉ khóa miễn phí."" },
        ""capDo"": { ""type"": ""string"", ""description"": ""Cấp độ: cơ bản, trung cấp hoặc nâng cao. Bỏ trống nếu không yêu cầu."" },
        ""sapXep"": { ""type"": ""string"", ""enum"": [""lien_quan"", ""gia_tang"", ""danh_gia"", ""pho_bien""], ""description"": ""lien_quan (mặc định), gia_tang (rẻ nhất trước), danh_gia (điểm cao trước), pho_bien (nhiều học viên trước)."" },
        ""soLuong"": { ""type"": ""integer"", ""description"": ""Số khóa trả về, 1 đến 8, mặc định 5."" }
      }
    }
  }
]");
        }

        /// <summary>Chạy một công cụ; trả về đối tượng JSON gửi lại cho Gemini. Lỗi công cụ cũng trả về dạng {loi: ...} để bot giải thích.</summary>
        public JObject Chay(string ten, JObject args)
        {
            try
            {
                switch (ten)
                {
                    case "tim_khoa_hoc": return TimKhoaHoc(args ?? new JObject());
                    default: return new JObject { ["loi"] = "Công cụ không tồn tại: " + ten };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("ChatbotCongCu." + ten + ": " + ex);
                return new JObject { ["loi"] = "Công cụ gặp sự cố, hãy xin lỗi người dùng và gợi ý thử lại." };
            }
        }

        private JObject TimKhoaHoc(JObject a)
        {
            string tuKhoa = ((string)a["tuKhoa"] ?? "").Trim();
            decimal? giaToiDa = DocSo(a["giaToiDa"]);
            string capDo = ((string)a["capDo"] ?? "").Trim();
            string sapXep = ((string)a["sapXep"] ?? "lien_quan").Trim();
            int soLuong = (int)Math.Max(1, Math.Min(SoKhoaToiDa, DocSo(a["soLuong"]) ?? SoKhoaMacDinh));

            var search = new SearchService(db);
            var cm = search.LayChiMuc();
            var boQua = search.BoQuaKhoaDaSoHuu(userId);

            bool khopMotPhan = false;
            IEnumerable<SearchService.KhoaHocChiMuc> ds;
            if (SearchText.KhongDau(tuKhoa).Length > 0)
                ds = search.XepHang(cm, tuKhoa, boQua, out khopMotPhan).Select(x => x.Key).ToList();
            else
                ds = cm.KhoaHoc.Where(k => !boQua(k)).OrderByDescending(k => k.SoHocVien).ThenByDescending(k => k.DiemDanhGia).ToList();

            if (giaToiDa.HasValue) ds = ds.Where(k => k.GiaHienTai <= giaToiDa.Value);
            string capDoKd = SearchText.KhongDau(capDo);
            if (capDoKd.Length > 0) ds = ds.Where(k => SearchText.KhongDau(k.CapDo ?? "").Contains(capDoKd));

            switch (sapXep)
            {
                case "gia_tang": ds = ds.OrderBy(k => k.GiaHienTai).ThenByDescending(k => k.DiemDanhGia); break;
                case "danh_gia": ds = ds.OrderByDescending(k => k.DiemDanhGia).ThenByDescending(k => k.SoDanhGia); break;
                case "pho_bien": ds = ds.OrderByDescending(k => k.SoHocVien).ThenByDescending(k => k.DiemDanhGia); break;
            }

            var tatCa = ds.ToList();
            var chon = tatCa.Take(soLuong).ToList();

            foreach (var k in chon)
            {
                if (the.Any(t => t.MaKhoaHoc == k.MaKhoaHoc)) continue;
                the.Add(new ChatThe
                {
                    MaKhoaHoc = k.MaKhoaHoc,
                    Ten = k.TenKhoaHoc,
                    AnhBia = k.AnhBia,
                    DanhMuc = k.TenDanhMuc,
                    GiaoVien = k.TenGiaoVien,
                    Gia = k.GiaHienTai,
                    Diem = k.DiemDanhGia,
                    SoDanhGia = k.SoDanhGia
                });
            }

            var kq = new JObject
            {
                ["tongSoTimDuoc"] = tatCa.Count,
                ["khopMotPhan"] = khopMotPhan,
                ["khoaHoc"] = new JArray(chon.Select(k => new JObject
                {
                    ["maKhoaHoc"] = k.MaKhoaHoc,
                    ["ten"] = k.TenKhoaHoc,
                    ["danhMuc"] = k.TenDanhMuc,
                    ["capDo"] = k.CapDo,
                    ["giaDong"] = (long)k.GiaHienTai,
                    ["giaoVien"] = k.TenGiaoVien,
                    ["diemDanhGia"] = Math.Round(k.DiemDanhGia, 1),
                    ["soDanhGia"] = k.SoDanhGia,
                    ["soHocVien"] = k.SoHocVien
                }))
            };
            if (chon.Count == 0) kq["ghiChu"] = "Không có khóa nào khớp. Hãy nói rõ với người dùng, có thể gợi ý nới giá hoặc đổi từ khóa.";
            return kq;
        }

        /// <summary>Gemini gửi số kiểu 300000 hoặc 3e5 hoặc "300000"; đọc theo InvariantCulture (máy chủ dùng vi-VN).</summary>
        private static decimal? DocSo(JToken t)
        {
            if (t == null || t.Type == JTokenType.Null) return null;
            if (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) return (decimal)t;
            decimal v;
            if (t.Type == JTokenType.String && decimal.TryParse((string)t, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;
            return null;
        }
    }
}
