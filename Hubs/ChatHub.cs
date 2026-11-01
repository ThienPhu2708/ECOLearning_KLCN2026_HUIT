using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Hubs
{
    public class ChatHub : Hub
    {
        private int LayUserId()
        {
            // Lấy từ query string khi client kết nối: $.connection.hub.qs = { userId: ... }
            string qsUser = Context.QueryString["userId"];
            if (!string.IsNullOrEmpty(qsUser) && int.TryParse(qsUser, out int uid))
            {
                return uid;
            }

            // Fallback: Nếu môi trường có FormAuthentication / User Identity
            if (Context.User != null && Context.User.Identity.IsAuthenticated)
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    var user = db.NguoiDung.Find(int.TryParse(Context.User.Identity.Name, out int id) ? id : 0);
                    if (user != null) return user.maNguoiDung;
                }
            }

            return 0;
        }

        public override Task OnConnected()
        {
            int userId = LayUserId();
            if (userId > 0)
            {
                Groups.Add(Context.ConnectionId, "user_" + userId);
            }
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            int userId = LayUserId();
            if (userId > 0)
            {
                Groups.Remove(Context.ConnectionId, "user_" + userId);
            }
            return base.OnDisconnected(stopCalled);
        }

        public async Task ThamGiaHoiThoai(int maHoiThoai)
        {
            await Groups.Add(Context.ConnectionId, "hoiThoai_" + maHoiThoai);
        }

        public async Task RoiHoiThoai(int maHoiThoai)
        {
            await Groups.Remove(Context.ConnectionId, "hoiThoai_" + maHoiThoai);
        }

        public void GuiTin(int maHoiThoai, string noiDung)
        {
            int userId = LayUserId();
            if (userId <= 0)
            {
                Clients.Caller.loiGuiTin("Vui lòng đăng nhập để gửi tin nhắn.");
                return;
            }

            using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
            {
                var service = new ChatService(db);
                var kq = service.GuiTinNhan(maHoiThoai, userId, noiDung);

                if (!kq.Success)
                {
                    Clients.Caller.loiGuiTin(kq.Message);
                    return;
                }

                // Gửi tin nhắn mới đến tất cả người đang mở phòng chat này
                Clients.Group("hoiThoai_" + maHoiThoai).nhanTinMoi(new
                {
                    maTinNhan = kq.TinNhan.MaTinNhan,
                    maHoiThoai = kq.TinNhan.MaHoiThoai,
                    maNguoiGui = kq.TinNhan.MaNguoiGui,
                    tenNguoiGui = kq.TinNhan.TenNguoiGui,
                    anhDaiDien = kq.TinNhan.AnhDaiDien,
                    noiDung = kq.TinNhan.NoiDung,
                    ngayGui = kq.TinNhan.NgayGui.ToString("HH:mm - dd/MM"),
                    canhBao = kq.CanhBaoThanhToanNgoai
                });

                // Thông báo cập nhật danh sách hội thoại cho cả hai bên
                Clients.Group("user_" + kq.MaNguoiNhan).thongBaoTinMoi(new
                {
                    maHoiThoai = maHoiThoai,
                    maNguoiGui = userId,
                    noiDungTomTat = kq.TinNhan.NoiDung.Length > 60 ? kq.TinNhan.NoiDung.Substring(0, 57) + "..." : kq.TinNhan.NoiDung,
                    thoiGian = DateTime.Now.ToString("HH:mm")
                });
            }
        }

        public void DangSoanTin(int maHoiThoai, bool isTyping)
        {
            int userId = LayUserId();
            if (userId <= 0) return;

            Clients.OthersInGroup("hoiThoai_" + maHoiThoai).nguoiDungDangSoan(new
            {
                maHoiThoai = maHoiThoai,
                userId = userId,
                isTyping = isTyping
            });
        }

        public void DanhDauDaDoc(int maHoiThoai)
        {
            int userId = LayUserId();
            if (userId <= 0) return;

            using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
            {
                var service = new ChatService(db);
                service.DanhDauDaDoc(maHoiThoai, userId);

                Clients.OthersInGroup("hoiThoai_" + maHoiThoai).daXemTin(new
                {
                    maHoiThoai = maHoiThoai,
                    userId = userId
                });
            }
        }

        public void ThuHoiTin(int maTinNhan, int maHoiThoai)
        {
            int userId = LayUserId();
            if (userId <= 0) return;

            using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
            {
                var service = new ChatService(db);
                bool ok = service.ThuHoiTinNhan(maTinNhan, userId);
                if (ok)
                {
                    Clients.Group("hoiThoai_" + maHoiThoai).tinNhanDaThuHoi(new
                    {
                        maTinNhan = maTinNhan,
                        maHoiThoai = maHoiThoai
                    });
                }
                else
                {
                    Clients.Caller.loiGuiTin("Không thể thu hồi tin nhắn (chỉ được thu hồi trong 5 phút đầu).");
                }
            }
        }
    }
}

