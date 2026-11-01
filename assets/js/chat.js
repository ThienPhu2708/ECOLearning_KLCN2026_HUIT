/**
 * ECOLearning - Real-time Chat Engine (SignalR 2 Client)
 * Quản lý gửi/nhận tin nhắn, typing, đã xem, thu hồi và cập nhật UI.
 */
(function (window, $) {
    'use strict';

    window.EcoChat = {
        hub: null,
        currentConvId: 0,
        currentUserId: 0,
        isTeacher: false,
        typingTimer: null,

        init: function (userId, isTeacher) {
            var self = this;
            self.currentUserId = userId;
            self.isTeacher = !!isTeacher;

            if (!$.connection || !$.connection.chatHub) {
                console.warn('SignalR chatHub chưa sẵn sàng.');
                return;
            }

            self.hub = $.connection.chatHub;
            $.connection.hub.qs = { userId: userId };

            // 1. Nhận tin nhắn mới
            self.hub.client.nhanTinMoi = function (data) {
                if (data.maHoiThoai === self.currentConvId) {
                    self.appendMessage(data);
                    self.scrollToBottom();
                    self.hub.server.danhDauDaDoc(self.currentConvId);
                }

                // Cập nhật danh sách hội thoại bên sidebar
                self.updateConversationItem(data.maHoiThoai, data.noiDung, data.ngayGui);
            };

            // 2. Nhận thông báo đang soạn tin
            self.hub.client.nguoiDungDangSoan = function (data) {
                if (data.maHoiThoai === self.currentConvId) {
                    var el = $('#chat-typing-indicator');
                    if (data.isTyping) {
                        el.show();
                    } else {
                        el.hide();
                    }
                }
            };

            // 3. Tin nhắn đã thu hồi
            self.hub.client.tinNhanDaThuHoi = function (data) {
                if (data.maHoiThoai === self.currentConvId) {
                    var row = $('#msg-row-' + data.maTinNhan);
                    if (row.length) {
                        row.find('.chat-msg-bubble').addClass('chat-msg-recalled').text('Tin nhắn đã được thu hồi');
                        row.find('.chat-recall-btn').remove();
                    }
                }
            };

            // 4. Lỗi gửi tin (vd: giáo viên nhắn trước hoặc vi phạm rate limit)
            self.hub.client.loiGuiTin = function (msg) {
                if (window.Swal) {
                    Swal.fire({
                        icon: 'warning',
                        title: 'Thông báo',
                        text: msg,
                        confirmButtonColor: '#0D9488'
                    });
                } else {
                    alert(msg);
                }
            };

            // 5. Thông báo có tin mới từ cuộc hội thoại khác
            self.hub.client.thongBaoTinMoi = function (data) {
                self.updateBadge();
                self.updateConversationItem(data.maHoiThoai, data.noiDungTomTat, data.thoiGian);
            };

            // Khởi động kết nối SignalR
            $.connection.hub.start().done(function () {
                console.log('SignalR connected successfully.');
                if (self.currentConvId > 0) {
                    self.hub.server.thamGiaHoiThoai(self.currentConvId);
                }
            }).fail(function (err) {
                console.error('Không thể kết nối SignalR: ', err);
            });

            // Gắn sự kiện giao diện
            self.bindEvents();
            self.updateBadge();
        },

        joinConversation: function (convId) {
            var self = this;
            if (self.currentConvId > 0 && self.hub && $.connection.hub.state === 1) {
                self.hub.server.roiHoiThoai(self.currentConvId);
            }

            self.currentConvId = convId;
            if (self.hub && $.connection.hub.state === 1) {
                self.hub.server.thamGiaHoiThoai(convId);
                self.hub.server.danhDauDaDoc(convId);
            }
            self.scrollToBottom();
        },

        sendMessage: function () {
            var self = this;
            var input = $('#chat-message-input');
            var text = input.val().trim();
            if (!text || self.currentConvId <= 0) return;

            // Kiểm tra cảnh báo số điện thoại / STK phía client
            var phoneRegex = /(0|\+84)[3|5|7|8|9][0-9]{8}\b|(\d{3,4}[\.\s]\d{3,4}[\.\s]\d{3,4})/;
            var bankRegex = /(stk|so\s*tai\s*khoan|tai\s*khoan|ngan\s*hang|momo|zalopay)\s*[:=\s]*\d{6,16}/i;

            if (phoneRegex.test(text) || bankRegex.test(text)) {
                if (window.Swal) {
                    Swal.fire({
                        title: 'Lưu ý giao dịch an toàn',
                        text: 'Điều khoản ECOLearning nghiêm cấm các hành vi mời chào hoặc thanh toán ngoài nền tảng để bảo vệ quyền lợi học viên.',
                        icon: 'info',
                        showCancelButton: true,
                        confirmButtonText: 'Tiếp tục gửi',
                        cancelButtonText: 'Hủy bỏ',
                        confirmButtonColor: '#0D9488'
                    }).then(function (result) {
                        if (result.isConfirmed) {
                            self.executeSend(text);
                            input.val('');
                        }
                    });
                    return;
                }
            }

            self.executeSend(text);
            input.val('');
            self.sendTyping(false);
        },

        executeSend: function (text) {
            var self = this;
            if (self.hub && $.connection.hub.state === 1) {
                self.hub.server.guiTin(self.currentConvId, text);
            } else {
                alert('Mất kết nối tới máy chủ chat. Vui lòng tải lại trang.');
            }
        },

        recallMessage: function (msgId) {
            var self = this;
            if (confirm('Bạn có chắc muốn thu hồi tin nhắn này? (Chỉ có thể thu hồi trong vòng 5 phút)')) {
                if (self.hub && $.connection.hub.state === 1) {
                    self.hub.server.thuHoiTin(msgId, self.currentConvId);
                }
            }
        },

        sendTyping: function (isTyping) {
            var self = this;
            if (self.hub && $.connection.hub.state === 1 && self.currentConvId > 0) {
                self.hub.server.dangSoanTin(self.currentConvId, isTyping);
            }
        },

        appendMessage: function (data) {
            var self = this;
            var isMine = data.maNguoiGui === self.currentUserId;
            var rowClass = isMine ? 'chat-msg-row msg-mine' : 'chat-msg-row msg-other';

            var recallHtml = isMine ? '<button type="button" class="chat-recall-btn" onclick="EcoChat.recallMessage(' + data.maTinNhan + ')" title="Thu hồi tin"><i class="bi bi-arrow-counterclockwise"></i></button>' : '';

            var html = '<div class="' + rowClass + '" id="msg-row-' + data.maTinNhan + '">'
                     + '  <img src="' + (data.anhDaiDien || '/assets/images/default-avatar.png') + '" class="chat-msg-avatar" alt="' + data.tenNguoiGui + '" />'
                     + '  <div>'
                     + '    <div class="chat-msg-bubble">' + self.escapeHtml(data.noiDung) + '</div>'
                     + '    <div class="chat-msg-time">' + data.ngayGui + ' ' + recallHtml + '</div>'
                     + '  </div>'
                     + '</div>';

            $('#chat-messages-container').append(html);
        },

        scrollToBottom: function () {
            var el = document.getElementById('chat-messages-container');
            if (el) {
                el.scrollTop = el.scrollHeight;
            }
        },

        updateConversationItem: function (convId, lastMsg, time) {
            var item = $('#conv-item-' + convId);
            if (item.length) {
                item.find('.chat-conv-last-msg').text(lastMsg);
                if (time) item.find('.chat-conv-time').text(time);
            }
        },

        updateBadge: function () {
            $.get('/Chat/UnreadCount', function (res) {
                var badge = $('.js-chat-unread-badge');
                if (res.count > 0) {
                    badge.text(res.count).show();
                } else {
                    badge.hide();
                }
            });
        },

        bindEvents: function () {
            var self = this;

            $('#chat-send-btn').on('click', function () {
                self.sendMessage();
            });

            $('#chat-message-input').on('keydown', function (e) {
                if (e.key === 'Enter' && !e.shiftKey) {
                    e.preventDefault();
                    self.sendMessage();
                } else {
                    self.sendTyping(true);
                    clearTimeout(self.typingTimer);
                    self.typingTimer = setTimeout(function () {
                        self.sendTyping(false);
                    }, 2000);
                }
            });
        },

        escapeHtml: function (string) {
            var entityMap = {
                '&': '&amp;',
                '<': '&lt;',
                '>': '&gt;',
                '"': '&quot;',
                "'": '&#39;'
            };
            return String(string).replace(/[&<>"']/g, function (s) {
                return entityMap[s];
            });
        }
    };
})(window, jQuery);

