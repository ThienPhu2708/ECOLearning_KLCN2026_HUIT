using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNet.SignalR;
using ECOLearning.Models;
using ECOLearning.Services;

namespace ECOLearning.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        public static void PhatAnh(ChatService.KetQuaGuiTin result, int sender)
        {
            var clients = GlobalHost.ConnectionManager.GetHubContext<ChatHub>().Clients;
            var m = result.TinNhan;
            clients.Group("hoiThoai_" + m.MaHoiThoai).nhanTinMoi(new {
                maTinNhan = m.MaTinNhan, maHoiThoai = m.MaHoiThoai, maNguoiGui = sender,
                tenNguoiGui = m.TenNguoiGui, anhDaiDien = m.AnhDaiDien, noiDung = "Ảnh",
                imageUrl = m.ImageUrl, ngayGui = m.NgayGui.ToString("HH:mm - dd/MM"), coTheThuHoi = true, recallSeconds = m.RecallSeconds
            });
            clients.Group("user_" + result.MaNguoiNhan).thongBaoTinMoi(new {
                maHoiThoai = m.MaHoiThoai, maNguoiGui = sender, noiDungTomTat = "Đã gửi một ảnh", thoiGian = DateTime.Now.ToString("HH:mm")
            });
        }
        private int LayUserId()
        {
            // Danh tính lấy từ cookie đăng nhập, không tin mã do trình duyệt gửi.
            if (Context.User != null && Context.User.Identity.IsAuthenticated)
            {
                using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                {
                    var email = Context.User.Identity.Name;
                    var user = db.NguoiDung.FirstOrDefault(u => u.email == email);
                    if (user != null && user.trangThai != false) return user.maNguoiDung;
                }
            }

            return 0;
        }

        private bool CoQuyen(int maHoiThoai)
        {
            using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
                return new ChatService(db).LayChiTietHoiThoai(maHoiThoai, LayUserId()) != null;
        }

        public override async Task OnConnected()
        {
            int userId = LayUserId();
            if (userId > 0)
            {
                await Groups.Add(Context.ConnectionId, "user_" + userId);
            }
            await base.OnConnected();
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
            if (!CoQuyen(maHoiThoai)) throw new HubException("Không có quyền truy cập hội thoại.");
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
                    recallSeconds = kq.TinNhan.RecallSeconds,
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
            if (!CoQuyen(maHoiThoai)) return;
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
            if (!CoQuyen(maHoiThoai)) return;
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
            if (!CoQuyen(maHoiThoai)) return;
            int userId = LayUserId();
            if (userId <= 0) return;

            using (var db = new QL_HETHONG_CUNGCAP_KHOAHOCEntities1())
            {
                var service = new ChatService(db);
                bool ok = service.ThuHoiTinNhan(maTinNhan, userId, maHoiThoai);
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

