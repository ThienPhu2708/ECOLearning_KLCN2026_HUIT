(function () {
    'use strict';
    var sidebar = document.getElementById('admin-sidebar');
    var toggle = document.getElementById('admin-menu-toggle');
    var backdrop = document.getElementById('admin-menu-backdrop');
    var mobile = window.matchMedia('(max-width: 992px)');
    if (!sidebar || !toggle || !backdrop) return;
    function setOpen(open, restoreFocus) {
        open = open && mobile.matches;
        sidebar.classList.toggle('active', open);
        toggle.setAttribute('aria-expanded', String(open));
        toggle.setAttribute('aria-label', open ? 'Đóng menu quản trị' : 'Mở menu quản trị');
        backdrop.hidden = !open;
        document.body.classList.toggle('admin-menu-open', open);
        sidebar.inert = mobile.matches && !open;
        if (open) (sidebar.querySelector('.sidebar-item.active') || sidebar.querySelector('a')).focus();
        else if (restoreFocus) toggle.focus();
    }
    toggle.addEventListener('click', function () { setOpen(toggle.getAttribute('aria-expanded') !== 'true', true); });
    backdrop.addEventListener('click', function () { setOpen(false, true); });
    document.addEventListener('keydown', function (event) {
        if (toggle.getAttribute('aria-expanded') !== 'true') return;
        if (event.key === 'Escape') { setOpen(false, true); return; }
        if (event.key !== 'Tab') return;
        var items = sidebar.querySelectorAll('a[href], button:not([disabled])');
        var first = items[0], last = items[items.length - 1];
        if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
        else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    });
    sidebar.addEventListener('click', function (event) {
        if (event.target.closest('a[href], .sidebar-footer button')) setOpen(false, false);
    });
    function syncMenu() { setOpen(false, false); }
    if (mobile.addEventListener) mobile.addEventListener('change', syncMenu);
    else mobile.addListener(syncMenu);
    syncMenu();

    // Tables retain readable columns; only their own viewport scrolls horizontally.
    document.querySelectorAll('.admin-page-content table').forEach(function (table) {
        if (!table.tHead) return;
        if (table.closest('.table-responsive, .rv-scroll, .ss-table-wrap, .admin-table-scroll')) return;
        var wrapper = document.createElement('div');
        wrapper.className = 'admin-table-scroll';
        table.parentNode.insertBefore(wrapper, table);
        wrapper.appendChild(table);
    });
    document.querySelectorAll('.admin-page-content .table-responsive, .admin-table-scroll, .rv-scroll, .ss-table-wrap').forEach(function (wrapper) {
        wrapper.setAttribute('tabindex', '0');
        wrapper.setAttribute('role', 'region');
        wrapper.setAttribute('aria-label', 'Bảng dữ liệu, cuộn ngang để xem đầy đủ');
    });
})();
