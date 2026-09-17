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
        }

        public List<CartItemViewModel> Items { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public string AppliedCouponCode { get; set; }
        public decimal TotalAmount { get; set; }
        public string SelectedPaymentMethod { get; set; }
    }
}
