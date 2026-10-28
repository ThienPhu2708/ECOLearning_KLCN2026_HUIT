/* Bong bóng Trợ lý Elearning (Views/Shared/_ChatBubble.cshtml).
   - Bấm bong bóng: mở / thu nhỏ cửa sổ chat. Lần mở đầu tiên tải lịch sử của phiên (GET /Chatbot/LichSu) để chuyển trang vẫn thấy tin cũ.
   - Badge đỏ: số tin bot gửi khi cửa sổ đang đóng (kể cả lời chào lần đầu vào trang); chấm xanh: trợ lý đang hoạt động.
   - Số tin chưa đọc và cờ "đã chào" lưu ở sessionStorage (chỉ là tiện ích, mất cũng không sao). */
(function () {
    var root = document.getElementById('eco-cb');
    if (!root) return;

    var bubble = document.getElementById('eco-cb-bubble');
    var win = document.getElementById('eco-cb-window');
    var badge = document.getElementById('eco-cb-badge');
    var teaser = document.getElementById('eco-cb-teaser');
    var log = document.getElementById('eco-cb-log');
    var chips = document.getElementById('eco-cb-chips');
    var form = document.getElementById('eco-cb-form');
    var input = document.getElementById('eco-cb-input');
    var send = document.getElementById('eco-cb-send');
    var token = form.querySelector('input[name="__RequestVerificationToken"]').value;
    var robot = root.querySelector('.eco-cb-avatar svg');

    var KHOA_DA_CHAO = 'ecoCb:daChao';
    var KHOA_CHUA_DOC = 'ecoCb:chuaDoc';
    var LOI_CHAO = 'Xin chào! Mình là **Trợ lý Elearning**. Mình có thể giúp bạn tìm khóa học theo chủ đề, giá hoặc cấp độ. Bạn muốn học gì hôm nay?';

    var mo = false, dang = false, daTai = false;
    var chuaDoc = parseInt(doc(KHOA_CHUA_DOC), 10) || 0;

    function doc(k) { try { return window.sessionStorage.getItem(k); } catch (e) { return null; } }
    function luu(k, v) { try { window.sessionStorage.setItem(k, v); } catch (e) { } }

    function esc(s) { return String(s == null ? '' : s).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
    // Chữ đậm **...** và xuống dòng; mọi thứ khác đều được escape trước
    function dinhDang(s) { return esc(s).replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>').replace(/\n/g, '<br>'); }
    function tien(n) { return n > 0 ? Number(n).toLocaleString('vi-VN') + 'đ' : 'Miễn phí'; }
    function cuonXuong() { log.scrollTop = log.scrollHeight; }

    function guiForm(url, duLieu) {
        var body = new URLSearchParams();
        body.append('__RequestVerificationToken', token);
        Object.keys(duLieu || {}).forEach(function (k) { body.append(k, duLieu[k]); });
        return fetch(url, { method: 'POST', body: body, credentials: 'same-origin', headers: { 'Content-Type': 'application/x-www-form-urlencoded' } });
    }

    /* ---------- Tin nhắn ---------- */
    function them(vai, html, loi) {
        var hang = document.createElement('div');
        hang.className = 'eco-cb-row ' + (vai === 'user' ? 'user' : 'bot') + (loi ? ' err' : '');
        if (vai !== 'user') {
            var av = document.createElement('span');
            av.className = 'eco-cb-av';
            av.appendChild(robot.cloneNode(true));
            hang.appendChild(av);
        }
        var msg = document.createElement('div');
        msg.className = 'eco-cb-msg';
        msg.innerHTML = html;
        hang.appendChild(msg);
        log.appendChild(hang);
        cuonXuong();
        return hang;
    }

    function themThe(ds) {
        if (!ds || !ds.length) return;
        var w = document.createElement('div');
        w.className = 'eco-cb-cards';
        ds.forEach(function (c) {
            var a = document.createElement('a');
            a.className = 'eco-cb-card';
            a.href = c.url;
            a.innerHTML = (c.anh ? '<img src="' + esc(c.anh) + '" alt="" loading="lazy" referrerpolicy="no-referrer">' : '<img alt="">') +
                '<div><div class="t">' + esc(c.ten) + '</div>' +
                '<div class="m">' + esc(c.giaoVien || '') + (c.soDanhGia > 0 ? ' · ★ ' + c.diem : '') + '</div>' +
                '<div class="g">' + tien(c.gia) + '</div></div>';
            w.appendChild(a);
        });
        log.appendChild(w);
        cuonXuong();
    }

    function batDauMoi() {
        log.innerHTML = '';
        them('bot', dinhDang(LOI_CHAO));
        chips.hidden = false;
    }

    function taiLichSu() {
        daTai = true;
        fetch(root.getAttribute('data-url-lich-su'), { credentials: 'same-origin', cache: 'no-store' })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                var ds = (d && d.tinNhan) || [];
                if (!ds.length) { batDauMoi(); return; }
                log.innerHTML = '';
                ds.forEach(function (t) { them(t.vai, t.vai === 'user' ? esc(t.noiDung) : dinhDang(t.noiDung)); });
                chips.hidden = true;
            })
            .catch(batDauMoi);
    }

    function hoi(cau) {
        cau = (cau || '').trim();
        if (!cau || dang) return;
        dang = true; send.disabled = true; input.value = '';
        chips.hidden = true;
        them('user', esc(cau));
        var cho = them('bot', '<span class="eco-cb-typing" aria-label="Đang trả lời"><i></i><i></i><i></i></span>');

        guiForm(root.getAttribute('data-url-hoi'), { cauHoi: cau })
            .then(function (r) { return r.json(); })
            .then(function (d) {
                if (!d.ok) cho.className += ' err';
                cho.querySelector('.eco-cb-msg').innerHTML = dinhDang(d.reply);
                if (d.ok) themThe(d.courses);
                if (d.debug && window.console) console.debug('Trợ lý Elearning', d.debug);
            })
            .catch(function () {
                cho.className += ' err';
                cho.querySelector('.eco-cb-msg').textContent = 'Không kết nối được máy chủ, bạn thử lại nhé.';
            })
            .then(function () {
                dang = false; send.disabled = false;
                cuonXuong();
                if (mo) input.focus(); else tinMoi();
            });
    }

    /* ---------- Badge / mở - đóng ---------- */
    function capNhatBadge() {
        badge.hidden = chuaDoc === 0;
        badge.textContent = chuaDoc > 9 ? '9+' : String(chuaDoc);
        bubble.setAttribute('aria-label', mo ? 'Thu nhỏ Trợ lý Elearning' : 'Mở Trợ lý Elearning' + (chuaDoc ? ' (' + chuaDoc + ' tin chưa đọc)' : ''));
        luu(KHOA_CHUA_DOC, String(chuaDoc));
    }

    function rung() {
        bubble.classList.remove('is-ring');
        void bubble.offsetWidth;   // chạy lại hiệu ứng nếu đang rung dở
        bubble.classList.add('is-ring');
    }

    function tinMoi() {
        chuaDoc++;
        capNhatBadge();
        rung();
    }

    function moCuaSo() {
        mo = true;
        root.classList.add('is-open');
        win.setAttribute('aria-hidden', 'false');
        bubble.setAttribute('aria-expanded', 'true');
        teaser.hidden = true;
        chuaDoc = 0;
        capNhatBadge();
        if (!daTai) taiLichSu();
        setTimeout(function () { input.focus(); cuonXuong(); }, 60);
    }

    function dongCuaSo() {
        mo = false;
        root.classList.remove('is-open');
        win.setAttribute('aria-hidden', 'true');
        bubble.setAttribute('aria-expanded', 'false');
        capNhatBadge();
        bubble.focus();
    }

    bubble.addEventListener('click', function () { if (mo) dongCuaSo(); else moCuaSo(); });
    root.querySelector('[data-cb="dong"]').addEventListener('click', dongCuaSo);
    root.querySelector('[data-cb="xoa"]').addEventListener('click', function () {
        if (dang) return;
        guiForm(root.getAttribute('data-url-xoa')).then(batDauMoi, batDauMoi);
    });
    document.addEventListener('keydown', function (e) { if (mo && (e.key === 'Escape' || e.key === 'Esc')) dongCuaSo(); });

    teaser.addEventListener('click', function (e) {
        if (e.target.closest('.eco-cb-teaser-x')) { teaser.hidden = true; return; }
        moCuaSo();
    });

    form.addEventListener('submit', function (e) { e.preventDefault(); hoi(input.value); });
    chips.querySelectorAll('button').forEach(function (b) { b.addEventListener('click', function () { hoi(b.textContent); }); });

    // Lần đầu vào trang trong phiên trình duyệt: lời chào tính là 1 tin chưa đọc, hiện lời chào cạnh bong bóng
    capNhatBadge();
    if (!doc(KHOA_DA_CHAO)) {
        luu(KHOA_DA_CHAO, '1');
        setTimeout(function () {
            if (mo) return;
            teaser.hidden = false;
            tinMoi();
        }, 1500);
    }
})();
