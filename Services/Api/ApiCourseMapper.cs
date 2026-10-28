using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ECOLearning.Models;

namespace ECOLearning.Services.Api
{
    /// <summary>
    /// Thẻ khóa học trả cho app (CourseSummary, Task_Tuan10.docx mục 5.5). Tên thuộc tính đổi sang camelCase khi trả JSON.
    /// </summary>
    public class CourseSummaryDto
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string CategoryId { get; set; }
        public string Category { get; set; }
        public string Instructor { get; set; }
        public string InstructorAvatar { get; set; }
        public string ImageUrl { get; set; }
        public string Level { get; set; }
        public double Rating { get; set; }
        public int ReviewCount { get; set; }
        public long Price { get; set; }
        public long OriginalPrice { get; set; }

        /// <summary>Chương trình giảm trực tiếp đang áp dụng cho khóa; null khi không giảm.</summary>
        public CoursePromotionDto Promotion { get; set; }
        public bool IsFree { get; set; }
        public int LessonsCount { get; set; }
        public int DurationSeconds { get; set; }
        public int StudentsCount { get; set; }
        public string Tag { get; set; }
    }

    /// <summary>
    /// Chương trình giảm trực tiếp trên thẻ khóa học. DiscountPercent là % thực tế sau khi áp trần giảm tối đa
    /// (khớp nhãn "-X%" app tự tính từ price và originalPrice), EndsAt là ngày kết thúc (yyyy-MM-dd).
    /// </summary>
    public class CoursePromotionDto
    {
        public string Name { get; set; }
        public int DiscountPercent { get; set; }
        public string EndsAt { get; set; }
    }

    /// <summary>Lấy thông tin thẻ của nhiều khóa học bằng một truy vấn, giữ nguyên thứ tự mã truyền vào.</summary>
    public class ApiCourseMapper
    {
        public const string AnhBiaMacDinh = "https://images.unsplash.com/photo-1517694712202-14dd9538aa97?w=600&auto=format&fit=crop&q=80";
        public const string AnhDaiDienMacDinh = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=100&auto=format&fit=crop&q=80";

        private readonly QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db;
        private readonly Func<string, string> urlDayDu;

        /// <param name="urlDayDu">Đổi đường dẫn tương đối (/assets/...) thành đường dẫn đầy đủ cho app</param>
        public ApiCourseMapper(QL_HETHONG_CUNGCAP_KHOAHOCEntities1 db, Func<string, string> urlDayDu)
        {
            this.db = db;
            this.urlDayDu = urlDayDu;
        }

        private class Row
        {
            public string MaKhoaHoc { get; set; }
            public string TenKhoaHoc { get; set; }
            public string MaDanhMuc { get; set; }
            public string TenDanhMuc { get; set; }
            public string TenGiaoVien { get; set; }
            public string AnhGiaoVien { get; set; }
            public string AnhBia { get; set; }
            public string CapDo { get; set; }
            public decimal GiaBan { get; set; }
            public int SoDanhGia { get; set; }
            public double DiemDanhGia { get; set; }
            public int SoBai { get; set; }
            public int TongGiay { get; set; }
            public int SoHocVien { get; set; }
        }

        public List<CourseSummaryDto> Map(IEnumerable<string> courseIds)
        {
            var ids = courseIds.Where(id => !string.IsNullOrWhiteSpace(id) && !id.Contains(",")).Distinct().ToList();
            if (ids.Count == 0) return new List<CourseSummaryDto>();

            var rows = db.Database.SqlQuery<Row>(@"
                SELECT k.maKhoaHoc AS MaKhoaHoc, k.tenKhoaHoc AS TenKhoaHoc, k.maDanhMuc AS MaDanhMuc, dm.tenDanhMuc AS TenDanhMuc,
                       u.hoTen AS TenGiaoVien, u.anhDaiDien AS AnhGiaoVien, k.anhBia AS AnhBia, k.capDo AS CapDo,
                       k.giaBan AS GiaBan,
                       ISNULL(dg.soLuong, 0) AS SoDanhGia, ISNULL(dg.diem, 0) AS DiemDanhGia,
                       ISNULL(bh.soBai, 0) AS SoBai, ISNULL(bh.tongGiay, 0) AS TongGiay,
                       ISNULL(dk.soHocVien, 0) AS SoHocVien
                FROM dbo.KhoaHoc k
                INNER JOIN (SELECT DISTINCT value AS ma FROM STRING_SPLIT(@ids, ',')) s ON s.ma = k.maKhoaHoc
                LEFT JOIN dbo.DanhMuc dm ON dm.maDanhMuc = k.maDanhMuc
                LEFT JOIN dbo.NguoiDung u ON u.maNguoiDung = k.maGiaoVien
                OUTER APPLY (SELECT COUNT(*) AS soLuong, AVG(CAST(d.soSao AS FLOAT)) AS diem
                             FROM dbo.DanhGiaKhoaHoc d WHERE d.maKhoaHoc = k.maKhoaHoc) dg
                OUTER APPLY (SELECT COUNT(*) AS soBai, SUM(ISNULL(b.thoiLuong, 0)) AS tongGiay
                             FROM dbo.BaiHoc b LEFT JOIN dbo.ChuongHoc ch ON ch.maChuong = b.maChuong
                             WHERE ch.maKhoaHoc = k.maKhoaHoc
                                OR (b.maChuong IS NULL AND LEFT(b.maBaiHoc, LEN(k.maKhoaHoc) + 1) = k.maKhoaHoc + '_')) bh
                OUTER APPLY (SELECT COUNT(*) AS soHocVien FROM dbo.DangKyKhoaHoc r WHERE r.maKhoaHoc = k.maKhoaHoc) dk",
                new SqlParameter("@ids", string.Join(",", ids))).ToList();

            var theoMa = rows.ToDictionary(r => r.MaKhoaHoc, StringComparer.OrdinalIgnoreCase);
            return ids.Where(theoMa.ContainsKey).Select(id => ToDto(theoMa[id])).ToList();
        }

        public CourseSummaryDto MapOne(string courseId) => Map(new[] { courseId }).FirstOrDefault();

        private CourseSummaryDto ToDto(Row r)
        {
            // Server tính một lần giá hiệu lực (CourseDiscountService): Price = giá bán sau giảm trực tiếp, OriginalPrice = giá gốc
            var gia = CourseDiscountService.Tinh(r.GiaBan, r.MaKhoaHoc, r.MaDanhMuc);
            return new CourseSummaryDto
            {
                Id = r.MaKhoaHoc,
                Title = r.TenKhoaHoc,
                CategoryId = r.MaDanhMuc,
                Category = r.TenDanhMuc,
                Instructor = r.TenGiaoVien,
                InstructorAvatar = urlDayDu(string.IsNullOrWhiteSpace(r.AnhGiaoVien) ? AnhDaiDienMacDinh : r.AnhGiaoVien),
                ImageUrl = urlDayDu(string.IsNullOrWhiteSpace(r.AnhBia) ? AnhBiaMacDinh : r.AnhBia),
                Level = r.CapDo,
                Rating = Math.Round(r.DiemDanhGia, 1),
                ReviewCount = r.SoDanhGia,
                Price = (long)gia.Gia,
                OriginalPrice = (long)gia.GiaGoc,
                Promotion = !gia.DangGiam ? null : new CoursePromotionDto
                {
                    Name = gia.TenChuongTrinh,
                    DiscountPercent = gia.PhanTram,
                    EndsAt = gia.NgayKetThuc?.ToString("yyyy-MM-dd")
                },
                // Miễn phí theo giá gốc: khóa có phí được giảm về 0đ vẫn đi qua giỏ hàng, không dùng enroll-free
                IsFree = gia.GiaGoc <= 0,
                LessonsCount = r.SoBai,
                DurationSeconds = r.TongGiay,
                StudentsCount = r.SoHocVien,
                Tag = null
            };
        }
    }
}
