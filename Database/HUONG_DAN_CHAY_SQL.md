# Hướng dẫn chạy các file SQL sau khi pull code

Sau khi pull nhánh có module Admin / Báo cáo vi phạm, mỗi bạn cần chạy thêm vài file `.sql` lên database
**trên máy mình** (`QL_HETHONG_CUNGCAP_KHOAHOC`). Nếu không chạy, trang **Báo cáo vi phạm** sẽ báo "Chưa khởi tạo dữ liệu"
và học viên không gửi được báo cáo.

Các file chỉ **thêm** bảng / cột / trigger, **không xóa dữ liệu** và **chạy lại nhiều lần vẫn an toàn**.

---

## Bước 1 — Bắt buộc: chạy 5 file migration theo đúng thứ tự

Thư mục: `Database/Migrations/`

| Thứ tự | File | Làm gì |
| :---: | :--- | :--- |
| 1 | `001_KhuyenNghi_ChoPhepGiaoVien.sql` | Sửa trigger gợi ý khóa học: giáo viên cũng được nhận gợi ý (giáo viên vẫn mua & học khóa của người khác). |
| 2 | `002_BaoCaoViPham.sql` | Tạo bảng `BaoCaoViPham` (học viên báo cáo) và `XuLyViPham` (Admin xử lý). |
| 3 | `003_BaoCaoViPham_BaiHoc.sql` | Thêm cột `maBaiHoc` để biết bài học nào bị báo cáo. |
| 4 | `004_XuLyViPham_TraoDoi.sql` | Thêm bước xử lý và bảng `PhanHoiViPham` (giáo viên ↔ Admin trao đổi khi khóa học bị tạm khóa). |
| 5 | `005_GoiY_LocCongTac.sql` | Gợi ý khóa học (lọc cộng tác): bảng `TuongTacKhoaHoc`, `course_similarity`, `GoiY_TrongSo`, view, hàm và 3 stored procedure (khóa tương tự, gợi ý cho bạn, nút "Gợi ý khóa học"). Sau khi chạy, xem `Recommender/README.md` để chạy job tính gợi ý. |
| 6 | `006_GoiY_GoiYLaiKhoaChiXem.sql` | Sửa quy tắc gợi ý: chỉ không gợi ý lại khóa **đã đăng ký / đã học / đã đánh giá**; khóa chỉ mới xem qua vẫn được gợi ý. Thiếu file này thì người xem nhiều khóa (chưa mua) sẽ thấy trang "Khóa học gợi ý" trống. |

### Cách chạy trong SQL Server Management Studio (SSMS)
1. Mở SSMS, kết nối vào SQL Server trên máy bạn.
2. **File → Open → File...** → chọn file `001_...sql`.
3. Bấm **Execute** (hoặc phím **F5**). Thấy *"Commands completed successfully"* là xong.
4. Lặp lại với `002`, `003`, `004`, `005`, `006` **theo đúng thứ tự**.

> Mỗi file đã có sẵn dòng `USE QL_HETHONG_CUNGCAP_KHOAHOC;` nên không cần chọn database trước.
> Nếu database của bạn đặt **tên khác**, sửa dòng `USE ...` ở đầu file cho đúng tên.

### Kiểm tra đã chạy đủ chưa
Mở một cửa sổ **New Query** và chạy:

```sql
USE QL_HETHONG_CUNGCAP_KHOAHOC;
SELECT
  CASE WHEN OBJECT_ID('dbo.BaoCaoViPham')  IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 002' END AS [002],
  CASE WHEN COL_LENGTH('dbo.BaoCaoViPham','maBaiHoc') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 003' END AS [003],
  CASE WHEN OBJECT_ID('dbo.PhanHoiViPham') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 004' END AS [004],
  CASE WHEN OBJECT_ID('dbo.course_similarity') IS NOT NULL THEN N'OK' ELSE N'THIẾU - chạy 005' END AS [005],
  CASE WHEN OBJECT_DEFINITION(OBJECT_ID('dbo.fn_GoiY_KhoaHocDuocGoiY')) LIKE N'%DangKyKhoaHoc%' THEN N'OK' ELSE N'THIẾU - chạy 006' END AS [006];
```

Cả 5 cột đều ra **OK** là được.

---

## Bước 2 — Không bắt buộc: nạp dữ liệu demo để test

Thư mục: `Database/Seed/`

| File | Làm gì |
| :--- | :--- |
| `01_du_lieu_demo.sql` | Tạo dữ liệu demo: 3 giáo viên, 12 học viên, 13 khóa học đủ mọi trạng thái, đơn hàng ~22 triệu rải trong 1 năm, tiến độ học, đánh giá, hồ sơ giáo viên, báo cáo vi phạm đủ các tình huống, yêu cầu rút tiền. |
| `99_xoa_du_lieu_demo.sql` | Xóa **sạch** dữ liệu demo (kể cả dữ liệu phát sinh khi thao tác bằng tài khoản demo). **Không** đụng dữ liệu thật. |

- Chạy `01_du_lieu_demo.sql` **sau khi đã chạy đủ 4 file migration**. Nếu chạy lần 2 sẽ báo "Đã có dữ liệu demo" — muốn tạo lại thì chạy `99_...` trước.
- Tài khoản demo — mật khẩu chung **`Demo@123`**, đuôi email **`@demo.ecolearning.vn`** (có dấu chấm giữa tên và họ):
  - Giáo viên: `khoa.tran`, `ha.le`, `bao.pham`
  - Học viên: `an.nguyen`, `binh.tran`, `cuong.le`, `dung.pham`, `duc.vo`, `han.dang`, `huy.bui`, `linh.ho`, `nam.ngo`,
    `phuong.duong` (hồ sơ GV chờ duyệt), `tam.ly` (hồ sơ GV bị từ chối), `vy.mai` (tài khoản bị khóa)
  - Ví dụ: `khoa.tran@demo.ecolearning.vn` / `Demo@123`

---

## Bước 3 — Mở Visual Studio

1. Chuột phải **Solution** → **Restore NuGet Packages** (bắt buộc lần đầu: dự án vừa thêm thư viện xuất PDF `PDFsharp-MigraDoc-gdi`).
2. **Build → Rebuild Solution**, rồi **Ctrl + F5**.

---

## Gặp lỗi?

| Lỗi | Cách xử lý |
| :--- | :--- |
| `Database 'QL_HETHONG_CUNGCAP_KHOAHOC' does not exist` | Database trên máy bạn tên khác → sửa dòng `USE ...` ở đầu file. |
| `Invalid object name 'BaoCaoViPham'` khi chạy 003/004 | Chưa chạy `002` → chạy lại theo đúng thứ tự 001 → 004. |
| `Cần chạy Database/Migrations 002, 003, 004 trước...` khi chạy dữ liệu demo | Chạy đủ 4 file migration trước. |
| `Đã có dữ liệu demo...` | Đã nạp rồi; muốn nạp lại thì chạy `99_xoa_du_lieu_demo.sql` trước. |
| Build lỗi thiếu `PdfSharp` / `MigraDoc` | Chưa Restore NuGet Packages (Bước 3). |
| Web báo lỗi kết nối CSDL | Sửa `data source=` trong `Web.config` cho đúng SQL Server trên máy bạn (ví dụ `.` hoặc `.\SQLEXPRESS`). **Không commit** thay đổi này. |
