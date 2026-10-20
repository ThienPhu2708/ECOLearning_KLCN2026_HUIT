/* Ô chọn ngân hàng dùng chung (Views/Shared/_BankPicker.cshtml).
   - Chỉ chọn trong danh mục (Services/BankDirectory.cs), không gõ tên tự do.
   - Tìm theo mã / tên viết tắt / tên đầy đủ, có dấu hay không ("vcb", "ngoai thuong"); xếp theo mức khớp
     (trùng mã > mã bắt đầu bằng > tên viết tắt bắt đầu bằng > khớp đầu một từ > chứa chuỗi).
   - ↑ ↓ Enter Esc; chọn xong phát sự kiện "bank:change" (detail: { code, shortName }) trên khối .bp.
   Kèm vài hàm chuẩn hóa dùng chung: BankPicker.chiSo, BankPicker.tenChuTaiKhoan (giống BankDirectory.cs). */
(function () {
    function khongDau(s) {
        return (s || '').normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D');
    }
    function timKiem(s) { return khongDau(s).toLowerCase().replace(/[^a-z0-9]+/g, ' ').trim(); }

    function mucKhop(b, k) {
        var c = timKiem(b.c), s = timKiem(b.s), f = timKiem(b.f);
        if (c === k || s === k) return 0;
        if (c.indexOf(k) === 0) return 1;
        if (s.indexOf(k) === 0) return 2;
        if ((' ' + s + ' ' + f).indexOf(' ' + k) >= 0) return 3;
        if (s.indexOf(k) >= 0 || f.indexOf(k) >= 0) return 4;
        return -1;
    }

    function khoiTao(root) {
        var BANKS = JSON.parse(root.getAttribute('data-banks') || '[]');
        var o = root.querySelector('.bp-search');
        var list = root.querySelector('.bp-list');
        var ma = root.querySelector('.bp-value');
        var loi = root.querySelector('.bp-error');
        var dangChon = -1, ketQua = [];

        function nhan(b) { return b.s + ' — ' + b.f; }
        function theoMa(code) { return BANKS.filter(function (x) { return x.c === code; })[0]; }
        function baoLoi(msg) { loi.textContent = msg || ''; o.classList.toggle('is-invalid', !!msg); }
        function phat(b) { root.dispatchEvent(new CustomEvent('bank:change', { bubbles: true, detail: { code: b ? b.c : '', shortName: b ? b.s : '' } })); }

        function tim(q) {
            var k = timKiem(q);
            if (!k) return BANKS.slice();
            return BANKS.map(function (b, i) { return { b: b, m: mucKhop(b, k), i: i }; })
                .filter(function (x) { return x.m >= 0; })
                .sort(function (x, y) { return x.m - y.m || x.i - y.i; })
                .map(function (x) { return x.b; });
        }
        function ve() {
            list.textContent = '';
            if (ketQua.length === 0) {
                var e = document.createElement('li');
                e.className = 'bp-empty';
                e.textContent = 'Không tìm thấy ngân hàng phù hợp. Thử gõ mã (VCB, TCB, MB...) hoặc tên viết tắt.';
                list.appendChild(e);
            }
            ketQua.forEach(function (b, i) {
                var li = document.createElement('li');
                li.className = 'bp-opt' + (i === dangChon ? ' is-active' : '');
                li.id = o.id + '-opt-' + i;
                li.setAttribute('role', 'option');
                li.setAttribute('aria-selected', i === dangChon ? 'true' : 'false');
                var code = document.createElement('span'); code.className = 'bp-code'; code.textContent = b.c;
                var t = document.createElement('span'); t.className = 'bp-text';
                var s = document.createElement('div'); s.className = 'bp-short'; s.textContent = b.s;
                var f = document.createElement('div'); f.className = 'bp-full'; f.textContent = b.f;
                t.appendChild(s); t.appendChild(f); li.appendChild(code); li.appendChild(t);
                li.addEventListener('mousedown', function (ev) { ev.preventDefault(); chon(b); });
                list.appendChild(li);
            });
            if (dangChon >= 0) {
                o.setAttribute('aria-activedescendant', o.id + '-opt-' + dangChon);
                var el = document.getElementById(o.id + '-opt-' + dangChon);
                if (el) el.scrollIntoView({ block: 'nearest' });
            } else {
                o.removeAttribute('aria-activedescendant');
            }
        }
        function mo(q) {
            ketQua = tim(q);
            dangChon = ketQua.length > 0 && timKiem(q) ? 0 : -1;
            ve();
            list.hidden = false;
            o.setAttribute('aria-expanded', 'true');
        }
        function dong() { list.hidden = true; o.setAttribute('aria-expanded', 'false'); o.removeAttribute('aria-activedescendant'); }
        function chon(b) {
            ma.value = b.c;
            o.value = nhan(b);
            baoLoi('');
            var note = root.querySelector('.bp-note');
            if (note) note.hidden = true;
            dong();
            phat(b);
            var next = root.getAttribute('data-next');
            if (next && document.getElementById(next)) document.getElementById(next).focus();
        }

        o.addEventListener('focus', function () { o.select(); mo(''); });
        o.addEventListener('input', function () { ma.value = ''; mo(o.value); phat(null); });
        o.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); if (list.hidden) mo(o.value); dangChon = Math.min(ketQua.length - 1, dangChon + 1); ve(); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); dangChon = Math.max(0, dangChon - 1); ve(); }
            else if (e.key === 'Enter') { if (!list.hidden && dangChon >= 0) { e.preventDefault(); chon(ketQua[dangChon]); } }
            else if (e.key === 'Escape') { dong(); }
        });
        o.addEventListener('blur', function () {
            dong();
            var b = theoMa(ma.value);
            if (b) { o.value = nhan(b); }
            else if (o.value.trim()) { baoLoi('Hãy chọn ngân hàng trong danh sách.'); }
        });

        root.bankPicker = {
            value: function () { return ma.value; },
            shortName: function () { var b = theoMa(ma.value); return b ? b.s : ''; },
            validate: function () {
                if (theoMa(ma.value)) return true;
                baoLoi('Vui lòng chọn ngân hàng nhận thanh toán.');
                o.focus();
                return false;
            }
        };
    }

    window.BankPicker = {
        chiSo: function (s) { return (s || '').replace(/\D+/g, ''); },
        tenChuTaiKhoan: function (s) { return khongDau(s).toUpperCase().replace(/[^A-Z]+/g, ' ').trim(); },
        get: function (el) { var r = el && el.closest ? el.closest('[data-bank-picker]') : null; return r ? r.bankPicker : null; }
    };

    document.querySelectorAll('[data-bank-picker]').forEach(khoiTao);
})();
