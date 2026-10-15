/* Menu thả xuống nhiều tầng trên header: "Khám phá" (_ExploreMenu) và "Khóa học của tôi" (_MyCoursesMenu).
   Di chuột / focus vào một dòng thì mở khung tương ứng ở cột kế tiếp.
   Bắt sự kiện ở thẻ .explore-menu (event delegation) và tìm cột tại thời điểm xảy ra sự kiện, nên khi nội dung
   menu được tải lại (favorite.js thay bảng "Khóa học của tôi" sau khi thả tim) menu vẫn hoạt động. */
(function () {
    function closeCol(col) {
        if (!col) return;
        col.classList.remove('is-open');
        col.querySelectorAll('.is-active').forEach(function (el) { el.classList.remove('is-active'); });
    }

    // Đánh dấu dòng đang chọn và mở khung tương ứng ở cột kế tiếp
    function activate(row, key, col, colAfter) {
        if (!col || row.classList.contains('is-active')) return;
        row.parentElement.querySelectorAll(':scope > .explore-row.is-active').forEach(function (el) { el.classList.remove('is-active'); });
        row.classList.add('is-active');
        closeCol(colAfter);
        col.querySelectorAll('.explore-sub').forEach(function (p) {
            p.classList.toggle('is-active', p.getAttribute('data-explore-panel') === key);
        });
        col.classList.add('is-open');
    }

    function init(menu) {
        if (menu.getAttribute('data-ready')) return;
        menu.setAttribute('data-ready', '1');

        function onEnter(e) {
            var row = e.target.closest('[data-explore-cat], [data-explore-level]');
            if (!row || !menu.contains(row)) return;
            var col2 = menu.querySelector('.explore-col-2');
            var col3 = menu.querySelector('.explore-col-3');
            if (row.hasAttribute('data-explore-cat')) {
                activate(row, row.getAttribute('data-explore-cat'), col2, col3);
            } else {
                activate(row, row.getAttribute('data-explore-level'), col3, null);
            }
        }

        menu.addEventListener('mouseover', onEnter);
        menu.addEventListener('focusin', onEnter);

        // Rời menu: đóng các tầng sau để lần mở sau bắt đầu lại từ tầng 1
        menu.addEventListener('mouseleave', function () {
            closeCol(menu.querySelector('.explore-col-3'));
            closeCol(menu.querySelector('.explore-col-2'));
            menu.querySelectorAll('[data-explore-cat].is-active').forEach(function (el) { el.classList.remove('is-active'); });
        });
    }

    document.querySelectorAll('.explore-menu').forEach(init);
})();
