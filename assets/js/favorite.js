/* Thả tim khóa học (nút ở Views/Shared/_FavoriteButton.cshtml, API Favorite/Toggle).
   Bắt sự kiện ở pha capture: nút nằm trong thẻ khóa học có onclick mở trang chi tiết, phải chặn trước
   để bấm tim không mở tab chi tiết.

   Đồng bộ dữ liệu sau khi tim / bỏ tim:
   - Tải lại bảng "Khóa học của tôi" trên header (Navigation/MyCoursesMenuPanel).
   - Báo cho các tab khác đang mở (BroadcastChannel, dự phòng bằng sự kiện storage): tab đó đọc lại trạng thái.
   - Quay lại trang bằng nút Back (trang lấy từ bộ nhớ đệm) hoặc chuyển về tab cũ: đọc lại trạng thái
     (Favorite/State) để trái tim, số đếm, menu và trang "Khóa học đã tim" khớp với CSDL. */
(function () {
    var toggleUrl = window.FAVORITE_TOGGLE_URL;
    var stateUrl = window.FAVORITE_STATE_URL;
    var menuUrl = window.MYCOURSES_MENU_URL;
    var menu = document.getElementById('mycourses-menu'); // chỉ có khi đã đăng nhập
    if (!toggleUrl || !menu) return;

    var CHANNEL = 'elearning-favorite';
    var channel = 'BroadcastChannel' in window ? new BroadcastChannel(CHANNEL) : null;

    function token() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function setState(courseId, liked) {
        document.querySelectorAll('[data-favorite-course="' + courseId + '"]').forEach(function (btn) {
            btn.classList.toggle('is-liked', liked);
            btn.setAttribute('aria-pressed', liked ? 'true' : 'false');
            var label = liked ? 'Bỏ tim khóa học' : 'Thả tim khóa học';
            btn.setAttribute('title', label);
            if (btn.hasAttribute('aria-label')) btn.setAttribute('aria-label', label);
        });
    }

    function setCount(count) {
        document.querySelectorAll('[data-favorite-count]').forEach(function (el) { el.textContent = count; });
    }

    function isFavoritesPage() {
        return !!document.getElementById('favorites-empty');
    }

    function showFavoritesEmptyIfNeeded() {
        if (document.querySelector('[data-favorite-item]')) return;
        var empty = document.getElementById('favorites-empty');
        var grid = document.getElementById('favorites-grid');
        if (grid) grid.style.display = 'none';
        if (empty) empty.style.display = 'block';
    }

    // Trang "Khóa học đã tim": bỏ tim thì gỡ thẻ khỏi danh sách
    function removeFromFavoritesPage(courseId) {
        var card = document.querySelector('[data-favorite-item="' + courseId + '"]');
        if (!card) return;
        card.classList.add('is-removing');
        setTimeout(function () {
            card.remove();
            showFavoritesEmptyIfNeeded();
        }, 250);
    }

    // Tải lại bảng "Khóa học của tôi" (danh sách khóa đã đăng ký / đã tim gần đây)
    function refreshMenu() {
        if (!menuUrl) return Promise.resolve();
        return fetch(menuUrl, { credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (res) { if (!res.ok) throw new Error(res.status); return res.text(); })
            .then(function (html) {
                var panel = menu.querySelector('.explore-panel');
                var box = document.createElement('div');
                box.innerHTML = html.trim();
                var fresh = box.querySelector('.explore-panel');
                if (panel && fresh) panel.replaceWith(fresh);
            })
            .catch(function () { /* giữ menu cũ nếu không tải được */ });
    }

    // Đọc trạng thái tim từ máy chủ rồi áp vào toàn bộ trang
    var syncing = null;
    var lastSync = 0;
    function syncFromServer() {
        if (!stateUrl || syncing) return syncing;
        lastSync = Date.now();
        syncing = fetch(stateUrl, { credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' } })
            .then(function (res) { return res.json(); })
            .then(function (res) {
                if (!res || !res.success) return;
                var liked = new Set(res.likedIds || []);
                var seen = new Set();
                document.querySelectorAll('[data-favorite-course]').forEach(function (btn) {
                    var id = btn.getAttribute('data-favorite-course');
                    seen.add(id);
                    setState(id, liked.has(id));
                });
                setCount(res.count);

                if (isFavoritesPage()) {
                    // Khóa vừa bỏ tim ở nơi khác: gỡ khỏi danh sách. Khóa vừa tim ở nơi khác: tải lại trang để hiện thẻ mới.
                    var conLai = 0;
                    document.querySelectorAll('[data-favorite-item]:not(.is-removing)').forEach(function (item) {
                        var id = item.getAttribute('data-favorite-item');
                        if (liked.has(id)) conLai++;
                        else removeFromFavoritesPage(id);
                    });
                    if (conLai < res.count) {
                        window.location.reload();
                        return;
                    }
                }
                return refreshMenu();
            })
            .catch(function () { })
            .finally(function () { syncing = null; });
        return syncing;
    }

    function broadcast() {
        var message = { at: Date.now() };
        if (channel) {
            channel.postMessage(message);
        } else {
            try { localStorage.setItem(CHANNEL, JSON.stringify(message)); } catch (e) { }
        }
    }

    if (channel) {
        channel.onmessage = function () { syncFromServer(); };
    } else {
        window.addEventListener('storage', function (e) { if (e.key === CHANNEL) syncFromServer(); });
    }

    // Trang lấy lại từ bộ nhớ đệm khi bấm Back / Forward: dữ liệu có thể đã cũ
    window.addEventListener('pageshow', function (e) { if (e.persisted) syncFromServer(); });

    // Chuyển về tab này sau khi thao tác ở tab khác
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible' && Date.now() - lastSync > 3000) syncFromServer();
    });

    function notify(message, type) {
        if (typeof window.showToast === 'function') window.showToast(message, type);
    }

    document.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-favorite-course]');
        if (!btn) return;
        e.preventDefault();
        e.stopPropagation();
        if (btn.disabled) return;

        var courseId = btn.getAttribute('data-favorite-course');
        var wasLiked = btn.classList.contains('is-liked');
        setState(courseId, !wasLiked); // đổi ngay trên giao diện, lỗi thì trả lại
        btn.classList.add('is-popping');
        setTimeout(function () { btn.classList.remove('is-popping'); }, 350);
        btn.disabled = true;

        var body = new URLSearchParams();
        body.append('courseId', courseId);
        body.append('__RequestVerificationToken', token());

        fetch(toggleUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8', 'X-Requested-With': 'XMLHttpRequest' },
            body: body.toString(),
            credentials: 'same-origin'
        })
            .then(function (res) { return res.json(); })
            .then(function (res) {
                if (!res.success) {
                    setState(courseId, wasLiked);
                    notify(res.message || 'Không thể thả tim lúc này!', 'warning');
                    return;
                }
                // Thành công: không hiện thông báo, trái tim đổi màu là đủ (chỉ báo khi có lỗi)
                setState(courseId, res.liked);
                setCount(res.count);
                if (!res.liked) removeFromFavoritesPage(courseId);
                lastSync = Date.now();
                refreshMenu();
                broadcast();
            })
            .catch(function () {
                setState(courseId, wasLiked);
                notify('Có lỗi xảy ra, vui lòng thử lại!', 'error');
            })
            .finally(function () { btn.disabled = false; });
    }, true);
})();
