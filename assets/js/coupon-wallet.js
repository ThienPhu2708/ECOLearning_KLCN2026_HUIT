/* Lưu mã giảm giá vào "Mã giảm giá của tôi".
   Nút: <button data-coupon-collect data-code="BLF2026">Lưu mã</button>
   Bấm là lưu ngay (POST Coupon/Collect, không hỏi xác nhận). Lưu xong mọi nút cùng mã trên trang chuyển thành "Đã lưu"
   màu xám, không bấm lại được. Lỗi thì nút trở lại "Lưu mã" và hiện thông báo. Không có thao tác hoàn tác. */
(function () {
    var FORM_ID = 'coupon-collect-form';

    function token() {
        var f = document.getElementById(FORM_ID);
        var input = f ? f.querySelector('input[name="__RequestVerificationToken"]') : null;
        return input ? input.value : '';
    }

    function loginUrl() {
        return (window.COUPON_LOGIN_URL || '/Account/Login') + '?returnUrl=' + encodeURIComponent(location.pathname + location.search);
    }

    function thongBao(msg) {
        if (typeof window.showToast === 'function') window.showToast(msg, 'error');
        else alert(msg);
    }

    function cacNut(code) {
        return Array.prototype.filter.call(document.querySelectorAll('[data-coupon-collect]'), function (b) {
            return b.getAttribute('data-code') === code;
        });
    }

    function dangLuu(code, busy) {
        cacNut(code).forEach(function (b) {
            b.disabled = busy;
            b.setAttribute('aria-busy', busy ? 'true' : 'false');
            b.textContent = busy ? 'Đang lưu...' : 'Lưu mã';
        });
    }

    function daLuu(code) {
        cacNut(code).forEach(function (b) {
            var s = document.createElement('button');
            s.type = 'button';
            s.className = b.className.replace('cpn-btn-primary', '').trim() + ' cpn-btn-saved';
            s.disabled = true;
            s.innerHTML = '<i class="bi bi-check2"></i> Đã lưu';
            b.replaceWith(s);
        });
        document.dispatchEvent(new CustomEvent('coupon:saved', { detail: { code: code } }));
    }

    function luu(btn) {
        var code = btn.getAttribute('data-code');
        if (!code || btn.disabled) return;
        // Khách / tài khoản không dùng mã (không có token trên trang): đăng nhập trước
        if (!document.getElementById(FORM_ID)) { window.location.href = loginUrl(); return; }

        dangLuu(code, true);
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
                if (!res.success) { dangLuu(code, false); thongBao(res.message || 'Không lưu được mã, vui lòng thử lại.'); return; }
                daLuu(code); // gồm cả trường hợp mã đã có trong ví (alreadySaved)
            })
            .catch(function () {
                dangLuu(code, false);
                thongBao('Không lưu được mã, vui lòng thử lại.');
            });
    }

    document.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-coupon-collect]');
        if (!btn) return;
        e.preventDefault();
        luu(btn);
    });
})();
