/* Thảo luận theo bài học - dữ liệu được ghi qua MVC AJAX, SignalR chỉ đồng bộ thời gian thực. */
(function (window, $) {
    'use strict';
    var cfg, hub, started = false, joined = false, replyingTo = null, loaded = false, sending = false;
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
        if (item.authorAvatar) return '<img class="lesson-comment-avatar" src="' + esc(item.authorAvatar) + '" alt="">';
        return '<span class="lesson-comment-avatar lesson-comment-avatar-fallback">' + esc((item.authorName || '?').charAt(0).toUpperCase()) + '</span>';
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
            '<div class="lesson-comment-message">' +
            '<div class="lesson-comment-head">' + avatar(item) + '<div><span class="lesson-comment-author">' + esc(item.authorName) + '</span>' + teacher + '<span class="lesson-comment-time">' + time(item.createdAt) + '</span></div></div>' +
            (item.parentId ? '<div class="lesson-comment-reference"><strong>Phản hồi ' + esc(item.replyToAuthor || 'bình luận') + '</strong>' + (item.replyToContent ? '<span>' + esc(item.replyToContent) + '</span>' : '') + '</div>' : '') +
            (item.content ? '<div class="lesson-comment-content">' + esc(item.content) + '</div>' : '') +
            (item.hasImage ? '<img class="lesson-comment-image" alt="Ảnh trong bình luận" src="' + esc(cfg.imageUrl + '?courseId=' + encodeURIComponent(cfg.courseId) + '&lessonId=' + encodeURIComponent(cfg.lessonId) + '&id=' + encodeURIComponent(item.id)) + '">' : '') +
            '<div class="lesson-comment-actions"><button type="button" class="lesson-like' + likeClass + '"><i class="bi bi-heart' + (item.likedByMe ? '-fill' : '') + '"></i><span>' + (item.likeCount || 0) + '</span> Thích</button>' +
            (!item.parentId ? '<button type="button" class="lesson-reply"><i class="bi bi-reply"></i> Phản hồi</button>' : '') + '</div></div></article>';
    }
    function updateLike(id, count) { var card = $('#lesson-discussion-list [data-discussion-id="' + id + '"]'); card.find('.lesson-like span').first().text(count); }
    function load(forceBottom) {
        if (!cfg) return;
        var list = document.getElementById('lesson-discussion-list');
        var nearBottom = !loaded || (list && list.scrollHeight - list.scrollTop - list.clientHeight < 80);
        var previousScroll = list ? list.scrollTop : 0;
        $('#lesson-discussion-list').html('<div class="lesson-discussion-state"><span class="spinner-border spinner-border-sm"></span> Đang tải thảo luận...</div>');
        $.ajax({ url: cfg.getUrl, method: 'GET', dataType: 'json', timeout: 15000, data: { courseId: cfg.courseId, lessonId: cfg.lessonId, page: 1, pageSize: 30 } }).done(function (res) {
            if (!res.success) { $('#lesson-discussion-list').text('Thảo luận tạm thời chưa sẵn sàng. Vui lòng thử lại sau.'); return; }
            var data = res.discussion || {}, items = field(data, 'items', []).map(normalizeItem);
            var html = items.length ? items.map(function (x) { return itemHtml(x, false); }).join('') : '<div class="lesson-discussion-state lesson-discussion-empty"><i class="bi bi-chat-square-text"></i>Chưa có bình luận nào. Hãy bắt đầu cuộc thảo luận.</div>';
            var total = field(data, 'totalComments', 0);
            $('#lesson-discussion-list').html(html); $('#lesson-discussion-total').text(total);
            $('.teacher-lesson.active .teacher-lesson-count').text(total);
            loaded = true;
            if (list) list.scrollTop = forceBottom || nearBottom ? list.scrollHeight : previousScroll;
        }).fail(function () { $('#lesson-discussion-list').text('Không tải được thảo luận. Vui lòng mở lại để thử lại.'); });
    }
    function startHub() {
        if (started || !$.connection || !$.connection.lessonDiscussionHub) return;
        hub = $.connection.lessonDiscussionHub;
        hub.client.discussionCommentAdded = function () { if (loaded) load(); };
        hub.client.discussionLikeChanged = function (data) { updateLike(data.id, data.likeCount); };
        $.connection.hub.start().done(function () { started = true; if ($('#lesson-discussion-panel').hasClass('active')) join(); });
    }
    function join() { if (!started || joined || !cfg) return; hub.server.joinLesson(cfg.courseId, cfg.lessonId).done(function () { joined = true; }); }
    function clearReply() {
        replyingTo = null;
        $('#lesson-discussion-reply-target').prop('hidden', true);
        $('#lesson-discussion-reply-name').text('');
        $('#lesson-discussion-input').attr('placeholder', 'Viết bình luận hoặc câu hỏi về bài học...');
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
            input.val(''); if (replyingTo === sentReplyId) clearReply(); clearImage(); load(true);
        }).fail(function () { window.showToast ? showToast('Chưa nhận được kết quả gửi. Vui lòng tải lại thảo luận để kiểm tra.', 'error') : alert('Chưa nhận được kết quả gửi. Vui lòng tải lại thảo luận để kiểm tra.'); }).always(function () { sending = false; button.prop('disabled', false); });
    }
    window.openLessonDiscussion = function () { $('#lesson-discussion-backdrop,#lesson-discussion-panel').addClass('active'); startHub(); join(); if (!loaded) load(); setTimeout(function () { $('#lesson-discussion-input').focus(); }, 200); };
    window.closeLessonDiscussion = function () { $('#lesson-discussion-backdrop,#lesson-discussion-panel').removeClass('active'); };
    window.EcoLessonDiscussion = { init: function (options) {
        cfg = options; startHub();
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
