using System;
using System.Collections.Generic;

namespace ECOLearning.ViewModels
{
    public class CartViewModel
    {
        public CartViewModel()
        {
            Items = new List<CartItemViewModel>();
            SelectedPaymentMethod = "MoMo/VNPay";
            AvailableCoupons = new List<MaGiamGiaHienThi>();
        }

        /// <summary>Mã trong ví "Mã giảm giá của tôi" (dùng được hoặc chưa đủ điều kiện, kèm lý do) cho giỏ hiện tại.</summary>
        public List<MaGiamGiaHienThi> AvailableCoupons { get; set; }

        /// <summary>Đã đăng nhập (khách chưa có ví mã, chỉ nhập tay được).</summary>
        public bool IsLoggedIn { get; set; }

        /// <summary>Ưu đãi thân thiết học viên chưa đạt tiêu chí (hiện tiến độ, không lộ mã).</summary>
        public List<UuDaiThanThietSapMo> ThanThietSapMo { get; set; } = new List<UuDaiThanThietSapMo>();

        public List<CartItemViewModel> Items { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public string AppliedCouponCode { get; set; }
        public decimal TotalAmount { get; set; }
        public string SelectedPaymentMethod { get; set; }
    }
}
