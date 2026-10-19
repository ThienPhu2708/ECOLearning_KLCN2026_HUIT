/* Lưu mã giảm giá vào "Mã giảm giá của tôi".
   Nút: <button data-coupon-collect data-code="BLF2026" data-label="Giảm 25%">Lưu mã</button>
   Bấm → hộp thoại xác nhận (Views/Shared/_CouponCollectModal.cshtml) → POST Coupon/Collect.
   Lưu xong mọi nút cùng mã trên trang chuyển thành "Đã lưu". Không có thao tác hoàn tác. */
(function () {
    var MODAL_ID = 'coupon-collect-modal';
    var pendingCode = null;
    var lastTrigger = null;

    function modal() { return document.getElementById(MODAL_ID); }
    function part(sel) { var m = modal(); return m ? m.querySelector(sel) : null; }

    function token() {
        var input = part('input[name="__RequestVerificationToken"]') || document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function loginUrl() {
        return (window.COUPON_LOGIN_URL || '/Account/Login') + '?returnUrl=' + encodeURIComponent(location.pathname + location.search);
    }

    function showError(msg) {
        var err = part('[data-cpn-error]');
        if (!err) return;
        err.textContent = msg || '';
        err.style.display = msg ? 'block' : 'none';
    }

    function setBusy(busy) {
        var ok = part('[data-cpn-ok]');
        if (!ok) return;
        ok.disabled = busy;
        ok.textContent = busy ? 'Đang lưu...' : 'Lưu mã';
    }

    function open(btn) {
        pendingCode = btn.getAttribute('data-code');
        lastTrigger = btn;
        part('[data-cpn-code]').textContent = pendingCode;
        part('[data-cpn-label]').textContent = btn.getAttribute('data-label') || '';
        showError('');
        setBusy(false);
        if (typeof openModal === 'function') openModal(MODAL_ID);
        else modal().classList.add('active');
        var ok = part('[data-cpn-ok]');
        if (ok) ok.focus();
    }

    function close() {
        if (typeof closeModal === 'function') closeModal(MODAL_ID);
        else modal().classList.remove('active');
        if (lastTrigger && document.body.contains(lastTrigger)) lastTrigger.focus();
        pendingCode = null;
    }

    function markSaved(code) {
        document.querySelectorAll('[data-coupon-collect]').forEach(function (b) {
            if (b.getAttribute('data-code') !== code) return;
            var s = document.createElement('span');
            s.className = 'cpn-btn cpn-btn-saved';
            s.innerHTML = '<i class="bi bi-check2"></i> Đã lưu';
            b.replaceWith(s);
        });
        document.dispatchEvent(new CustomEvent('coupon:saved', { detail: { code: code } }));
    }

    function submit() {
        if (!pendingCode) return;
        var code = pendingCode;
        setBusy(true);
        showError('');
        var body = new URLSearchParams();
        body.append('code', code);
        body.append('__RequestVerificationToken', token());
        fetch(window.COUPON_COLLECT_URL || '/Coupon/Collect', {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-Requested-With': 'XMLHttpRequest' },
            body: body
        })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                if (res.requireLogin) { window.location.href = loginUrl(); return; }
                if (!res.success) { setBusy(false); showError(res.message); return; }
                lastTrigger = null;
                close();
                markSaved(code);
            })
            .catch(function () {
                setBusy(false);
                showError('Không lưu được mã, vui lòng thử lại.');
            });
    }

    document.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-coupon-collect]');
        if (btn) {
            e.preventDefault();
            if (!modal()) { window.location.href = loginUrl(); return; }
            open(btn);
            return;
        }
        if (e.target.closest('#' + MODAL_ID + ' [data-cpn-cancel]')) close();
        else if (e.target.closest('#' + MODAL_ID + ' [data-cpn-ok]')) submit();
    });

    document.addEventListener('keydown', function (e) {
        var m = modal();
        if (e.key === 'Escape' && m && m.classList.contains('active')) close();
    });
})();
