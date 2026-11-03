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
            self.groupMessages();
            self.expireRecallButtons($(self.selectors.messages));
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
                        if (self.isActive() && !document.hidden) {
                            self.markRead();
                        } else if (typeof self.onIncomingWhileHidden === 'function') {
                            // Khung chat đang đóng: KHÔNG đánh dấu đã đọc để người dùng còn thấy tin mới
                            self.onIncomingWhileHidden(data);
                        }
                    }
                }

                // Cập nhật danh sách hội thoại bên sidebar (nếu trang có)
                self.updateConversationItem(data.maHoiThoai, data.noiDung, data.ngayGui, data.maNguoiGui);
                if (Number(data.maNguoiGui) !== Number(self.currentUserId)) self.setConversationUnread(data.maHoiThoai, Number(data.maHoiThoai) !== Number(self.currentConvId) || !self.isActive() || document.hidden);
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
                self.updateConversationItem(data.maHoiThoai, data.noiDungTomTat, data.thoiGian, data.maNguoiGui);
                self.setConversationUnread(data.maHoiThoai, Number(data.maHoiThoai) !== Number(self.currentConvId) || !self.isActive() || document.hidden);
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
            var self = this;
            if (this.currentConvId > 0 && this.isConnected()) {
                return this.hub.server.danhDauDaDoc(this.currentConvId).done(function () {
                    self.setConversationUnread(self.currentConvId, false);
                    if (typeof self.onRead === 'function') self.onRead();
                });
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
            if (document.getElementById('chat-recall-dialog')) return;
            var convId = self.currentConvId;
            var previousFocus = document.activeElement;
            var dialog = document.createElement('dialog');
            dialog.id = 'chat-recall-dialog';
            dialog.className = 'chat-confirm-dialog';
            dialog.setAttribute('aria-labelledby', 'chat-recall-title');
            dialog.setAttribute('aria-describedby', 'chat-recall-description');
            dialog.innerHTML = '<div class="chat-confirm-icon"><i class="bi bi-arrow-counterclockwise"></i></div>'
                + '<h3 id="chat-recall-title">Thu hồi tin nhắn?</h3>'
                + '<p id="chat-recall-description">Tin nhắn sẽ được thu hồi với mọi người trong cuộc trò chuyện. Chỉ áp dụng trong 5 phút sau khi gửi.</p>'
                + '<div class="chat-confirm-actions"><button type="button" class="chat-confirm-cancel" autofocus>Giữ lại</button><button type="button" class="chat-confirm-accept">Thu hồi</button></div>';
            document.body.appendChild(dialog);
            dialog.addEventListener('close', function () {
                dialog.remove();
                if (previousFocus && previousFocus.isConnected) previousFocus.focus();
            });
            dialog.querySelector('.chat-confirm-cancel').onclick = function () { dialog.close(); };
            dialog.querySelector('.chat-confirm-accept').onclick = function () {
                dialog.close();
                if (self.isConnected()) {
                    self.hub.server.thuHoiTin(msgId, convId).fail(function () {
                        self.notify('Chưa thu hồi được tin nhắn. Vui lòng thử lại.', 'error');
                    });
                } else {
                    self.notify('Mất kết nối tới máy chủ chat, chưa thể thu hồi tin nhắn.', 'warning');
                }
            };
            dialog.showModal();
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
            var recallSeconds = data.recallSeconds == null ? 300 : Math.max(0, Number(data.recallSeconds) || 0);
            var canRecall = isMine && !recalled && data.coTheThuHoi !== false && recallSeconds > 0;
            var rowClass = 'chat-msg-row ' + (isMine ? 'msg-mine' : 'msg-other');
            var avatar = self.escapeHtml(data.anhDaiDien || '/assets/images/default-avatar.png');
            var name = self.escapeHtml(data.tenNguoiGui || '');
            var bubbleClass = 'chat-msg-bubble' + (recalled ? ' chat-msg-recalled' : '');
            var bubbleText = recalled ? 'Tin nhắn đã được thu hồi' : self.escapeHtml(data.noiDung);
            var imageUrl = !recalled && /^\/Chat\/Image\/\d+$/.test(data.imageUrl || '') ? data.imageUrl : null;
            if (imageUrl) bubbleText = '<button type="button" class="chat-image-open" data-image-url="' + imageUrl + '"><img class="chat-image" src="' + imageUrl + '" alt="Ảnh trong cuộc trò chuyện" /></button>';

            var recallHtml = canRecall
                ? '<button type="button" class="chat-recall-btn" data-recall-seconds="' + recallSeconds + '" onclick="EcoChat.recallMessage(' + id + ')" title="Thu hồi tin trong 5 phút" aria-label="Thu hồi tin nhắn"><i class="bi bi-arrow-counterclockwise"></i></button>'
                : '';

            var sentAt = String(data.ngayGui || '');
            var shortTime = (sentAt.match(/\d{2}:\d{2}/) || [sentAt])[0];
            var html = '<div class="' + rowClass + '" id="msg-row-' + id + '" data-sender="' + Number(data.maNguoiGui) + '" tabindex="0">'
                + '  <img src="' + avatar + '" class="chat-msg-avatar" alt="' + name + '" />'
                + '  <div>'
                + '    <div class="' + bubbleClass + '" title="' + self.escapeHtml(sentAt) + '">' + bubbleText + '</div>'
                + '    <div class="chat-msg-time">' + self.escapeHtml(shortTime) + ' ' + recallHtml + '</div>'
                + '  </div>'
                + '</div>';

            var row = $(html);
            row.find('.chat-msg-avatar').on('error', function () {
                $(this).replaceWith($('<span class="chat-msg-avatar chat-avatar-fallback" aria-hidden="true">').text((data.tenNguoiGui || '?').trim().charAt(0).toUpperCase()));
            });
            $box.append(row);
            self.expireRecallButtons(row);
            row.find('.chat-image').on('load', function () { self.scrollToBottom(); });
            self.groupMessages();
        },

        expireRecallButtons: function (scope) {
            scope.find('.chat-recall-btn[data-recall-seconds]').each(function () {
                var button = this, seconds = Number(button.getAttribute('data-recall-seconds')) || 0;
                button.removeAttribute('data-recall-seconds');
                if (seconds <= 0) button.remove();
                else setTimeout(function () { button.remove(); }, seconds * 1000);
            });
        },

        groupMessages: function () {
            var rows = $(this.selectors.messages).children('.chat-msg-row');
            rows.removeClass('chat-group-start chat-group-continues');
            rows.each(function (index) {
                var sender = this.getAttribute('data-sender');
                if (index === 0 || !sender || sender !== rows[index - 1].getAttribute('data-sender')) $(this).addClass('chat-group-start');
                if (sender && index + 1 < rows.length && sender === rows[index + 1].getAttribute('data-sender')) $(this).addClass('chat-group-continues');
            });
        },

        scrollToBottom: function () {
            var el = $(this.selectors.messages)[0];
            if (el) {
                el.scrollTop = el.scrollHeight;
            }
        },

        updateConversationItem: function (convId, lastMsg, time, sender) {
            var item = $('#conv-item-' + convId);
            if (item.length) {
                item.find('.chat-conv-last-msg').text((Number(sender) === Number(this.currentUserId) ? 'Bạn: ' : '') + (lastMsg || ''));
                if (time) item.find('.chat-conv-time').text(time);
            }
        },

        setConversationUnread: function (convId, unread) {
            var item = $('#conv-item-' + convId);
            item.toggleClass('is-unread', unread);
            item.find('.inbox-unread-dot').prop('hidden', !unread);
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
            self.bindMedia();

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
                        this.style.overflowY = this.scrollHeight > 120 ? 'auto' : 'hidden';
                    }
                });
        },

        bindMedia: function () {
            var self = this, input = $(self.selectors.input), footer = input.closest('.chat-footer');
            if (!input.length || footer.find('.chat-compose-tools').length) return;
            var tools = $('<div class="chat-compose-tools"><button type="button" class="chat-tool chat-emoji-toggle" aria-label="Chọn emoji" aria-expanded="false"><i class="bi bi-emoji-smile"></i></button><button type="button" class="chat-tool chat-attach" aria-label="Gửi ảnh" title="Gửi ảnh JPG/PNG, tối đa 5 MB"><i class="bi bi-image"></i></button><input class="chat-file" type="file" accept="image/jpeg,image/png" hidden /><div class="chat-emoji-picker" hidden role="group" aria-label="Chọn emoji"></div></div>');
            footer.prepend(tools);
            var picker = tools.find('.chat-emoji-picker'), toggle = tools.find('.chat-emoji-toggle');
            '😊 😄 😂 🥰 😍 😎 🤔 😢 😮 🙏 👍 👏 ❤️ 💜 🎉 ✅ 💡 📚 🔥 💯'.split(' ').forEach(function (emoji) {
                $('<button type="button" class="chat-emoji">').text(emoji).attr('aria-label', emoji).on('click', function () {
                    var el = input[0], start = el.selectionStart || 0, end = el.selectionEnd || 0;
                    if (el.value.length - (end - start) + emoji.length > (el.maxLength > 0 ? el.maxLength : 2000)) return;
                    el.setRangeText(emoji, start, end, 'end'); input.trigger('input'); el.focus();
                    picker.prop('hidden', true); toggle.attr('aria-expanded', 'false');
                }).appendTo(picker);
            });
            toggle.on('click', function () { var open = picker.prop('hidden'); picker.prop('hidden', !open); toggle.attr('aria-expanded', String(open)); });
            $(document).on('click.ecochatmedia', function (e) { if (!tools[0].contains(e.target)) { picker.prop('hidden', true); toggle.attr('aria-expanded', 'false'); } });
            footer.on('keydown', function (e) { if (e.key === 'Escape') { picker.prop('hidden', true); toggle.attr('aria-expanded', 'false'); } });
            tools.find('.chat-attach').on('click', function () { tools.find('.chat-file')[0].click(); });
            tools.find('.chat-file').on('change', function () {
                var file = this.files[0]; this.value = ''; if (!file) return;
                if (file.size > 5 * 1024 * 1024 || !/image\/(jpeg|png)$/.test(file.type)) { self.notify('Chọn ảnh JPG hoặc PNG, tối đa 5 MB.', 'warning'); return; }
                if (!self.currentConvId) { self.notify('Chọn cuộc trò chuyện trước khi gửi ảnh.', 'warning'); return; }
                var body = new FormData(); body.append('conversationId', self.currentConvId); body.append('image', file);
                body.append('__RequestVerificationToken', $('input[name="__RequestVerificationToken"]').first().val() || '');
                tools.find('.chat-attach').prop('disabled', true).attr('aria-label', 'Đang gửi ảnh');
                $.ajax({ url:'/Chat/SendImage', type:'POST', data:body, processData:false, contentType:false })
                    .done(function (res) { if (!res.success) { self.notify(res.message || 'Không gửi được ảnh.', 'warning'); return; } self.appendMessage(res.message); self.scrollToBottom(); self.updateConversationItem(res.message.maHoiThoai, 'Đã gửi một ảnh', res.message.ngayGui, self.currentUserId); })
                    .fail(function () { self.notify('Gửi ảnh thất bại. Vui lòng chọn ảnh và thử lại.', 'error'); })
                    .always(function () { tools.find('.chat-attach').prop('disabled', false).attr('aria-label', 'Gửi ảnh'); });
            });
            $(document).off('click.ecochatimage', '.chat-image-open').on('click.ecochatimage', '.chat-image-open', function () {
                var url = this.getAttribute('data-image-url'); if (!/^\/Chat\/Image\/\d+$/.test(url || '') || document.getElementById('chat-image-dialog')) return;
                var dialog = document.createElement('dialog'); dialog.id = 'chat-image-dialog'; dialog.className = 'chat-image-dialog'; dialog.setAttribute('aria-label', 'Xem ảnh');
                var close = document.createElement('button'); close.type = 'button'; close.className = 'chat-image-close'; close.setAttribute('aria-label', 'Đóng ảnh'); close.textContent = '×';
                var photo = document.createElement('img'); photo.src = url; photo.alt = 'Ảnh trong cuộc trò chuyện';
                dialog.append(close, photo); document.body.appendChild(dialog); close.onclick = function () { dialog.close(); };
                var previous = this; dialog.addEventListener('close', function () { dialog.remove(); if (previous.isConnected) previous.focus(); }); dialog.showModal();
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
