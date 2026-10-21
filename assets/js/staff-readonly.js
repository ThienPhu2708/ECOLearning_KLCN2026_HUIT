/* Chế độ chỉ xem của Admin / Marketing ở trang người học (Services/StaffViewMode.cs).
   _Layout gắn class "staff-readonly" cho <body> khi tài khoản là Admin / Marketing. Script này:
   - đánh dấu data-staff-lock (làm mờ bằng assets/css/staff-readonly.css) cho mọi nút có ghi dữ liệu: mua, thêm giỏ,
     học miễn phí, thả tim, lưu mã, hữu ích / không hữu ích, đánh giá, báo cáo vi phạm; cả nút tải thêm bằng AJAX (MutationObserver);
   - chặn bấm ở pha capture trên document nên các xử lý gốc (favorite.js, coupon-wallet.js, onclick...) không chạy.
   Server vẫn chặn riêng bằng [StaffReadOnly] nếu ai đó gửi thẳng yêu cầu. */
(function () {
    var body = document.body;
    if (!body || !body.classList.contains('staff-readonly')) return;

    var THONG_BAO = body.getAttribute('data-staff-msg') || 'Tài khoản quản trị chỉ có quyền xem.';
    var GOI_Y = 'Chế độ chỉ xem của ' + (body.getAttribute('data-staff-role') || 'Quản trị viên') + ': không thể thao tác';

    var CAN_KHOA = [
        '[data-staff-lock]',
        '[data-favorite-course]',          // thả tim (thẻ khóa học, trang chi tiết)
        '[data-coupon-collect]',           // lưu mã giảm giá vào ví
        '[data-rv-vote]',                  // hữu ích / không hữu ích
        '[data-fbt-add]', '[data-fbt-add-all]',   // "Thường xuyên được mua cùng nhau"
        '#btnAddToCartMain',
        '[onclick*="buyNowAjax"]', '[onclick*="addToCartAjax"]', '[onclick*="enrollFreeAjax"]',
        '[onclick*="submitReview"]', '[onclick*="handleRatingButtonClick"]', '[onclick*="openViolationReport"]'
    ].join(',');

    function khoa(el) {
        if (el.getAttribute('data-staff-locked') === '1') return;
        el.setAttribute('data-staff-locked', '1');
        el.setAttribute('data-staff-lock', '');
        el.setAttribute('aria-disabled', 'true');
        el.setAttribute('title', GOI_Y);
        if (el.tagName === 'A') el.setAttribute('tabindex', '-1');
    }

    function danhDau(goc) {
        if (!goc || goc.nodeType !== 1) return;
        if (goc.matches && goc.matches(CAN_KHOA)) khoa(goc);
        goc.querySelectorAll(CAN_KHOA).forEach(khoa);
    }

    var lanBao = 0;
    function bao() {
        var bayGio = Date.now();
        if (bayGio - lanBao < 1500) return;   // bấm liên tục không hiện chồng thông báo
        lanBao = bayGio;
        if (typeof window.showToast === 'function') window.showToast(THONG_BAO, 'info');
        else alert(THONG_BAO);
    }

    function chan(e) {
        var el = e.target.closest && e.target.closest('[data-staff-lock]');
        if (!el) return;
        e.preventDefault();
        e.stopPropagation();
        e.stopImmediatePropagation();
        if (e.type === 'click') bao();
    }

    // Pha capture trên document chạy trước mọi xử lý của nút (kể cả onclick viết trong HTML)
    document.addEventListener('click', chan, true);
    document.addEventListener('submit', function (e) {
        if (e.target.querySelector && e.target.querySelector('[data-staff-lock][type="submit"]')) {
            e.preventDefault();
            e.stopImmediatePropagation();
            bao();
        }
    }, true);

    danhDau(document.body);
    new MutationObserver(function (ds) {
        ds.forEach(function (m) { m.addedNodes.forEach(danhDau); });
    }).observe(document.body, { childList: true, subtree: true });
})();
