/* Gợi ý khi gõ ở thanh tìm kiếm (header): GET /Search/Suggest?q= → tối đa 7 từ khóa, 5 khóa học, 2 giáo viên.
   - Chờ người dùng ngừng gõ 180 ms rồi mới gọi; hủy request cũ (AbortController); bỏ kết quả về muộn.
   - Cache 30 câu gần nhất trong trang.
   - Bàn phím: ↑ ↓ chọn, Enter mở mục đang chọn (không chọn thì tìm cả câu), Esc đóng. ARIA combobox / listbox.
   - Dựng DOM bằng textContent, không chèn HTML từ dữ liệu. */
(function () {
    var form = document.querySelector('.header-search form');
    if (!form) return;
    var input = form.querySelector('input[name="q"]');
    if (!input) return;

    var SUGGEST_URL = window.SEARCH_SUGGEST_URL || '/Search/Suggest';
    var SEARCH_URL = form.getAttribute('action') || '/tim-kiem';
    var DELAY = 180;
    var MAX_CACHE = 30;

    var box = document.createElement('div');
    box.className = 'search-suggest';
    box.id = 'search-suggest-list';
    box.setAttribute('role', 'listbox');
    box.setAttribute('aria-label', 'Gợi ý tìm kiếm');
    box.hidden = true;
    form.parentNode.appendChild(box);

    input.setAttribute('role', 'combobox');
    input.setAttribute('aria-autocomplete', 'list');
    input.setAttribute('aria-controls', box.id);
    input.setAttribute('aria-expanded', 'false');
    input.setAttribute('autocomplete', 'off');

    var cache = new Map();
    var timer = null;
    var controller = null;
    var seq = 0;
    var activeIndex = -1;

    function normalize(q) { return q.replace(/\s+/g, ' ').trim(); }

    function searchUrl(q) { return SEARCH_URL + '?q=' + encodeURIComponent(q); }

    function options() { return Array.prototype.slice.call(box.querySelectorAll('[role="option"]')); }

    // Hộp rộng hơn ô nhập: dời sang trái nếu tràn mép phải màn hình (màn hình hẹp)
    function place() {
        box.style.left = '0px';
        var r = box.getBoundingClientRect();
        var vw = document.documentElement.clientWidth;
        var shift = 0;
        if (r.right > vw - 12) shift = vw - 12 - r.right;
        if (r.left + shift < 12) shift = 12 - r.left;
        box.style.left = shift + 'px';
    }

    function open() {
        if (!box.childNodes.length) return close();
        box.hidden = false;
        input.setAttribute('aria-expanded', 'true');
        place();
    }

    window.addEventListener('resize', function () { if (!box.hidden) place(); });

    function close() {
        box.hidden = true;
        input.setAttribute('aria-expanded', 'false');
        input.removeAttribute('aria-activedescendant');
        activeIndex = -1;
    }

    function setActive(i) {
        var opts = options();
        opts.forEach(function (o) { o.classList.remove('is-active'); o.setAttribute('aria-selected', 'false'); });
        if (i < 0 || i >= opts.length) {
            activeIndex = -1;
            input.removeAttribute('aria-activedescendant');
            return;
        }
        activeIndex = i;
        opts[i].classList.add('is-active');
        opts[i].setAttribute('aria-selected', 'true');
        input.setAttribute('aria-activedescendant', opts[i].id);
        opts[i].scrollIntoView({ block: 'nearest' });
    }

    function el(tag, cls, text) {
        var e = document.createElement(tag);
        if (cls) e.className = cls;
        if (text != null) e.textContent = text;
        return e;
    }

    var optionId = 0;
    function option(href, className) {
        var a = el('a', 'ss-item' + (className ? ' ' + className : ''));
        a.href = href;
        a.id = 'ss-opt-' + (++optionId);
        a.setAttribute('role', 'option');
        a.setAttribute('aria-selected', 'false');
        a.tabIndex = -1;
        return a;
    }

    /* Phần người dùng đã gõ (không dấu) giữ đậm, phần còn lại nhạt: tô theo vị trí trên chuỗi đã bỏ dấu */
    function stripAccents(s) {
        return s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();
    }

    function keywordText(text, q) {
        var span = el('span', 'ss-keyword');
        var plain = stripAccents(text), needle = stripAccents(q);
        var at = needle ? plain.indexOf(needle) : -1;
        // Chuỗi bỏ dấu (NFC đầu vào) có cùng độ dài với chuỗi gốc nên cắt theo vị trí được
        if (at < 0 || plain.length !== text.length) { span.textContent = text; return span; }
        if (at > 0) span.appendChild(el('mark', null, text.slice(0, at)));
        span.appendChild(document.createTextNode(text.slice(at, at + needle.length)));
        if (at + needle.length < text.length) span.appendChild(el('mark', null, text.slice(at + needle.length)));
        return span;
    }

    function initials(name) {
        var parts = (name || '').trim().split(/\s+/);
        return (parts.length ? parts[parts.length - 1].charAt(0) : '?').toUpperCase();
    }

    function render(q, data) {
        box.textContent = '';
        activeIndex = -1;
        var has = false;

        (data.TuKhoa || []).forEach(function (k) {
            var a = option(k.Url);
            var icon = el('span', 'ss-icon'); icon.appendChild(el('i', 'bi bi-search'));
            a.appendChild(icon);
            a.appendChild(keywordText(k.Text, q));
            box.appendChild(a);
            has = true;
        });

        if ((data.KhoaHoc || []).length && has) box.appendChild(el('div', 'ss-sep'));
        (data.KhoaHoc || []).forEach(function (c) {
            var a = option(c.Url);
            var img = el('img', 'ss-thumb');
            img.alt = '';
            img.loading = 'lazy';
            if (c.Anh) img.src = c.Anh;
            a.appendChild(img);
            var t = el('div', 'ss-text');
            t.appendChild(el('div', 'ss-title', c.Ten));
            var m = el('div', 'ss-meta');
            m.appendChild(el('strong', null, 'Khóa học'));
            m.appendChild(document.createTextNode(c.GiaoVien || ''));
            t.appendChild(m);
            a.appendChild(t);
            box.appendChild(a);
            has = true;
        });

        (data.GiaoVien || []).forEach(function (g) {
            var a = option(g.Url);
            if (g.Anh) {
                var img = el('img', 'ss-avatar'); img.alt = ''; img.src = g.Anh; img.loading = 'lazy';
                a.appendChild(img);
            } else {
                a.appendChild(el('div', 'ss-avatar-fallback', initials(g.Ten)));
            }
            var t = el('div', 'ss-text');
            t.appendChild(el('div', 'ss-title', g.Ten));
            t.appendChild(el('div', 'ss-meta', 'Giảng viên · ' + g.SoKhoaHoc + ' khóa học'));
            a.appendChild(t);
            box.appendChild(a);
            has = true;
        });

        if (has) {
            box.appendChild(el('div', 'ss-sep'));
            var all = option(searchUrl(q), 'ss-all');
            all.className = 'ss-all';
            all.textContent = 'Xem tất cả kết quả cho “' + q + '”';
            box.appendChild(all);
        } else {
            box.appendChild(el('div', 'ss-empty', 'Không có gợi ý cho “' + q + '”. Nhấn Enter để tìm.'));
        }
        open();
    }

    function remember(q, data) {
        if (cache.size >= MAX_CACHE) cache.delete(cache.keys().next().value);
        cache.set(q, data);
    }

    function fetchSuggest(q) {
        var key = q.toLowerCase();
        if (cache.has(key)) { render(q, cache.get(key)); return; }
        if (controller) controller.abort();
        controller = ('AbortController' in window) ? new AbortController() : null;
        var mySeq = ++seq;
        fetch(SUGGEST_URL + '?q=' + encodeURIComponent(q), {
            credentials: 'same-origin',
            headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
            signal: controller ? controller.signal : undefined
        })
            .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
            .then(function (data) {
                if (mySeq !== seq) return;               // đã có câu mới hơn
                remember(key, data);
                if (normalize(input.value) === q) render(q, data);
            })
            .catch(function (e) { if (e.name !== 'AbortError') close(); });
    }

    input.addEventListener('input', function () {
        clearTimeout(timer);
        var q = normalize(input.value);
        if (!q) { seq++; if (controller) controller.abort(); close(); box.textContent = ''; return; }
        timer = setTimeout(function () { fetchSuggest(q); }, DELAY);
    });

    input.addEventListener('focus', function () {
        var q = normalize(input.value);
        if (q && box.childNodes.length) open();
        else if (q) fetchSuggest(q);
    });

    input.addEventListener('keydown', function (e) {
        var opts = options();
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            if (box.hidden) { open(); }
            if (opts.length) setActive(activeIndex + 1 >= opts.length ? 0 : activeIndex + 1);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            if (opts.length) setActive(activeIndex - 1 < 0 ? opts.length - 1 : activeIndex - 1);
        } else if (e.key === 'Enter') {
            if (!box.hidden && activeIndex >= 0 && opts[activeIndex]) {
                e.preventDefault();
                window.location.href = opts[activeIndex].href;
            }
        } else if (e.key === 'Escape') {
            if (!box.hidden) { e.preventDefault(); close(); }
        }
    });

    // Không gửi câu rỗng
    form.addEventListener('submit', function (e) {
        var q = normalize(input.value);
        if (!q) { e.preventDefault(); input.focus(); return; }
        input.value = q;
    });

    // Bấm ra ngoài thì đóng; giữ focus khi bấm trong hộp (mousedown trước blur)
    box.addEventListener('mousedown', function (e) { e.preventDefault(); });
    document.addEventListener('click', function (e) {
        if (!form.parentNode.contains(e.target)) close();
    });
})();
