using System.Web;
using System.Web.Mvc;

namespace ECOLearning
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            // Sau HandleError để chạy trước nó: ghi lỗi ra App_Data/logs rồi để HandleError hiện trang lỗi như cũ
            filters.Add(new GhiLogLoiAttribute());
            // Đăng xuất tài khoản bị khóa ngay, buộc đổi mật khẩu tạm do Admin cấp
            filters.Add(new AccountStatusFilter());
        }
    }
}
