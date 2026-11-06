/* Thảo luận theo bài học - dữ liệu được ghi qua MVC AJAX, SignalR chỉ đồng bộ thời gian thực. */
(function (window, $) {
    'use strict';
    var cfg, hub, started = false, joined = false, replyingTo = null, loaded = false, sending = false;
    var seenKey, seen = {}, unreadItems = [], sessionUnreadItems = [], openingSeenId = null;
    function updateUnread() {
        var count = unreadItems.length;
        $('#lesson-discussion-launcher-unread').prop('hidden', count === 0).text(count > 99 ? '99+' : count);
        $('#lesson-discussion-launcher').attr('aria-label', 'Thảo luận bài học' + (count ? ', ' + count + ' lượt chưa đọc' : ''));
        var sessionCount = openingSeenId === null ? count : sessionUnreadItems.length;
        $('#lesson-discussion-unread').prop('disabled', !sessionCount).text(sessionCount ? sessionCount + ' lượt thảo luận mới ↓' : 'Không có thảo luận mới');
    }
    var checkingUnread = false;
    function refreshUnread() {
        if (!cfg || !seenKey || document.hidden || checkingUnread) return;
        checkingUnread = true;
        $.ajax({ url: cfg.getUrl, method:'GET', dataType:'json', timeout:15000,
            data:{ courseId:cfg.courseId, lessonId:cfg.lessonId, page:1, pageSize:1, afterId:Number(seen[cfg.lessonId]) || 0 }
        }).done(function (res) {
            if (!res.success) return;
            unreadItems = field(res.discussion, 'items', []).map(normalizeItem).filter(function (item) {
                return !item.isMine && item.id > (Number(seen[cfg.lessonId]) || 0);
            });
            updateUnread();
        }).always(function () { checkingUnread = false; });
    }
    function acknowledgeVisible() {
        if (document.hidden || !cfg || !$('#lesson-discussion-panel').hasClass('active')) return;
        var list = document.getElementById('lesson-discussion-list'), bounds = list.getBoundingClientRect();
        var cursor = Number(seen[cfg.lessonId]) || 0;
        unreadItems.forEach(function (item) {
            var node = list.querySelector('[data-discussion-id="' + item.id + '"]');
            if (!node) return;
            var rect = node.getBoundingClientRect();
            if (rect.top < bounds.bottom && rect.bottom > bounds.top) cursor = Math.max(cursor, item.id);
        });
        seen[cfg.lessonId] = cursor;
        try { localStorage.setItem(seenKey, JSON.stringify(seen)); } catch (_) {}
        unreadItems = unreadItems.filter(function (item) { return item.id > cursor; });
        updateUnread();
        document.dispatchEvent(new CustomEvent('lesson-discussion-read', { detail: { lessonId: cfg.lessonId, latestId: cursor } }));
    }
    function token() { var el = document.querySelector('#learning-antiforgery input'); return el ? el.value : ''; }
    function esc(s) { return $('<div>').text(s || '').html(); }
    // MVC JsonResult giữ PascalCase của ViewModel; API/SignalR có thể dùng camelCase.
    function field(obj, name, fallback) {
        if (!obj) return fallback;
        var value = obj[name];
        if (value === undefined) value = obj[name.charAt(0).toUpperCase() + name.slice(1)];
        return value == null ? fallback : value;
    }
    function normalizeItem(item) {
        return {
            id: field(item, 'id', 0), parentId: field(item, 'parentId', null), content: field(item, 'content', ''),
            replyToAuthor: field(item, 'replyToAuthor', ''), replyToContent: field(item, 'replyToContent', ''),
            createdAt: field(item, 'createdAt', ''), authorName: field(item, 'authorName', ''),
            authorAvatar: field(item, 'authorAvatar', ''), isTeacher: field(item, 'isTeacher', false), isMine: field(item, 'isMine', false),
            likeCount: field(item, 'likeCount', 0), likedByMe: field(item, 'likedByMe', false), hasImage: field(item, 'hasImage', false),
            replies: field(item, 'replies', []).map(normalizeItem)
        };
    }
    function avatar(item) {
        var initial = esc((item.authorName || '?').charAt(0).toUpperCase());
        return '<span class="lesson-comment-avatar lesson-comment-avatar-fallback" style="position:relative;overflow:hidden">' + initial +
            (item.authorAvatar ? '<img class="lesson-comment-avatar-image" src="' + esc(item.authorAvatar) + '" alt="" style="position:absolute;inset:0;width:100%;height:100%;object-fit:cover">' : '') + '</span>';
    }
    function time(value) {
        var d = new Date(value), seconds = Math.floor((Date.now() - d.getTime()) / 1000);
        if (isNaN(d.getTime()) || seconds < 0) return '';
        if (seconds < 60) return 'vừa xong';
        if (seconds < 3600) return Math.floor(seconds / 60) + ' phút trước';
        if (seconds < 86400) return Math.floor(seconds / 3600) + ' giờ trước';
        var days = Math.floor(seconds / 86400);
        if (days < 14) return days + ' ngày trước';
        if (days < 56) return Math.floor(days / 7) + ' tuần trước';
        if (days < 365) return Math.floor(days / 30) + ' tháng trước';
        return Math.floor(days / 365) + ' năm trước';
    }
    function itemHtml(item, reply) {
        var likeClass = item.likedByMe ? ' liked' : '', teacher = item.isTeacher ? '<span class="lesson-comment-teacher">Giáo viên</span>' : '';
        return '<article class="lesson-comment' + (item.isMine ? ' lesson-comment-mine' : ' lesson-comment-other') + '" data-discussion-id="' + item.id + '">' +
            '<div class="lesson-comment-message" tabindex="0">' +
            '<div class="lesson-comment-head">' + avatar(item) + '<div><span class="lesson-comment-author">' + esc(item.authorName) + '</span>' + teacher + '</div></div>' +
            (item.parentId ? '<div class="lesson-comment-reference"><strong>Phản hồi ' + esc(item.replyToAuthor || 'bình luận') + '</strong>' + (item.replyToContent ? '<span>' + esc(item.replyToContent) + '</span>' : '') + '</div>' : '') +
            (item.content ? '<div class="lesson-comment-content">' + esc(item.content) + '</div>' : '') +
            (item.hasImage ? '<img class="lesson-comment-image" alt="Ảnh trong bình luận" src="' + esc(cfg.imageUrl + '?courseId=' + encodeURIComponent(cfg.courseId) + '&lessonId=' + encodeURIComponent(cfg.lessonId) + '&id=' + encodeURIComponent(item.id)) + '">' : '') +
            '<div class="lesson-comment-actions"><span class="lesson-comment-time">' + time(item.createdAt) + '</span><button type="button" class="lesson-like' + likeClass + '"><i class="bi bi-heart' + (item.likedByMe ? '-fill' : '') + '"></i><span>' + (item.likeCount || 0) + '</span> Thích</button>' +
            (!item.parentId ? '<button type="button" class="lesson-reply"><i class="bi bi-reply"></i> Phản hồi</button>' : '') + '</div></div></article>';
    }
    function updateLike(id, count) { var card = $('#lesson-discussion-list [data-discussion-id="' + id + '"]'); card.find('.lesson-like span').first().text(count); }
    function load(forceBottom) {
        if (!cfg) return;
        var list = document.getElementById('lesson-discussion-list');
        var nearBottom = !loaded || (list && list.scrollHeight - list.scrollTop - list.clientHeight < 80);
        var previousScroll = list ? list.scrollTop : 0;
        $('#lesson-discussion-list').html('<div class="lesson-discussion-state"><span class="spinner-border spinner-border-sm"></span> Đang tải thảo luận...</div>');
        $.ajax({ url: cfg.getUrl, method: 'GET', dataType: 'json', timeout: 15000, data: { courseId: cfg.courseId, lessonId: cfg.lessonId, page: 1, pageSize: 30, afterId: openingSeenId === null ? (Number(seen[cfg.lessonId]) || 0) : openingSeenId } }).done(function (res) {
            if (!res.success) { $('#lesson-discussion-list').text('Thảo luận tạm thời chưa sẵn sàng. Vui lòng thử lại sau.'); return; }
            var data = res.discussion || {}, items = field(data, 'items', []).map(normalizeItem);
            unreadItems = items.filter(function (item) { return !item.isMine && item.id > (Number(seen[cfg.lessonId]) || 0); });
            sessionUnreadItems = items.filter(function (item) { return !item.isMine && item.id > (openingSeenId === null ? (Number(seen[cfg.lessonId]) || 0) : openingSeenId); });
            updateUnread();
            var html = items.length ? items.map(function (x, index) {
                function sameGroup(first, second) {
                    return first && second && first.authorName === second.authorName && first.isMine === second.isMine && first.authorAvatar === second.authorAvatar && !first.parentId && !second.parentId && Math.abs(new Date(second.createdAt) - new Date(first.createdAt)) < 300000;
                }
                var grouped = sameGroup(items[index - 1], x);
                var groupEnd = !sameGroup(x, items[index + 1]);
                return itemHtml(x, false).replace('class="lesson-comment', 'class="lesson-comment' + (grouped ? ' lesson-comment-grouped' : '') + (groupEnd ? ' lesson-comment-group-end' : ''));
            }).join('') : '<div class="lesson-discussion-state lesson-discussion-empty"><i class="bi bi-chat-square-text"></i>Chưa có bình luận nào. Hãy bắt đầu cuộc thảo luận.</div>';
            var total = field(data, 'totalComments', 0);
            $('#lesson-discussion-list').html(html);
            if (sessionUnreadItems.length) {
                var firstNew = $('#lesson-discussion-list [data-discussion-id="' + sessionUnreadItems[0].id + '"]');
                firstNew.removeClass('lesson-comment-grouped');
                $('<div>', { 'class': 'lesson-discussion-new-marker', text: 'Thảo luận mới từ đây' }).insertBefore(firstNew);
            }
            $('#lesson-discussion-list .lesson-comment-avatar-image').on('error', function () { this.remove(); }).each(function () { if (this.complete && !this.naturalWidth) this.remove(); }); $('#lesson-discussion-total').text(total);
            $('.teacher-lesson.active .teacher-lesson-count').text(total);
            loaded = true;
            if (list) list.scrollTop = forceBottom || nearBottom ? list.scrollHeight : previousScroll;
            setTimeout(acknowledgeVisible, 300);
        }).fail(function () { $('#lesson-discussion-list').text('Không tải được thảo luận. Vui lòng mở lại để thử lại.'); });
    }
    function startHub() {
        if (started || !$.connection || !$.connection.lessonDiscussionHub) return;
        hub = $.connection.lessonDiscussionHub;
        hub.client.discussionCommentAdded = function () { if (loaded && $('#lesson-discussion-panel').hasClass('active')) load(); else refreshUnread(); };
        hub.client.discussionLikeChanged = function (data) { updateLike(data.id, data.likeCount); };
        $.connection.hub.start().done(function () { started = true; join(); });
    }
    function join() { if (!started || joined || !cfg) return; hub.server.joinLesson(cfg.courseId, cfg.lessonId).done(function () { joined = true; }); }
    function clearReply() {
        replyingTo = null;
        $('#lesson-discussion-reply-target').prop('hidden', true);
        $('#lesson-discussion-reply-name').text('');
        $('#lesson-discussion-input').attr('placeholder', 'Viết thảo luận...');
    }
    function reply(id, name) {
        replyingTo = id;
        $('#lesson-discussion-reply-name').text(name || 'người học');
        $('#lesson-discussion-reply-target').prop('hidden', false);
        $('#lesson-discussion-input').attr('placeholder', 'Viết phản hồi...').focus();
    }
    function post() {
        var input = $('#lesson-discussion-input'), content = input.val().trim(), image = $('#lesson-discussion-image')[0].files[0]; if ((!content && !image) || !cfg || sending) return;
        sending = true;
        var button = $('#lesson-discussion-send').prop('disabled', true);
        var sentReplyId = replyingTo;
        var data = new FormData(); data.append('__RequestVerificationToken', token()); data.append('courseId', cfg.courseId); data.append('lessonId', cfg.lessonId); if (sentReplyId) data.append('parentId', sentReplyId); data.append('content', content); if (image) data.append('image', image);
        $.ajax({ url: cfg.createUrl, method: 'POST', dataType: 'json', timeout: 60000, data: data, processData: false, contentType: false }).done(function (res) {
            if (!res.success) { window.showToast ? showToast(res.message || 'Không gửi được bình luận.', 'error') : alert(res.message || 'Không gửi được bình luận.'); return; }
            input.val('').trigger('input'); if (replyingTo === sentReplyId) clearReply(); clearImage(); load(true);
        }).fail(function () { window.showToast ? showToast('Chưa nhận được kết quả gửi. Vui lòng tải lại thảo luận để kiểm tra.', 'error') : alert('Chưa nhận được kết quả gửi. Vui lòng tải lại thảo luận để kiểm tra.'); }).always(function () { sending = false; button.prop('disabled', false); });
    }
    window.openLessonDiscussion = function (focusInput) { if (openingSeenId === null) openingSeenId = Number(seen[cfg.lessonId]) || 0; $('#lesson-discussion-backdrop,#lesson-discussion-panel').addClass('active'); startHub(); join(); load(); if (focusInput !== false) setTimeout(function () { $('#lesson-discussion-input').focus(); }, 200); };
    window.closeLessonDiscussion = function () { $('#lesson-discussion-backdrop,#lesson-discussion-panel').removeClass('active'); openingSeenId = null; sessionUnreadItems = []; updateUnread(); };
    window.EcoLessonDiscussion = { init: function (options) {
        cfg = options; startHub();
        // Upgrade an already open page when the shared script is refreshed.
        if (!document.getElementById('lesson-discussion-unread')) {
            $('#lesson-discussion-title').empty().append($('<button>', { type: 'button', id: 'lesson-discussion-unread', disabled: true, text: 'Không có thảo luận mới' }));
        }
        seenKey = 'teacher-discussions-seen-' + ($('#lesson-discussion-panel').attr('data-user-id') || '');
        try { seen = JSON.parse(localStorage.getItem(seenKey) || '{}') || {}; } catch (_) { seen = {}; }
        refreshUnread();
        setInterval(refreshUnread, 10000);
        document.addEventListener('visibilitychange', refreshUnread);
        window.addEventListener('storage', function (event) {
            if (event.key !== seenKey) return;
            try { seen = JSON.parse(event.newValue || '{}') || {}; } catch (_) { seen = {}; }
            refreshUnread();
        });
        $('#lesson-discussion-unread').on('click', function () {
            if (!sessionUnreadItems.length) return;
            var first = document.querySelector('[data-discussion-id="' + sessionUnreadItems[0].id + '"]');
            if (first) { first.scrollIntoView({ behavior: 'smooth', block: 'center' }); first.classList.add('lesson-unread-highlight'); setTimeout(function () { first.classList.remove('lesson-unread-highlight'); acknowledgeVisible(); }, 700); }
        });
        var readTimer;
        $('#lesson-discussion-list').on('scroll wheel touchmove keydown', function () {
            clearTimeout(readTimer); readTimer = setTimeout(acknowledgeVisible, 500);
        });
        $('#lesson-discussion-list').on('click', '.lesson-comment-content,.lesson-comment-image', function () {
            var card = $(this).closest('.lesson-comment');
            $('#lesson-discussion-list .lesson-comment').not(card).removeClass('show-actions');
            card.toggleClass('show-actions');
        });
        function resizeComposer() {
            var input = document.getElementById('lesson-discussion-input');
            if (!input) return;
            input.style.height = 'auto';
            input.style.height = Math.min(140, Math.max(42, input.scrollHeight)) + 'px';
        }
        $('#lesson-discussion-input').on('input', resizeComposer);

        $('#lesson-discussion-form').on('submit', function (e) { e.preventDefault(); post(); });
        $('#lesson-discussion-send').on('click', post);
        $('#lesson-discussion-input').on('keydown', function (e) {
            if (e.key !== 'Enter' || e.isComposing || (e.originalEvent && e.originalEvent.isComposing) || e.keyCode === 229) return;
            e.preventDefault();
            if (e.altKey) {
                if (this.value.length - (this.selectionEnd - this.selectionStart) < this.maxLength)
                    this.setRangeText('\n', this.selectionStart, this.selectionEnd, 'end');
            } else if (!e.repeat && !(e.originalEvent && e.originalEvent.repeat)) {
                post();
            }
        });
        var emojis = ['😀','😂','😊','😍','🥰','😎','🤔','😢','😮','😅','👍','👎','❤️','🎉','👏','🙏','🔥','✅','💡','💯'];
        var picker = $('#lesson-discussion-emoji-picker');
        emojis.forEach(function (emoji) { $('<button type="button">').text(emoji).attr('aria-label', emoji).on('click', function () { var input = document.getElementById('lesson-discussion-input'), start = input.selectionStart, end = input.selectionEnd; input.setRangeText(emoji, start, end, 'end'); input.focus(); picker.prop('hidden', true); $('#lesson-discussion-emoji-toggle').attr('aria-expanded', 'false'); }).appendTo(picker); });
        $('#lesson-discussion-emoji-toggle').on('click', function () { var show = picker.prop('hidden'); picker.prop('hidden', !show); $(this).attr('aria-expanded', String(show)); });
        $('#lesson-discussion-image-button').on('click', function () { $('#lesson-discussion-image').trigger('click'); });
        $('#lesson-discussion-image').on('change', function () { var file = this.files[0]; if (!file) return; if (!/^image\/(jpeg|png)$/.test(file.type) || file.size > 5 * 1024 * 1024) { alert('Chọn ảnh JPG hoặc PNG, tối đa 5 MB.'); clearImage(); return; } var reader = new FileReader(); reader.onload = function () { $('#lesson-discussion-image-preview img').attr('src', reader.result); $('#lesson-discussion-image-preview').prop('hidden', false); }; reader.readAsDataURL(file); });
        $('#lesson-discussion-image-remove').on('click', clearImage);
        $('#lesson-discussion-reply-clear').on('click', function () { clearReply(); $('#lesson-discussion-input').focus(); });
        $('#lesson-discussion-list').on('click', '.lesson-reply', function () {
            var card = $(this).closest('[data-discussion-id]');
            reply(Number(card.data('discussion-id')), card.find('.lesson-comment-author').first().text());
        });
        $('#lesson-discussion-list').on('click', '.lesson-like', function () { var card = $(this).closest('[data-discussion-id]'), id = Number(card.data('discussion-id')), btn = $(this); $.post(cfg.likeUrl, { __RequestVerificationToken: token(), courseId: cfg.courseId, lessonId: cfg.lessonId, discussionId: id }).done(function (res) { if (res.success) { updateLike(id, res.likeCount); btn.toggleClass('liked', !!res.likedByMe).find('i').attr('class', 'bi bi-heart' + (res.likedByMe ? '-fill' : '')); } }); });
    }};
    function clearImage() { $('#lesson-discussion-image').val(''); $('#lesson-discussion-image-preview').prop('hidden', true).find('img').removeAttr('src'); }
}(window, jQuery));
