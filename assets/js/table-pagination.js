/**
 * table-pagination.js — Phân trang phía trình duyệt cho bảng trong khu vực Admin & Giáo viên.
 *
 * Cách dùng: thêm data-paginate="10" vào <table> (số dòng mỗi trang).
 * - Chỉ đếm các dòng dữ liệu trong <tbody>; dòng thông báo "không có dữ liệu" (1 ô colspan) được bỏ qua.
 * - Tôn trọng bộ lọc / tìm kiếm sẵn có của trang: dòng bị ẩn bằng style.display = 'none'
 *   hoặc thuộc tính hidden được coi là không khớp bộ lọc. Khi bộ lọc thay đổi, phân trang tự tính lại
 *   và quay về trang 1.
 * - Thanh phân trang dùng lại class .pagination-container / .pagination / .page-item / .page-link.
 */
(function () {
  'use strict';

  var HIDE_CLASS = 'tp-page-hidden';

  function injectStyle() {
    if (document.getElementById('tp-style')) return;
    var style = document.createElement('style');
    style.id = 'tp-style';
    style.textContent =
      'tr.' + HIDE_CLASS + '{display:none !important;}' +
      '.tp-pager[hidden]{display:none !important;}' +
      '.tp-pager{margin-top:16px;gap:12px;flex-wrap:wrap;}' +
      '.tp-pager .page-link{cursor:pointer;min-width:36px;}' +
      '.tp-pager .page-item.disabled .page-link{cursor:default;}' +
      '.tp-pager .tp-ellipsis{padding:0 4px;color:var(--text-muted,#94A3B8);}';
    document.head.appendChild(style);
  }

  function isDataRow(row) {
    var cells = row.cells;
    return !(cells.length === 1 && cells[0].colSpan > 1);
  }

  function matchesFilter(row) {
    return !row.hidden && row.style.display !== 'none';
  }

  function pageList(current, total) {
    // 1 … 4 5 [6] 7 8 … 20
    var pages = [];
    for (var p = 1; p <= total; p++) {
      if (p === 1 || p === total || Math.abs(p - current) <= 2) pages.push(p);
      else if (pages[pages.length - 1] !== '…') pages.push('…');
    }
    return pages;
  }

  function setup(table) {
    if (table.dataset.tpReady) return;
    table.dataset.tpReady = '1';

    var pageSize = parseInt(table.getAttribute('data-paginate'), 10) || 10;
    var tbody = table.tBodies[0];
    if (!tbody) return;

    var pager = document.createElement('div');
    pager.className = 'pagination-container tp-pager';
    // Đặt thanh phân trang ngay dưới khung chứa bảng (không nằm trong vùng cuộn ngang của bảng)
    var anchor = table.closest('.table-responsive')
      || (table.parentElement && table.parentElement.children.length === 1 ? table.parentElement : table);
    anchor.parentNode.insertBefore(pager, anchor.nextSibling);

    var state = { page: 1, signature: '' };

    function render() {
      var rows = Array.prototype.filter.call(tbody.rows, isDataRow);
      var visible = rows.filter(matchesFilter);
      var signature = visible.length + ':' + rows.length;
      if (signature !== state.signature) {
        state.signature = signature;
        state.page = 1;
      }

      var total = visible.length;
      var totalPages = Math.max(1, Math.ceil(total / pageSize));
      if (state.page > totalPages) state.page = totalPages;

      var start = (state.page - 1) * pageSize;
      var end = start + pageSize;
      rows.forEach(function (row) { row.classList.add(HIDE_CLASS); });
      visible.slice(start, end).forEach(function (row) { row.classList.remove(HIDE_CLASS); });

      // Ít hơn 1 trang thì không cần thanh phân trang
      if (total <= pageSize) {
        rows.forEach(function (row) { row.classList.remove(HIDE_CLASS); });
        pager.hidden = true;
        return;
      }
      pager.hidden = false;

      var html = '<div class="pagination-info">Hiển thị <strong>' + (start + 1) + ' - ' + Math.min(end, total) +
        '</strong> trên tổng số <strong>' + total + '</strong> dòng</div><ul class="pagination">';
      html += '<li class="page-item' + (state.page <= 1 ? ' disabled' : '') + '"><button type="button" class="page-link" data-page="' + (state.page - 1) + '">&lsaquo; Trước</button></li>';
      pageList(state.page, totalPages).forEach(function (p) {
        if (p === '…') html += '<li class="tp-ellipsis">…</li>';
        else html += '<li class="page-item' + (p === state.page ? ' active' : '') + '"><button type="button" class="page-link" data-page="' + p + '">' + p + '</button></li>';
      });
      html += '<li class="page-item' + (state.page >= totalPages ? ' disabled' : '') + '"><button type="button" class="page-link" data-page="' + (state.page + 1) + '">Sau &rsaquo;</button></li></ul>';
      pager.innerHTML = html;
    }

    pager.addEventListener('click', function (e) {
      var btn = e.target.closest('[data-page]');
      if (!btn || btn.closest('.disabled')) return;
      var target = parseInt(btn.getAttribute('data-page'), 10);
      if (!isNaN(target)) {
        state.page = target;
        render();
        table.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
      }
    });

    // Bộ lọc của trang đổi style.display / hidden của dòng, hoặc thêm/bớt dòng -> tính lại
    var pending = false;
    var observer = new MutationObserver(function () {
      if (pending) return;
      pending = true;
      requestAnimationFrame(function () {
        pending = false;
        observer.disconnect();
        render();
        observe();
      });
    });
    function observe() {
      observer.observe(tbody, { childList: true, subtree: true, attributes: true, attributeFilter: ['style', 'hidden'] });
    }

    render();
    observe();
  }

  function init() {
    injectStyle();
    document.querySelectorAll('table[data-paginate]').forEach(setup);
  }

  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
  else init();

  window.TablePagination = { init: init };
})();
