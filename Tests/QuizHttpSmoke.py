"""HTTP flow checks against a local IIS instance; requires the existing demo seed.
Run with Recommender/.venv/Scripts/python.exe Tests/QuizHttpSmoke.py http://localhost:55123
Creates one isolated temporary course, then removes it in finally.
"""
import http.cookiejar
import io
import json
import sys
import urllib.error
import urllib.parse
import urllib.request
import uuid
import zipfile
from pathlib import Path
from html.parser import HTMLParser
from html import unescape

import pyodbc

base = sys.argv[1].rstrip("/")
if urllib.parse.urlparse(base).hostname not in {"localhost", "127.0.0.1"}:
    raise ValueError("This smoke test is restricted to localhost.")
course = "QHTTP" + uuid.uuid4().hex[:10]
conn = pyodbc.connect("DRIVER={ODBC Driver 17 for SQL Server};SERVER=.\\SQLEXPRESS;DATABASE=QL_HETHONG_CUNGCAP_KHOAHOC;Trusted_Connection=yes;TrustServerCertificate=yes;")


class Inputs(HTMLParser):
    def __init__(self, html):
        super().__init__()
        self.values = {}
        self.feed(html)

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "input" and "name" in attrs:
            self.values[attrs["name"]] = attrs.get("value", "")


def session():
    return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))


def get(client, path):
    global last_html
    response = client.open(base + path, timeout=30)
    last_html = unescape(response.read().decode("utf-8-sig"))
    return last_html, response.url


def post(client, path, values, token):
    global last_html
    values = dict(values, __RequestVerificationToken=token)
    try:
        response = client.open(base + path, urllib.parse.urlencode(values, doseq=True).encode(), timeout=30)
    except urllib.error.HTTPError as ex:
        (Path(__file__).resolve().parents[2] / "tmp" / "quiz-http-failure.html").write_bytes(ex.read())
        raise
    last_html = unescape(response.read().decode("utf-8-sig"))
    return last_html, response.url


def token(html):
    return Inputs(html).values["__RequestVerificationToken"]


def login(client, email):
    html, _ = get(client, "/Account/Login")
    _, url = post(client, "/Account/Login", {"Email": email, "Password": "Demo@123"}, token(html))
    assert "/Account/Login" not in url, "Demo sign-in failed"


def check(value, label):
    if not value and "last_html" in globals():
        (Path(__file__).resolve().parents[2] / "tmp" / "quiz-http-failure.html").write_text(last_html, encoding="utf-8")
    assert value, label
    print("PASS:", label)


try:
    cur = conn.cursor()
    teacher = cur.execute("SELECT maNguoiDung FROM NguoiDung WHERE email='khoa.tran@demo.ecolearning.vn'").fetchone()[0]
    learner = cur.execute("SELECT maNguoiDung FROM NguoiDung WHERE email='an.nguyen@demo.ecolearning.vn'").fetchone()[0]
    cur.execute("INSERT KhoaHoc(maKhoaHoc,tenKhoaHoc,giaBan,trangThai,maDanhMuc,maGiaoVien,capDo) SELECT ?,N'Quiz HTTP isolated test',0,N'Đã xuất bản',maDanhMuc,?,N'Cơ bản' FROM KhoaHoc WHERE maKhoaHoc='DMKH01'", course, teacher)
    cur.execute("INSERT ChuongHoc(maChuong,tenChuong,thuTu,maKhoaHoc) VALUES(?,N'Test',1,?)", course + "C", course)
    cur.execute("INSERT BaiHoc(maBaiHoc,tenBaiHoc,thuTu,maChuong,thoiLuong) VALUES(?,N'Test lesson',1,?,60)", course + "_1", course + "C")
    cur.execute("INSERT DangKyKhoaHoc(maDangKy,maHocVien,maKhoaHoc,ngayDangKy,trangThaiDK) VALUES(?,?,?,GETDATE(),N'Đang học')", course + "DK", learner, course)
    cur.execute("INSERT TienDoBaiHoc(maHocVien,maBaiHoc,daHoanThanh,ngayHoanThanh) VALUES(?,?,1,GETDATE())", learner, course + "_1")
    conn.commit()
    teacher_session, student_session = session(), session()
    login(teacher_session, "khoa.tran@demo.ecolearning.vn")
    html, _ = get(teacher_session, "/Instructor/Quiz/Edit?courseId=" + course)
    html, _ = post(teacher_session, "/Instructor/Quiz/Edit", {
        "Id": 0, "CourseId": course, "Topic": "HTTP test", "Difficulty": 1,
        "Text": "C# dùng từ khóa nào để khai báo lớp?", "Options[0]": "class", "Options[1]": "select",
        "Options[2]": "table", "Options[3]": "print", "CorrectOption": 0,
        "ShuffleOptions": "true", "Explanation": "class khai báo lớp.",
    }, token(html))
    check("Đã lưu câu hỏi" in html, "MVC question form saves successfully")
    template = teacher_session.open(base + "/Instructor/Quiz/Template", timeout=30).read()
    check(zipfile.is_zipfile(io.BytesIO(template)), "template is a real XLSX workbook")
    html, _ = get(teacher_session, "/Instructor/Quiz/Import?courseId=" + course)
    boundary = "QuizBoundary" + uuid.uuid4().hex
    parts = []
    for name, value in {"courseId": course, "__RequestVerificationToken": token(html)}.items():
        parts.append((f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n').encode())
    parts.append((f'--{boundary}\r\nContent-Disposition: form-data; name="upload"; filename="questions.xlsx"\r\nContent-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet\r\n\r\n').encode() + template + b"\r\n")
    parts.append((f"--{boundary}--\r\n").encode())
    request = urllib.request.Request(base + "/Instructor/Quiz/Import", b"".join(parts), {"Content-Type": "multipart/form-data; boundary=" + boundary})
    html = teacher_session.open(request, timeout=30).read().decode("utf-8-sig")
    import_token = Inputs(html).values.get("token")
    check(bool(import_token), "Excel upload produces confirmation preview")
    html, _ = post(teacher_session, "/Instructor/Quiz/ConfirmImport", {"courseId": course, "token": import_token}, token(html))
    check("Đã nhập 1 câu hỏi" in html, "confirmed Excel rows are imported")
    html, _ = get(teacher_session, "/Instructor/Quiz/Configure?courseId=" + course)
    html, _ = post(teacher_session, "/Instructor/Quiz/Configure", {
        "CourseId": course, "Title": "HTTP quiz", "Minutes": 5, "PassPercent": 80, "MaxAttempts": 1,
        "EasyCount": 2, "MediumCount": 0, "HardCount": 0, "RandomSelection": "true",
        "ShuffleQuestions": "true", "ShuffleAnswers": "true", "Published": "true",
    }, token(html))
    check("Đã lưu cấu hình" in html, "MVC configuration publishes valid quiz")
    html, _ = get(teacher_session, "/Instructor/Quiz/Preview?courseId=" + course)
    check("HTTP quiz" in html and "Câu 02" in html, "preview renders selected question structure")
    login(student_session, "an.nguyen@demo.ecolearning.vn")
    html, _ = get(student_session, "/Learning/Study?courseId=" + course)
    check("100% (1/1 bài)" in html, "study progress only counts lessons in this course")
    try:
        post(student_session, "/Learning/UpdateProgress", {"courseId": course, "lessonId": "DMKH01_1", "durationWatched": 60, "totalDuration": 60}, token(html))
        raise AssertionError("Cross-course lesson update unexpectedly succeeded")
    except urllib.error.HTTPError as ex:
        check(ex.code == 400, "progress update rejects a lesson from another course")
    progress, _ = post(student_session, "/Learning/UpdateProgress", {"courseId": course, "lessonId": course + "_1", "durationWatched": 60, "totalDuration": 60}, token(html))
    check(json.loads(progress)["success"], "enrolled learner can still save valid video progress")
    anonymous = session()
    anon_html, anon_url = get(anonymous, "/Learning/Study?courseId=" + course)
    check("/Account/Login" in anon_url, "anonymous study does not assume an existing learner identity")
    try:
        post(anonymous, "/Learning/UpdateProgress", {"courseId": course, "lessonId": course + "_1", "durationWatched": 60, "totalDuration": 60}, token(anon_html))
        raise AssertionError("Anonymous progress update unexpectedly succeeded")
    except urllib.error.HTTPError as ex:
        check(ex.code == 403, "anonymous request cannot write another learner's progress")
    teacher_html, _ = get(teacher_session, "/Learning/Study?courseId=" + course)
    try:
        post(teacher_session, "/Learning/UpdateProgress", {"courseId": course, "lessonId": course + "_1", "durationWatched": 60, "totalDuration": 60}, token(teacher_html))
        raise AssertionError("Preview unexpectedly enrolled course owner")
    except urllib.error.HTTPError as ex:
        check(ex.code == 403, "course preview cannot auto-enroll or record learner progress")
    html, _ = get(student_session, "/Quiz/Index?courseId=" + course)
    html, url = post(student_session, "/Quiz/Start", {"courseId": course}, token(html))
    attempt_id = int(urllib.parse.urlparse(url).path.rsplit("/", 1)[-1])
    check("quiz-time" in html and "CorrectOption" not in html and "SnapshotJson" not in html, "live exam renders without exposing answer key")
    check('data-question-link="0"' in html and 'id="quiz-question-0"' in html and "quiz-answer-count" in html, "live exam has question navigation and answer count")
    snapshot = json.loads(cur.execute("SELECT SnapshotJson FROM QuizAttempt WHERE Id=?", attempt_id).fetchone()[0])
    conn.commit()
    html, _ = post(student_session, "/Quiz/Submit", dict({f"answers[{i}]": -1 for i in range(len(snapshot))}, id=attempt_id), token(html))
    check("Chưa đạt" in html and "Giải thích:" not in html and "✓" not in html, "failed final attempt does not release answer key before a retake")
    html, _ = get(teacher_session, f"/Instructor/Quiz/History?courseId={course}&studentId={learner}")
    check("Cấp thêm 1 lượt" in html, "teacher can review a learner who exhausted attempts")
    html, _ = post(teacher_session, "/Instructor/Quiz/GrantAttempt", {"courseId": course, "studentId": learner, "reason": "HTTP retake review"}, token(html))
    check("Đã cấp thêm một lượt" in html, "teacher grants one learner a recorded extra attempt")
    html, _ = get(student_session, "/Quiz/Index?courseId=" + course)
    html, url = post(student_session, "/Quiz/Start", {"courseId": course}, token(html))
    attempt_id = int(urllib.parse.urlparse(url).path.rsplit("/", 1)[-1])
    snapshot = json.loads(cur.execute("SELECT SnapshotJson FROM QuizAttempt WHERE Id=?", attempt_id).fetchone()[0])
    conn.commit()
    answers = {f"answers[{i}]": q["CorrectOption"] for i, q in enumerate(snapshot)}
    payload = dict(answers, id=attempt_id)
    saved, _ = post(student_session, "/Quiz/SaveDraft", payload, token(html))
    check(json.loads(saved)["saved"], "draft endpoint accepts authenticated answers")
    html, _ = post(student_session, "/Quiz/Submit", payload, token(html))
    check("Đã đạt" in html and "2/2 câu đúng" in html, "student submit grades and displays result")
    html, url = get(student_session, "/Learning/Certificate?courseId=" + course)
    check("/Quiz" in url, "certificate URL requires explicit learner claim")
    html, claim_url = post(student_session, "/Quiz/ClaimCertificate", {"courseId": course}, token(html))
    check("/Learning/Certificate" in claim_url and "CERT-" in html, "learner claim immediately issues and opens certificate")
    html, gradebook_url = get(teacher_session, "/Instructor/Quiz/Results?courseId=" + course)
    check("/Instructor/Student" in gradebook_url and 'href="/Instructor/Quiz/Results' not in html, "gradebook and sidebar consolidate into student management")
    check(("10<small>/10</small>" in html) and "Đã cấp" in html and "Chờ cấp" not in html, "gradebook immediately shows issued status without teacher approval")
    check("Điểm trung bình /10" in html and "Xuất lịch sử thi" in html, "merged student page includes score statistics and both exports")
    html, _ = get(teacher_session, "/Instructor/Student?courseId=" + course + "&status=issued")
    check("an.nguyen@demo.ecolearning.vn" in html, "issued-certificate filter keeps matching learner-course row")
    html, _ = get(teacher_session, "/Instructor/Student?courseId=" + course + "&status=failed")
    check("an.nguyen@demo.ecolearning.vn" not in html, "failed-quiz filter excludes a learner who already passed")
    html, _ = get(teacher_session, "/Instructor/Student?courseId=" + course)
    check("Đã đạt" in html and "Đã cấp" in html, "existing student management shows quiz and certificate status")
    html, _ = get(student_session, "/Learning/Certificate?courseId=" + course)
    check("CERT-" in html and "Quiz HTTP isolated test" in html, "issued certificate is accessible with persistent code")
    cur.execute("INSERT BaiHoc(maBaiHoc,tenBaiHoc,thuTu,maChuong,thoiLuong) VALUES(?,N'Added after certificate',2,?,60)", course + "_2", course + "C")
    conn.commit()
    html, _ = get(student_session, "/Learning/Certificate?courseId=" + course)
    check("CERT-" in html and "Quiz HTTP isolated test" in html, "issued certificate survives newly added course content")
    html, _ = get(teacher_session, f"/Instructor/Quiz/History?courseId={course}&studentId={learner}")
    check("Tiến độ từng bài học" in html, "teacher sees attempt history and lesson progress")
    for suffix in ("", "&history=true"):
        data = teacher_session.open(base + "/Instructor/Quiz/Export?courseId=" + course + suffix, timeout=30).read()
        check(zipfile.is_zipfile(io.BytesIO(data)), "grade export is a real XLSX " + suffix)
    try:
        get(teacher_session, "/Instructor/Quiz/Index?courseId=DMKH04")
        raise AssertionError("Teacher unexpectedly accessed another teacher's bank")
    except urllib.error.HTTPError as ex:
        check(ex.code == 404, "other teacher's course is inaccessible")
    print("ALL HTTP QUIZ CHECKS PASSED")
finally:
    conn.rollback()
    cur = conn.cursor()
    for table in ("QuizAttemptGrant", "QuizCertificate", "QuizAttempt", "QuizConfig", "QuizQuestion"):
        cur.execute(f"DELETE dbo.{table} WHERE CourseId=?", course)
    cur.execute("DELETE TienDoBaiHoc WHERE maBaiHoc=?", course + "_1")
    for table in ("TienDoHocTap", "DangKyKhoaHoc"):
        cur.execute(f"DELETE dbo.{table} WHERE maKhoaHoc=?", course)
    cur.execute("DELETE BaiHoc WHERE maChuong=?", course + "C")
    cur.execute("DELETE ChuongHoc WHERE maKhoaHoc=?", course)
    cur.execute("DELETE KhoaHoc WHERE maKhoaHoc=?", course)
    conn.commit()
    conn.close()
    print("Temporary HTTP fixtures removed.")
