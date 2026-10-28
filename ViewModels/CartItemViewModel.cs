using System;

namespace ECOLearning.ViewModels
{
    public class CartItemViewModel
    {
        public string CourseId { get; set; }
        public string CourseName { get; set; }
        public string InstructorName { get; set; }
        public string DurationText { get; set; }

        /// <summary>Giá thanh toán của khóa (đã trừ giảm trực tiếp, chưa trừ coupon).</summary>
        public decimal Price { get; set; }

        /// <summary>Giá gốc trong CSDL; lớn hơn Price khi khóa đang được giảm trực tiếp.</summary>
        public decimal OriginalPrice { get; set; }

        /// <summary>Chương trình giảm trực tiếp đang áp dụng (null = không giảm).</summary>
        public string MaKhuyenMaiTrucTiep { get; set; }
        public string ThumbnailUrl { get; set; }
    }
}
