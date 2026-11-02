/**
 * ECOLearning - Real-time Chat Engine (SignalR 2 Client)
 * Quản lý gửi/nhận tin nhắn, typing, đã xem, thu hồi và cập nhật UI.
 *
 * Dùng chung cho nhiều giao diện: truyền thêm tham số `selectors` vào init()
 * khi khung chat dùng id khác mặc định (ví dụ panel "Hỏi giáo viên" ở trang Study).
 */
(function (window, $) {
    'use strict';

    var DEFAULT_SELECTORS = {
        messages: '#chat-messages-container',
        input: '#chat-message-input',
        send: '#chat-send-btn',
        typing: '#chat-typing-indicator'
    };

    window.EcoChat = {
        hub: null,
        currentConvId: 0,
        currentUserId: 0,
        isTeacher: false,
        initialized: false,
        isTyping: false,
        typingTimer: null,
        typingHideTimer: null,
        reconnectTimer: null,
        lastSentText: '',
        selectors: $.extend({}, DEFAULT_SELECTORS),

        // Trang nào có khung chat ẩn/hiện thì ghi đè hàm này.
        // Mặc định luôn coi là đang hiển thị (giữ nguyên hành vi cũ ở trang tin nhắn).
        isActive: function () { return true; },

        // Tùy chọn: được gọi khi có tin của người khác đến lúc khung chat đang ẩn.
        onIncomingWhileHidden: null,

        // Trả về true nếu khởi tạo được, false nếu SignalR chưa sẵn sàng.
        init: function (userId, isTeacher, selectors) {
            var self = this;
            if (self.initialized) return true;

            self.currentUserId = userId;
            self.isTeacher = !!isTeacher;
            self.selectors = $.extend({}, DEFAULT_SELECTORS, selectors || {});

            if (!$.connection || !$.connection.chatHub) {
                console.warn('SignalR chatHub chưa sẵn sàng (kiểm tra /signalr/hubs).');
                return false;
            }

            self.hub = $.connection.chatHub;
            $.connection.hub.qs = { userId: userId };

            // Phải đăng ký handler TRƯỚC khi start()
            self.registerClientHandlers();

            // Mỗi lần kết nối (lần đầu hoặc nối lại) đều vào lại nhóm hội thoại
            $.connection.hub.stateChanged(function (change) {
                if (change.newState === $.signalR.connectionState.connected) {
                    self.rejoin();
                }
            });
            // Mất kết nối hẳn thì tự thử nối lại
            $.connection.hub.disconnected(function () {
                self.scheduleReconnect();
            });

            self.bindEvents();
            self.updateBadge();
            self.startHub();

            self.initialized = true;
            return true;
        },

        registerClientHandlers: function () {
            var self = this;

            // 1. Nhận tin nhắn mới
            self.hub.client.nhanTinMoi = function (data) {
                if (Number(data.maHoiThoai) === Number(self.currentConvId)) {
                    self.appendMessage(data);
                    self.scrollToBottom();

                    var fromMe = Number(data.maNguoiGui) === Number(self.currentUserId);
                    if (!fromMe) {
                        if (self.isActive()) {
                            self.markRead();
                        } else if (typeof self.onIncomingWhileHidden === 'function') {
                            // Khung chat đang đóng: KHÔNG đánh dấu đã đọc để người dùng còn thấy tin mới
                            self.onIncomingWhileHidden(data);
                        }
                    }
                }

                // Cập nhật danh sách hội thoại bên sidebar (nếu trang có)
                self.updateConversationItem(data.maHoiThoai, data.noiDung, data.ngayGui);
            };

            // 2. Nhận thông báo đang soạn tin
            self.hub.client.nguoiDungDangSoan = function (data) {
                if (Number(data.maHoiThoai) !== Number(self.currentConvId)) return;
                var el = $(self.selectors.typing);
                clearTimeout(self.typingHideTimer);
                if (data.isTyping) {
                    el.show();
                    // Phòng khi mất sự kiện "ngừng soạn": tự ẩn sau 4 giây
                    self.typingHideTimer = setTimeout(function () { el.hide(); }, 4000);
                } else {
                    el.hide();
                }
            };

            // 3. Tin nhắn đã thu hồi
            self.hub.client.tinNhanDaThuHoi = function (data) {
                if (Number(data.maHoiThoai) === Number(self.currentConvId)) {
                    var row = $('#msg-row-' + data.maTinNhan);
                    if (row.length) {
                        row.find('.chat-msg-bubble').addClass('chat-msg-recalled').text('Tin nhắn đã được thu hồi');
                        row.find('.chat-recall-btn').remove();
                    }
                }
            };

            // 4. Lỗi gửi tin (vd: giáo viên nhắn trước hoặc vi phạm rate limit)
            self.hub.client.loiGuiTin = function (msg) {
                // Trả lại nội dung đã gõ để người dùng sửa/gửi lại
                var input = $(self.selectors.input);
                if (self.lastSentText && !input.val()) input.val(self.lastSentText);
                self.notify(msg, 'warning');
            };

            // 5. Thông báo có tin mới từ cuộc hội thoại khác
            self.hub.client.thongBaoTinMoi = function (data) {
                self.updateBadge();
                self.updateConversationItem(data.maHoiThoai, data.noiDungTomTat, data.thoiGian);
            };
        },

        startHub: function () {
            var self = this;
            if ($.connection.hub.state !== $.signalR.connectionState.disconnected) return;
            $.connection.hub.start()
                .done(function () { console.log('SignalR connected successfully.'); })
                .fail(function (err) {
                    console.error('Không thể kết nối SignalR: ', err);
                    self.scheduleReconnect();
                });
        },

        scheduleReconnect: function () {
            var self = this;
            clearTimeout(self.reconnectTimer);
            self.reconnectTimer = setTimeout(function () { self.startHub(); }, 5000);
        },

        isConnected: function () {
            return !!(this.hub && $.connection.hub.state === $.signalR.connectionState.connected);
        },

        // Vào lại nhóm hội thoại hiện tại (không làm gì nếu chưa kết nối hoặc chưa có hội thoại)
        rejoin: function () {
            if (this.currentConvId > 0 && this.isConnected()) {
                this.hub.server.thamGiaHoiThoai(this.currentConvId);
            }
        },

        markRead: function () {
            if (this.currentConvId > 0 && this.isConnected()) {
                this.hub.server.danhDauDaDoc(this.currentConvId);
            }
        },

        joinConversation: function (convId) {
            var self = this;
            if (self.currentConvId > 0 && self.isConnected()) {
                self.hub.server.roiHoiThoai(self.currentConvId);
            }

            self.currentConvId = convId;
            if (self.isConnected()) {
                self.hub.server.thamGiaHoiThoai(convId);
                self.hub.server.danhDauDaDoc(convId);
            }
            self.scrollToBottom();
        },

        // Thông báo cho người dùng: ưu tiên Swal, rồi toast của trang, cuối cùng là alert
        notify: function (msg, type) {
            if (window.Swal) {
                Swal.fire({
                    icon: type === 'error' ? 'error' : 'warning',
                    title: 'Thông báo',
                    text: msg,
                    confirmButtonColor: '#0D9488'
                });
            } else if (typeof window.showToast === 'function') {
                window.showToast(msg, type || 'warning');
            } else {
                alert(msg);
            }
        },

        // Phát hiện số điện thoại / STK (bỏ dấu cách, dấu chấm giữa các chữ số và dấu tiếng Việt trước khi kiểm tra)
        looksLikeOffPlatformDeal: function (text) {
            var compact = text.replace(/(\d)[\s.\-](?=\d)/g, '$1');
            var plain = compact;
            if (plain.normalize) {
                plain = plain.normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D');
            }
            var phoneRegex = /(?:^|\D)(?:0|\+84)[35789]\d{8}(?!\d)/;
            var bankRegex = /(stk|so\s*tai\s*khoan|tai\s*khoan|ngan\s*hang|momo|zalopay)\s*[:=\s]*\d{6,16}/i;
            return phoneRegex.test(compact) || bankRegex.test(plain);
        },

        sendMessage: function () {
            var self = this;
            var input = $(self.selectors.input);
            var text = $.trim(input.val());
            if (!text || self.currentConvId <= 0) return;

            if (self.looksLikeOffPlatformDeal(text) && window.Swal) {
                Swal.fire({
                    title: 'Lưu ý giao dịch an toàn',
                    text: 'Điều khoản ECOLearning nghiêm cấm các hành vi mời chào hoặc thanh toán ngoài nền tảng để bảo vệ quyền lợi học viên.',
                    icon: 'info',
                    showCancelButton: true,
                    confirmButtonText: 'Tiếp tục gửi',
                    cancelButtonText: 'Hủy bỏ',
                    confirmButtonColor: '#0D9488'
                }).then(function (result) {
                    if (result.isConfirmed) self.submit(text, input);
                });
                return;
            }

            self.submit(text, input);
        },

        // Gửi đi; chỉ xóa ô nhập khi thật sự gửi được, nếu lỗi thì trả lại nội dung
        submit: function (text, input) {
            var self = this;
            var request = self.executeSend(text);
            if (!request) return;   // chưa kết nối: giữ nguyên nội dung ô nhập

            input.val('');
            if (input[0] && input[0].tagName === 'TEXTAREA') input.css('height', 'auto');
            clearTimeout(self.typingTimer);
            self.isTyping = false;
            self.sendTyping(false);

            if (request.fail) {
                request.fail(function () {
                    if (!input.val()) input.val(text);
                    self.notify('Gửi tin nhắn thất bại, hãy thử lại.', 'error');
                });
            }
        },

        // Trả về promise của hub (hoặc null nếu chưa kết nối)
        executeSend: function (text) {
            var self = this;
            if (!self.isConnected()) {
                self.notify('Mất kết nối tới máy chủ chat. Hệ thống đang kết nối lại, nội dung của bạn vẫn được giữ nguyên.', 'warning');
                return null;
            }
            self.lastSentText = text;
            return self.hub.server.guiTin(self.currentConvId, text);
        },

        recallMessage: function (msgId) {
            var self = this;
            if (confirm('Bạn có chắc muốn thu hồi tin nhắn này? (Chỉ có thể thu hồi trong vòng 5 phút)')) {
                if (self.isConnected()) {
                    self.hub.server.thuHoiTin(msgId, self.currentConvId);
                } else {
                    self.notify('Mất kết nối tới máy chủ chat, chưa thể thu hồi tin nhắn.', 'warning');
                }
            }
        },

        sendTyping: function (isTyping) {
            var self = this;
            if (self.isConnected() && self.currentConvId > 0) {
                self.hub.server.dangSoanTin(self.currentConvId, isTyping);
            }
        },

        // Chỉ gửi "đang soạn" một lần khi bắt đầu gõ, rồi gửi "ngừng" sau 2 giây không gõ
        notifyTyping: function () {
            var self = this;
            if (!self.isTyping) {
                self.isTyping = true;
                self.sendTyping(true);
            }
            clearTimeout(self.typingTimer);
            self.typingTimer = setTimeout(function () {
                self.isTyping = false;
                self.sendTyping(false);
            }, 2000);
        },

        appendMessage: function (data) {
            var self = this;

            // Chống hiển thị trùng (tin vừa có trong lịch sử vừa đến qua SignalR)
            var id = parseInt(data.maTinNhan, 10) || 0;
            if (id && $('#msg-row-' + id).length) return;

            var $box = $(self.selectors.messages);
            $box.find('.eco-chat-state').remove();   // bỏ placeholder "chưa có tin nhắn"

            var isMine = Number(data.maNguoiGui) === Number(self.currentUserId);
            var recalled = data.daThuHoi === true;
            var canRecall = isMine && !recalled && data.coTheThuHoi !== false;
            var rowClass = 'chat-msg-row ' + (isMine ? 'msg-mine' : 'msg-other');
            var avatar = self.escapeHtml(data.anhDaiDien || '/assets/images/default-avatar.png');
            var name = self.escapeHtml(data.tenNguoiGui || '');
            var bubbleClass = 'chat-msg-bubble' + (recalled ? ' chat-msg-recalled' : '');
            var bubbleText = recalled ? 'Tin nhắn đã được thu hồi' : self.escapeHtml(data.noiDung);

            var recallHtml = canRecall
                ? '<button type="button" class="chat-recall-btn" onclick="EcoChat.recallMessage(' + id + ')" title="Thu hồi tin"><i class="bi bi-arrow-counterclockwise"></i></button>'
                : '';

            var html = '<div class="' + rowClass + '" id="msg-row-' + id + '">'
                + '  <img src="' + avatar + '" class="chat-msg-avatar" alt="' + name + '" />'
                + '  <div>'
                + '    <div class="' + bubbleClass + '">' + bubbleText + '</div>'
                + '    <div class="chat-msg-time">' + self.escapeHtml(data.ngayGui || '') + ' ' + recallHtml + '</div>'
                + '  </div>'
                + '</div>';

            $box.append(html);
        },

        scrollToBottom: function () {
            var el = $(this.selectors.messages)[0];
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
            $.ajax({ url: '/Chat/UnreadCount', type: 'GET', dataType: 'json', cache: false })
                .done(function (res) {
                    var badge = $('.js-chat-unread-badge');
                    if (res && res.count > 0) {
                        badge.text(res.count).show();
                    } else {
                        badge.hide();
                    }
                });
        },

        // Dùng namespace .ecochat để gọi lại nhiều lần không bị gắn trùng sự kiện
        bindEvents: function () {
            var self = this;

            $(self.selectors.send).off('.ecochat').on('click.ecochat', function () {
                self.sendMessage();
            });

            $(self.selectors.input).off('.ecochat')
                .on('keydown.ecochat', function (e) {
                    // Đang gõ dấu tiếng Việt (IME) thì Enter chỉ để chốt chữ, không gửi
                    var composing = (e.originalEvent && e.originalEvent.isComposing) || e.keyCode === 229;
                    if (e.which === 13 && !e.shiftKey && !composing) {
                        e.preventDefault();
                        self.sendMessage();
                        return;
                    }
                    self.notifyTyping();
                })
                .on('input.ecochat', function () {
                    // Ô nhập tự giãn theo nội dung (tối đa 120px)
                    if (this.tagName === 'TEXTAREA') {
                        this.style.height = 'auto';
                        this.style.height = Math.min(this.scrollHeight, 120) + 'px';
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
            return String(string === undefined || string === null ? '' : string).replace(/[&<>"']/g, function (s) {
                return entityMap[s];
            });
        }
    };
})(window, jQuery);