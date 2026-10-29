"""Kiểm thử HTTP cho API học viên mới: E2 (đánh giá hữu ích), J1–J3 (báo cáo vi phạm, liên hệ), K1–K3 (Trợ lý ECO), A12 (Google).
Chạy với IIS Express trên máy, cần dữ liệu demo (mật khẩu Demo@123):
    python Tests/StudentSupportApiSmoke.py http://localhost:55123 [--send-email]
Bình chọn trên một đánh giá có sẵn (mà học viên B chưa bình chọn) và tạo báo cáo vi phạm tạm; finally xóa phiếu của B và báo cáo tạm.
--send-email: gửi thật MỘT email liên hệ tới hộp thư hỗ trợ (SmtpEmail); mặc định chỉ kiểm tra phần kiểm tra dữ liệu.
K1 gọi Gemini thật một lần (tốn một lượt hạn mức) nếu Chatbot:Enabled = true.
"""
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid

import pyodbc

base = sys.argv[1].rstrip("/") + "/api/v1"
send_email = "--send-email" in sys.argv
if urllib.parse.urlparse(base).hostname not in {"localhost", "127.0.0.1"}:
    raise ValueError("This smoke test is restricted to localhost.")

conn = pyodbc.connect("DRIVER={ODBC Driver 17 for SQL Server};SERVER=.\\SQLEXPRESS;DATABASE=QL_HETHONG_CUNGCAP_KHOAHOC;Trusted_Connection=yes;TrustServerCertificate=yes;", autocommit=True)
cur = conn.cursor()

DA_XUAT_BAN = "Đã xuất bản"
HOC_VIEN_B = "binh.tran@demo.ecolearning.vn"   # bình chọn, báo cáo, hỏi trợ lý
GIAO_VIEN = "khoa.tran@demo.ecolearning.vn"
passed = []
skipped = []


def call(method, path, body=None, token=None):
    data = None if body is None else json.dumps(body).encode()
    req = urllib.request.Request(base + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            return r.status, json.loads(r.read().decode("utf-8-sig"))
    except urllib.error.HTTPError as ex:
        raw = ex.read().decode("utf-8-sig")
        try:
            return ex.code, json.loads(raw)
        except ValueError:
            return ex.code, {"raw": raw[:300]}


def check(value, label, detail=None):
    if not value:
        raise AssertionError(label + ("" if detail is None else " -> " + json.dumps(detail, ensure_ascii=False)[:500]))
    passed.append(label)
    print("PASS:", label)


def expect(resp, status, label, code=None):
    s, body = resp
    check(s == status and (code is None or body.get("code") == code), label, {"status": s, "body": body})
    return body


def one(sql, *args):
    row = cur.execute(sql, *args).fetchone()
    return None if row is None else row[0]


def login(email):
    s, body = call("POST", "/auth/login", {"email": email, "password": "Demo@123"})
    assert s == 200, "Đăng nhập demo thất bại: " + email + " " + str(body)
    return body["data"]["token"], body["data"]["user"]["id"]


temp_review = None
tokens = []
report_course = None
user_b = None
started = one("SELECT GETDATE()")

try:
    token_b, user_b = login(HOC_VIEN_B)
    token_gv, user_gv = login(GIAO_VIEN)
    tokens += [token_b, token_gv]
    user_b, user_gv = int(user_b), int(user_gv)

    # ------------------------------------------------------------------ A12 (Google) — chỉ phần không cần token Google thật
    expect(call("POST", "/auth/google", {}), 400, "A12: thiếu idToken -> 400")
    expect(call("POST", "/auth/google", {"idToken": "abc"}), 401, "A12: idToken giả -> 401 GOOGLE_INVALID_TOKEN", "GOOGLE_INVALID_TOKEN")

    # ------------------------------------------------------------------ E2 đánh giá hữu ích
    # Đánh giá có sẵn của một học viên demo khác, khóa đang bán, B chưa bình chọn (DB có trigger chặn tạo đánh giá tạm khi chưa học 25%)
    row = cur.execute("""SELECT TOP 1 d.maDanhGia, d.maKhoaHoc, n.email FROM DanhGiaKhoaHoc d
        JOIN KhoaHoc k ON k.maKhoaHoc = d.maKhoaHoc JOIN NguoiDung n ON n.maNguoiDung = d.maHocVien
        WHERE k.trangThai = ? AND d.maHocVien <> ? AND n.email LIKE '%@demo.ecolearning.vn' AND n.trangThai = 1
          AND NOT EXISTS (SELECT 1 FROM DanhGiaHuuIch h WHERE h.maDanhGia = d.maDanhGia AND h.maNguoiDung = ?)
        ORDER BY d.ngayDanhGia DESC""", DA_XUAT_BAN, user_b, user_b).fetchone()
    assert row, "Không có đánh giá phù hợp để kiểm thử bình chọn"
    temp_review, review_course, author_email = row
    token_a, _ = login(author_email)
    tokens.append(token_a)
    base_help = one("SELECT COUNT(*) FROM DanhGiaHuuIch WHERE maDanhGia = ? AND huuIch = 1", temp_review)
    base_not = one("SELECT COUNT(*) FROM DanhGiaHuuIch WHERE maDanhGia = ? AND huuIch = 0", temp_review)

    def vote(helpful, token=token_b, review=temp_review):
        return call("POST", "/reviews/%s/helpful" % review, {} if helpful is None else {"helpful": helpful}, token)

    def db_counts():
        """Phiếu của B trên đánh giá: (hữu ích, không hữu ích) và tổng số phiếu so với ban đầu"""
        return (one("SELECT COUNT(*) FROM DanhGiaHuuIch WHERE maDanhGia = ? AND maNguoiDung = ? AND huuIch = 1", temp_review, user_b),
                one("SELECT COUNT(*) FROM DanhGiaHuuIch WHERE maDanhGia = ? AND maNguoiDung = ? AND huuIch = 0", temp_review, user_b))

    def review_item(token):
        # đánh giá có thể ở trang sau: duyệt hết các trang
        page = 1
        while True:
            s, body = call("GET", "/courses/%s/reviews?page=%d" % (review_course, page), token=token)
            assert s == 200, body
            found = next((r for r in body["data"]["items"] if r["id"] == temp_review), None)
            if found or page >= body["data"]["totalPages"]:
                return found
            page += 1

    expect(vote(True, token=None), 401, "E2: chưa đăng nhập -> 401")
    expect(vote(None), 400, "E2: thiếu helpful -> 400")
    expect(vote(True, review="KHONGCO_" + temp_review), 404, "E2: đánh giá không tồn tại -> 404", "REVIEW_NOT_FOUND")
    expect(vote(True, token=token_a), 403, "E2: tự bình chọn đánh giá của mình -> 403", "OWN_REVIEW")

    d = expect(vote(True), 200, "E2: bình chọn Hữu ích -> 200")["data"]
    check(d["myVote"] is True and d["helpfulCount"] == base_help + 1 and d["notHelpfulCount"] == base_not, "E2: số đếm sau khi chọn Hữu ích", d)
    check(db_counts() == (1, 0), "E2: DB có phiếu hữu ích của B", db_counts())

    item = review_item(token_b)
    check(item and item["myVote"] is True and item["isMine"] is False and item["helpfulCount"] == base_help + 1 and item["notHelpfulCount"] == base_not,
          "B4: người bình chọn thấy myVote = true, isMine = false", item)
    item = review_item(token_a)
    check(item and item["isMine"] is True, "B4: tác giả thấy isMine = true", item)
    item = review_item(None)
    check(item and item["isMine"] is False and item["myVote"] is None, "B4: khách thấy isMine = false, myVote = null", item)

    d = expect(vote(False), 200, "E2: đổi sang Không hữu ích -> 200")["data"]
    check(d["myVote"] is False and d["helpfulCount"] == base_help and d["notHelpfulCount"] == base_not + 1, "E2: số đếm sau khi đổi lựa chọn", d)
    check(db_counts() == (0, 1), "E2: DB đổi phiếu thành không hữu ích", db_counts())

    d = expect(vote(False), 200, "E2: gửi lại lựa chọn đang có -> bỏ bình chọn")["data"]
    check(d["myVote"] is None and d["helpfulCount"] == base_help and d["notHelpfulCount"] == base_not, "E2: số đếm sau khi bỏ bình chọn", d)
    check(db_counts() == (0, 0), "E2: DB không còn phiếu của B", db_counts())

    # ------------------------------------------------------------------ J1, J2 báo cáo vi phạm
    types = expect(call("GET", "/violation-types"), 200, "J1: danh sách loại vi phạm -> 200")["data"]
    check(isinstance(types, list) and len(types) == 6 and "Khác" in types, "J1: đủ 6 loại vi phạm như website", types)

    report_course = one("""SELECT TOP 1 dk.maKhoaHoc FROM DangKyKhoaHoc dk JOIN KhoaHoc k ON k.maKhoaHoc = dk.maKhoaHoc
        WHERE dk.maHocVien = ? AND (k.maGiaoVien IS NULL OR k.maGiaoVien <> ?)
          AND NOT EXISTS (SELECT 1 FROM BaoCaoViPham b WHERE b.maKhoaHoc = dk.maKhoaHoc AND b.maNguoiBaoCao = ? AND b.trangThai = N'Chờ xử lý')
          AND EXISTS (SELECT 1 FROM BaiHoc bh JOIN ChuongHoc ch ON ch.maChuong = bh.maChuong WHERE ch.maKhoaHoc = dk.maKhoaHoc)
        ORDER BY dk.maKhoaHoc""", user_b, user_b, user_b)
    assert report_course, "Học viên B không có khóa đã đăng ký phù hợp để báo cáo"
    lesson = one("SELECT TOP 1 bh.maBaiHoc FROM BaiHoc bh JOIN ChuongHoc ch ON ch.maChuong = bh.maChuong WHERE ch.maKhoaHoc = ? ORDER BY bh.maBaiHoc", report_course)
    other_lesson = one("SELECT TOP 1 bh.maBaiHoc FROM BaiHoc bh JOIN ChuongHoc ch ON ch.maChuong = bh.maChuong WHERE ch.maKhoaHoc <> ? ORDER BY bh.maBaiHoc", report_course)
    not_enrolled = one("""SELECT TOP 1 k.maKhoaHoc FROM KhoaHoc k WHERE k.trangThai = ? AND (k.maGiaoVien IS NULL OR k.maGiaoVien <> ?)
        AND NOT EXISTS (SELECT 1 FROM DangKyKhoaHoc dk WHERE dk.maKhoaHoc = k.maKhoaHoc AND dk.maHocVien = ?) ORDER BY k.maKhoaHoc""", DA_XUAT_BAN, user_b, user_b)
    own_course = one("SELECT TOP 1 maKhoaHoc FROM KhoaHoc WHERE maGiaoVien = ? ORDER BY maKhoaHoc", user_gv)
    valid = {"type": types[0], "description": "Video bài học bị lỗi âm thanh từ phút thứ 2 (kiểm thử API).", "lessonId": lesson}

    def report(course, body, token=token_b):
        return call("POST", "/courses/%s/reports" % course, body, token)

    expect(report(report_course, valid, token=None), 401, "J2: chưa đăng nhập -> 401")
    b = expect(report(report_course, dict(valid, type="Loại không có")), 400, "J2: loại vi phạm sai -> 400")
    check("type" in (b.get("errors") or {}), "J2: lỗi gắn vào trường type", b)
    b = expect(report(report_course, dict(valid, description="ngắn")), 400, "J2: mô tả dưới 10 ký tự -> 400")
    check("description" in (b.get("errors") or {}), "J2: lỗi gắn vào trường description", b)
    b = expect(report(report_course, dict(valid, description="x" * 2001)), 400, "J2: mô tả quá 2000 ký tự -> 400")
    expect(report("KHONGCO_" + uuid.uuid4().hex[:6], valid), 404, "J2: khóa học không tồn tại -> 404", "COURSE_NOT_FOUND")
    if not_enrolled:
        expect(report(not_enrolled, dict(valid, lessonId=None)), 403, "J2: chưa đăng ký khóa -> 403", "NOT_ENROLLED")
    if own_course:
        expect(report(own_course, dict(valid, lessonId=None), token=token_gv), 403, "J2: giáo viên báo cáo khóa của mình -> 403", "OWN_COURSE")
    if other_lesson:
        b = expect(report(report_course, dict(valid, lessonId=other_lesson)), 400, "J2: bài học không thuộc khóa -> 400")
        check("lessonId" in (b.get("errors") or {}), "J2: lỗi gắn vào trường lessonId", b)
    check(one("SELECT COUNT(*) FROM BaoCaoViPham WHERE maNguoiBaoCao = ? AND ngayTao >= ?", user_b, started) == 0,
          "J2: các yêu cầu lỗi không ghi gì vào DB")

    expect(report(report_course, valid), 200, "J2: báo cáo hợp lệ -> 200")
    row = cur.execute("SELECT loaiViPham, moTa, maBaiHoc, trangThai FROM BaoCaoViPham WHERE maKhoaHoc = ? AND maNguoiBaoCao = ? AND ngayTao >= ?",
                      report_course, user_b, started).fetchall()
    check(len(row) == 1 and row[0][0] == valid["type"] and row[0][2] == lesson and row[0][3] == "Chờ xử lý",
          "J2: DB có đúng 1 báo cáo 'Chờ xử lý' kèm bài học", [tuple(r) for r in row])
    expect(report(report_course, valid), 409, "J2: báo cáo lần 2 khi còn chờ xử lý -> 409", "REPORT_PENDING")

    # ------------------------------------------------------------------ J3 liên hệ
    b = expect(call("POST", "/contact", {}), 400, "J3: khách gửi form trống -> 400")
    check({"fullName", "email", "content"} <= set((b.get("errors") or {}).keys()), "J3: báo lỗi đủ fullName, email, content", b)
    b = expect(call("POST", "/contact", {"fullName": "Nguyễn Văn Test", "email": "sai-dinh-dang", "content": "Xin chào"}), 400, "J3: email sai định dạng -> 400")
    check("email" in (b.get("errors") or {}), "J3: lỗi gắn vào trường email", b)
    b = expect(call("POST", "/contact", {"content": "x" * 5001}, token_b), 400, "J3: nội dung quá 5000 ký tự -> 400")
    check(set((b.get("errors") or {}).keys()) == {"content"}, "J3: đã đăng nhập thì họ tên, email lấy theo tài khoản", b)
    if send_email:
        expect(call("POST", "/contact", {"subject": "[Kiểm thử API] Liên hệ từ app", "content": "Tin nhắn kiểm thử J3, vui lòng bỏ qua."}, token_b),
               200, "J3: gửi email liên hệ thật -> 200")
    else:
        skipped.append("J3 gửi email thật (thêm --send-email để chạy)")

    # ------------------------------------------------------------------ K1–K3 Trợ lý ECO
    s, b = call("GET", "/chatbot/history")
    if s == 404 and b.get("code") == "CHATBOT_DISABLED":
        check(True, "K: Chatbot:Enabled tắt -> 404 CHATBOT_DISABLED")
        skipped.append("K1–K3 (Chatbot:Enabled đang tắt)")
    else:
        check(s == 200 and b["data"]["messages"] == [], "K2: khách -> lịch sử rỗng", b)
        expect(call("POST", "/chatbot/ask", {"question": "   "}), 400, "K1: câu hỏi trống -> 400")
        expect(call("POST", "/chatbot/ask", {"question": "a" * 501}), 400, "K1: câu hỏi quá 500 ký tự -> 400")
        expect(call("DELETE", "/chatbot/history", token=token_b), 200, "K3: xóa lịch sử -> 200")
        s, b = call("POST", "/chatbot/ask", {"question": "Có khóa học lập trình Python nào cho người mới không?"}, token_b)
        if s == 200:
            d = b["data"]
            check(isinstance(d["reply"], str) and d["reply"].strip() != "" and isinstance(d["courses"], list), "K1: hỏi trợ lý -> 200 có câu trả lời", d)
            if d["courses"]:
                c = d["courses"][0]
                check({"id", "title", "thumbnail", "price", "rating", "reviewCount"} <= set(c.keys()), "K1: thẻ khóa học đủ trường", c)
            h = expect(call("GET", "/chatbot/history", token=token_b), 200, "K2: lịch sử sau khi hỏi -> 200")["data"]["messages"]
            check(len(h) == 2 and h[0]["role"] == "user" and h[1]["role"] == "model", "K2: lưu đúng 1 lượt hỏi - đáp", h)
            check(call("GET", "/chatbot/history", token=token_a)[1]["data"]["messages"] == [], "K2: tài khoản khác không thấy lịch sử của B")
            expect(call("DELETE", "/chatbot/history", token=token_b), 200, "K3: xóa lịch sử sau khi hỏi -> 200")
            check(call("GET", "/chatbot/history", token=token_b)[1]["data"]["messages"] == [], "K3: lịch sử đã trống")
        else:
            check(s == 503 and str(b.get("code", "")).startswith("CHATBOT_"), "K1: Gemini lỗi -> 503 CHATBOT_* (không phải 500)", b)
            skipped.append("K1 câu trả lời thật (Gemini trả %s)" % b.get("code"))

    print("\nOK: %d kiểm tra đạt." % len(passed))
    for s in skipped:
        print("BỎ QUA:", s)
finally:
    if temp_review and user_b:
        cur.execute("DELETE FROM DanhGiaHuuIch WHERE maDanhGia = ? AND maNguoiDung = ?", temp_review, user_b)  # trước đó B chưa bình chọn
    if user_b:
        cur.execute("DELETE FROM BaoCaoViPham WHERE maNguoiBaoCao = ? AND ngayTao >= ? AND moTa LIKE N'%(kiểm thử API)%'", user_b, started)
    for t in tokens:
        call("POST", "/auth/logout", {}, t)
    conn.close()
