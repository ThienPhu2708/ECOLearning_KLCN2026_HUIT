// Ô nhập một mục tiêu đầu ra (trang Tạo / Sửa khóa học của giáo viên).
// Là textarea tự giãn chiều cao theo nội dung nên câu dài vẫn đọc được hết,
// nhưng không cho xuống dòng: khi lưu, mỗi mục tiêu là một dòng của cột mucTieuDauRa.
window.ecoOutcomeField = function (el, onEnter) {
    el.rows = 1;
    el.maxLength = 250;
    el.style.minHeight = '0';
    el.style.resize = 'none';
    el.style.overflow = 'hidden';
    el.style.lineHeight = '1.5';
    el.style.padding = '10px 16px';

    function fit() {
        el.style.height = 'auto';
        el.style.height = (el.scrollHeight + el.offsetHeight - el.clientHeight) + 'px';
    }

    el.addEventListener('keydown', function (e) {
        if (e.key === 'Enter') {
            e.preventDefault();
            if (onEnter) onEnter();
        }
    });
    el.addEventListener('input', function () {
        if (/[\r\n]/.test(el.value)) el.value = el.value.replace(/\s*[\r\n]+\s*/g, ' ');
        fit();
    });

    // Đo lại khi ô đổi bề rộng (đổi cỡ cửa sổ, hoặc bước wizard đang ẩn được hiện ra).
    if (window.ResizeObserver) {
        var rong = -1;
        new ResizeObserver(function () {
            if (el.clientWidth !== rong) { rong = el.clientWidth; fit(); }
        }).observe(el);
    }
    fit();
};
