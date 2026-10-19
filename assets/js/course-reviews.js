/* Đánh giá khóa học (Views/Course/_ReviewItem.cshtml, _ReviewSection.cshtml, Reviews.cshtml):
   - "Bạn thấy hữu ích?": POST Review/Helpful; bấm lại lựa chọn đang có để bỏ. Khách → trang đăng nhập.
   - Phản hồi của giáo viên: POST Review/Reply (viết / sửa), Review/DeleteReply. Server kiểm tra quyền.
   Nội dung đưa vào trang bằng textContent (không chèn HTML). */
(function () {
    function root(el) { return el.closest('.rv-root'); }

    function token(r) {
        var i = r.querySelector('input[name="__RequestVerificationToken"]') || document.querySelector('input[name="__RequestVerificationToken"]');
        return i ? i.value : '';
    }

    function post(r, url, data) {
        var body = new URLSearchParams(data);
        body.append('__RequestVerificationToken', token(r));
        return fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-Requested-With': 'XMLHttpRequest' },
            body: body
        }).then(function (res) { if (!res.ok) throw new Error(res.status); return res.json(); });
    }

    function thongBao(msg, loai) {
        if (typeof showToast === 'function') showToast(msg, loai || 'error');
        else alert(msg);
    }

    function veBinhChon(item, res) {
        item.querySelectorAll('[data-rv-vote]').forEach(function (b) {
            var laHuuIch = b.getAttribute('data-rv-vote') === '1';
            var dangChon = res.myVote !== null && res.myVote !== undefined && String(res.myVote) === b.getAttribute('data-rv-vote');
            b.classList.toggle('is-active', dangChon);
            b.setAttribute('aria-pressed', dangChon ? 'true' : 'false');
            b.querySelector('i').className = 'bi ' + (laHuuIch ? 'bi-hand-thumbs-up' : 'bi-hand-thumbs-down') + (dangChon ? '-fill' : '');
            var n = laHuuIch ? res.helpfulCount : res.notHelpfulCount;
            b.querySelector('.rv-count').textContent = n > 0 ? n : '';
        });
    }

    function moForm(item, noiDungCu) {
        var form = item.querySelector('.rv-reply-form');
        if (!form) return;
        form.hidden = false;
        form.querySelector('.rv-reply-error').textContent = '';
        var ta = form.querySelector('textarea');
        ta.value = noiDungCu || '';
        ta.focus();
        var nutMo = item.querySelector('[data-rv-reply-open]');
        if (nutMo) nutMo.hidden = true;
    }

    function dongForm(item) {
        var form = item.querySelector('.rv-reply-form');
        if (form) form.hidden = true;
        var coPhanHoi = !item.querySelector('.rv-reply').hidden;
        var nutMo = item.querySelector('[data-rv-reply-open]');
        if (nutMo) nutMo.hidden = coPhanHoi;
    }

    document.addEventListener('click', function (e) {
        var vote = e.target.closest('[data-rv-vote]');
        if (vote && root(vote)) {
            var r = root(vote), item = vote.closest('.rv-item');
            vote.disabled = true;
            post(r, r.getAttribute('data-helpful-url'), { reviewId: item.getAttribute('data-review-id'), helpful: vote.getAttribute('data-rv-vote') === '1' })
                .then(function (res) {
                    if (res.requireLogin) { window.location.href = r.getAttribute('data-login-url'); return; }
                    if (!res.success) { thongBao(res.message); return; }
                    veBinhChon(item, res);
                })
                .catch(function () { thongBao('Không gửi được bình chọn, vui lòng thử lại.'); })
                .then(function () { vote.disabled = false; });
            return;
        }

        var mo = e.target.closest('[data-rv-reply-open]');
        if (mo) { moForm(mo.closest('.rv-item'), ''); return; }

        var sua = e.target.closest('[data-rv-reply-edit]');
        if (sua) {
            var it = sua.closest('.rv-item');
            moForm(it, it.querySelector('.rv-reply-text').textContent);
            return;
        }

        var huy = e.target.closest('[data-rv-reply-cancel]');
        if (huy) { dongForm(huy.closest('.rv-item')); return; }

        var xoa = e.target.closest('[data-rv-reply-delete]');
        if (xoa) {
            if (!confirm('Xóa phản hồi này? Học viên sẽ không còn thấy phản hồi của bạn.')) return;
            var r2 = root(xoa), item2 = xoa.closest('.rv-item');
            xoa.disabled = true;
            post(r2, r2.getAttribute('data-delete-reply-url'), { reviewId: item2.getAttribute('data-review-id') })
                .then(function (res) {
                    if (!res.success) { thongBao(res.message); return; }
                    item2.querySelector('.rv-reply').hidden = true;
                    item2.querySelector('.rv-reply-text').textContent = '';
                    dongForm(item2);
                })
                .catch(function () { thongBao('Không xóa được phản hồi, vui lòng thử lại.'); })
                .then(function () { xoa.disabled = false; });
        }
    });

    document.addEventListener('submit', function (e) {
        var form = e.target.closest('.rv-reply-form');
        if (!form) return;
        e.preventDefault();
        var r = root(form), item = form.closest('.rv-item');
        var ta = form.querySelector('textarea'), loi = form.querySelector('.rv-reply-error');
        var noiDung = ta.value.trim();
        if (!noiDung) { loi.textContent = 'Vui lòng nhập nội dung phản hồi.'; ta.focus(); return; }
        var nut = form.querySelector('button[type="submit"]');
        nut.disabled = true;
        post(r, r.getAttribute('data-reply-url'), { reviewId: item.getAttribute('data-review-id'), content: noiDung })
            .then(function (res) {
                if (res.requireLogin) { window.location.href = r.getAttribute('data-login-url'); return; }
                if (!res.success) { loi.textContent = res.message || 'Không gửi được phản hồi.'; return; }
                var khoi = item.querySelector('.rv-reply');
                khoi.querySelector('.rv-reply-text').textContent = res.content;
                khoi.querySelector('.rv-reply-time').textContent = res.dateText || '';
                khoi.hidden = false;
                dongForm(item);
            })
            .catch(function () { loi.textContent = 'Không gửi được phản hồi, vui lòng thử lại.'; })
            .then(function () { nut.disabled = false; });
    });

    // Tiểu sử giảng viên dài: thu gọn, có nút "Hiện thêm" (Views/Course/_InstructorInfo.cshtml)
    var bio = document.getElementById('gv-bio');
    var nutBio = document.querySelector('.gv-more');
    if (bio && nutBio) {
        if (bio.scrollHeight > bio.clientHeight + 4) nutBio.hidden = false;
        nutBio.addEventListener('click', function () {
            var mo = bio.classList.toggle('is-open');
            nutBio.setAttribute('aria-expanded', mo ? 'true' : 'false');
            nutBio.querySelector('span').textContent = mo ? 'Thu gọn' : 'Hiện thêm';
            nutBio.querySelector('i').className = mo ? 'bi bi-chevron-up' : 'bi bi-chevron-down';
        });
    }
})();
