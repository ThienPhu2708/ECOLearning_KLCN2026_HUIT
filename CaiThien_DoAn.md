# Cải thiện đồ án E-Learning (14–15/10/2026)

Tóm tắt các phần đã làm thêm: chức năng gợi ý khóa học bằng lọc cộng tác (engine, CSDL, giao diện),
menu "Khám phá", carousel banner trang chủ, Skeleton Screen và xử lý web chạy chậm.

> **Chưa commit.** Tất cả thay đổi đang nằm trong thư mục làm việc của nhánh `phu`.
> File `Database/Seed/01_du_lieu_demo.sql` và phần "số chạy tăng dần" ở mục thống kê của `Views/Home/Index.cshtml`
> là thay đổi có sẵn từ trước, không thuộc đợt này.

---

## 0. Việc cần làm sau khi pull

1. Chạy `Database/Migrations/005_GoiY_LocCongTac.sql` rồi `006_GoiY_GoiYLaiKhoaChiXem.sql` trong SSMS. Chạy lại nhiều lần vẫn an toàn, không xóa dữ liệu.
2. Visual Studio: **Rebuild Solution**.
3. Máy nào chạy job gợi ý thì cần Python và ODBC Driver 17 for SQL Server:
   ```bash
   cd Recommender
   pip install -r requirements.txt
   python build_recommendations.py --source sql
   ```
4. Tùy chọn: đặt lịch chạy job mỗi đêm (xem mục 1.5).

---

## 1. Gợi ý khóa học bằng lọc cộng tác (Item-based Collaborative Filtering)

Làm theo ràng buộc của GVHD: memory-based, **không huấn luyện mô hình**, đúng 4 bước:
ma trận user × course → cosine item-item → sắp xếp giảm dần → lấy top-K.

### 1.1. Engine Python (`Recommender/`)

| File | Vai trò |
| :--- | :--- |
| `recommender/config.py` | Trọng số tín hiệu, `MIN_COMMON_USERS = 2`, K, chuỗi kết nối (ghi đè bằng biến môi trường `ECO_SQL_CONN`) |
| `recommender/scoring.py` | Điểm tương tác của một ô (user, course), nằm trong [0, 1] |
| `recommender/item_cf.py` | 4 bước CF: `fit`, `recommend_for_user`, `similar_courses`, `popular_courses` (cold-start) |
| `recommender/data_sources.py` | Nạp dữ liệu từ CSV hoặc SQL Server, cùng một cấu trúc cột |
| `recommender/sql_store.py` | Ghi `GoiY_TrongSo`, `course_similarity`, `KhoaHocKhuyenNghi` |
| `recommender/evaluation.py`, `evaluate.py` | Đánh giá Precision / Recall / HitRate@K |
| `build_recommendations.py` | Job tính định kỳ |
| `run_demo.py` | Chạy mẫu trên CSV |
| `tests/test_item_cf.py` | 9 test đơn vị, ví dụ nhỏ tính tay được |
| `README.md` | Hướng dẫn chi tiết, kết quả đánh giá, cách tích hợp |

**Công thức điểm tương tác:**

`score = 0.4·rating/5 + 0.2·views/max + 0.1·clicks/max + 0.1·likes + 0.2·progress/100`

- Trọng số khai báo ở **một chỗ duy nhất** là `SIGNAL_WEIGHTS`. Tổng phải bằng 1, nếu không code sẽ báo lỗi.
- **Thêm bình luận sau này:** thêm cột `comments` vào view `vw_GoiY_TuongTac`, thêm một dòng vào view `vw_GoiY_TinHieu` và một dòng vào `SIGNAL_WEIGHTS`. Không phải sửa thuật toán.

**Điểm dự đoán:** `pred(j) = Σ sim(i, j) × score(u, i)` trên các khóa i mà người dùng đã tương tác.
Bỏ khóa đã học và khóa có điểm 0. Cặp khóa có dưới 2 người dùng chung thì độ tương đồng = 0.

### 1.2. Kết quả đánh giá (để đưa vào báo cáo)

Dữ liệu CSV gồm 2.800 tương tác, 350 user, 214 khóa. Cách đo: giấu 20% khóa của mỗi user, chạy 5 lần với 5 seed khác nhau, `min_common_users = 2`.

| K | Phương pháp | Precision@K | Recall@K | HitRate@K |
| :-: | :--- | :-: | :-: | :-: |
| 5 | **ItemCF** | **0.0755** | **0.2366** | **0.3503** |
| 5 | Phổ biến (baseline) | 0.0093 | 0.0291 | 0.0463 |
| 10 | **ItemCF** | **0.0741** | **0.4577** | **0.6149** |
| 10 | Phổ biến (baseline) | 0.0096 | 0.0580 | 0.0931 |

Nhận xét trung thực:
- **ItemCF vượt baseline khoảng 7 lần.** Baseline phổ biến gần như ngang mức chọn ngẫu nhiên, vì độ phổ biến giữa các khóa khá đều.
- **Precision thấp là do cách đo:** mỗi user chỉ bị giấu khoảng 1,6 khóa, nên Precision@10 tối đa chỉ khoảng 0,16.
- **Kết quả lạc quan:** dữ liệu được sinh theo cụm danh mục, trung bình 76% khóa của một user thuộc cùng một danh mục (nếu chọn ngẫu nhiên chỉ khoảng 30%). Cần đo lại trên dữ liệu thật.
- **Về `min_common_users`:** HitRate@10 lần lượt là 1 → 0.622, 2 → 0.615, 3 → 0.552. Chọn mức 2 vì gần như không giảm độ chính xác so với 1, nhưng loại được các cặp nhiễu.
- **Dữ liệu CSV có tên khóa trùng:** 47 tên khóa xuất hiện dưới nhiều `course_id` (ví dụ "Introduction to Psychology" có 5 mã), do bộ dữ liệu Coursera gốc liệt kê một khóa theo nhiều danh mục.

### 1.3. CSDL: `Database/Migrations/005_GoiY_LocCongTac.sql`

| Đối tượng | Vai trò |
| :--- | :--- |
| `TuongTacKhoaHoc` | Lượt xem / click / thích của người dùng với khóa học |
| `course_similarity` | Ma trận độ tương đồng (chỉ lưu cặp > 0, tối đa 50 khóa tương tự cho mỗi khóa) |
| `GoiY_TrongSo` | Trọng số job đã dùng, để SQL tính điểm giống hệt Python |
| `vw_GoiY_TuongTac` | Dữ liệu tương tác cùng cấu trúc với file CSV (`rating` ← `DanhGiaKhoaHoc`, `progress` ← `TienDoHocTap`, `views/clicks/likes` ← `TuongTacKhoaHoc`) |
| `vw_GoiY_TinHieu`, `fn_GoiY_KhoaHocDuocGoiY` | Hỗ trợ tính điểm và lọc khóa được phép gợi ý |
| `sp_GoiY_KhoaHocTuongTu` | "Khóa học tương tự" (chưa đưa lên giao diện) |
| `sp_GoiY_KhoaHocChoNguoiDung` | Đọc gợi ý đã tính sẵn, dùng cho mục ở trang chủ |
| `sp_GoiY_TinhGoiYChoNguoiDung` | **Tính lại gợi ý cho một người** từ ma trận đã lưu, không tính lại cosine, mất khoảng 70 ms |

Mọi gợi ý đều **không** gồm: khóa đã tương tác, khóa không còn mở bán, và khóa do chính giáo viên đó dạy.

**Kiểm chứng:** kết quả SQL **khớp 100%** với Python (353/353 user trên dữ liệu CSV, 21/21 user trên bản sao CSDL thật).
So sánh cả danh sách, thứ tự, điểm và "khóa làm lý do".

### 1.4. Hai luồng tính gợi ý

| Luồng | Khi nào | Làm gì |
| :--- | :--- | :--- |
| Job Python | Mỗi đêm lúc 0:00 | Tính lại **cả 4 bước** cho mọi người dùng |
| Trang "Khóa học gợi ý" | Mỗi lần mở trang / bấm "Làm mới gợi ý" | Chỉ tính lại cho **người đang xem**, từ tương tác mới nhất × ma trận đã lưu |

### 1.5. Lịch chạy job

- `Recommender/chay_job_goi_y.bat`: chạy job và ghi log vào `Recommender/output/job.log`.
- Đã tạo tác vụ `ECOLearning_GoiY` trong Task Scheduler trên máy LAPTOP-L3MTTMQ3, chạy 0:00 mỗi đêm. Lệnh tạo tác vụ có trong `Recommender/README.md`.
- Khi triển khai thật nên dời sang 2:00–4:00 sáng. SQL Server Express không có SQL Agent nên phải dùng Task Scheduler.

### 1.6. Giao diện gợi ý

- **Trang "Khóa học gợi ý"** `/Recommendation`: mở từ nút "Xem gợi ý cá nhân" trên banner, hoặc "Xem tất cả khóa học" ở mục gợi ý trang chủ.
  - Mỗi thẻ có dòng "Vì bạn đã học …".
  - Đầu trang có các chỉ số: số khóa đã tương tác, số khóa được gợi ý, giờ tính, giờ ma trận cập nhật.
- **Trang chủ, mục "Khóa học dành riêng cho bạn":** hiện 3 gợi ý thật, thay cho 3 khóa cố định trước đây.
- **Chỉ hiển thị kết quả lọc cộng tác.** Hằng số `RecommendationService.HienThiKhoaPhoBien = false`; logic khóa phổ biến vẫn giữ trong SQL.
- **Hai trạng thái trống:**
  - Chưa có tương tác: "Bạn cần xem một số khóa học để hiển thị gợi ý."
  - Có tương tác nhưng chưa có khóa tương tự: "Các khóa học bạn đã xem chưa có khóa học tương tự để gợi ý."
- **Ghi lượt xem:** mở trang chi tiết khóa học thì `TuongTacKhoaHoc.luotXem` tăng 1 (mỗi khóa một lần mỗi phiên; bỏ qua Admin và giáo viên xem khóa của chính mình).

File: `Services/RecommendationService.cs`, `Controllers/RecommendationController.cs`, `ViewModels/RecommendationViewModels.cs`,
`Views/Recommendation/Index.cshtml`, `Views/Shared/_RecommendedCourseCard.cshtml`, `Controllers/HomeController.cs`, `Controllers/CourseController.cs`.

---

## 2. Menu "Khám phá" 3 tầng trên header

- Rê chuột vào "Khám phá": **Danh mục → Cấp độ (Cơ bản / Trung cấp / Nâng cao) → Khóa học**. Tầng nào cũng bấm được.
  - Tầng 1 và 2 mở trang danh sách đã lọc sẵn.
  - Tầng 3 mở trang chi tiết khóa học.
- Đầu menu có lối tắt "Khóa học gợi ý cho bạn" và "Tất cả khóa học".
- Danh mục tạm ngưng bị ẩn. Không hiện con số. Không cache, nên dữ liệu mới hiện ngay.
- Dùng được bằng bàn phím (Tab).

File: `Controllers/NavigationController.cs`, `ViewModels/ExploreMenuViewModels.cs`, `Views/Shared/_ExploreMenu.cshtml`, `Views/Shared/_Layout.cshtml`.

---

## 3. Carousel banner trang chủ (Hero Section)

- 4 banner: banner cũ, "Gợi ý khóa học cá nhân hóa", "Học mọi lúc, mọi nơi", "Trở thành giảng viên".
- Tự chuyển sau **2 giây**. Rê chuột vào thì tạm dừng. Điều hướng được bằng mũi tên hai bên, chấm bên dưới, phím ← → và vuốt trên điện thoại.
- Nút bấm đổi theo người xem:
  - Khách: Đăng ký / Đăng nhập.
  - Học viên: Xem gợi ý, Tiếp tục học, Đăng ký làm giảng viên.
  - Giáo viên: Vào kênh giảng viên.
- Ảnh lấy từ Unsplash (giấy phép miễn phí), lưu sẵn trong `assets/images/banners/`, nên demo không cần mạng.

File: `Views/Home/_HeroCarousel.cshtml`, ảnh `banner_goi_y.jpg`, `banner_hoc_linh_hoat.jpg`, `banner_giang_vien.jpg`.

---

## 4. Skeleton Screen cho thẻ khóa học

- Khi tải trang, thẻ khóa học hiện khung xương có hiệu ứng ánh sáng chạy qua, đúng vị trí ảnh, tên khóa, giảng viên, đánh giá và giá.
- Ảnh tải xong thì nội dung hiện ra (tối đa 2,5 giây). Trang không bị giật.
- Áp dụng ở trang chủ, Khám phá và Khóa học gợi ý. Chưa áp dụng ở trang hồ sơ giảng viên, vì thẻ ở đó viết bằng style trực tiếp.

File: `Views/Shared/_Layout.cshtml` (script trong `<head>`), `assets/css/components.css`.

---

## 5. Web chạy chậm: nguyên nhân và cách xử lý

**Hiện tượng:** có lúc trang chủ, trang gợi ý và "Khóa học của tôi" mất 25–50 giây mới hiện.

**Nguyên nhân:**
- Máy thiếu RAM (12 GB, chỉ còn khoảng 1 GB trống), nên Windows ép SQL Server thu nhỏ, chỉ còn **5 MB** cho truy vấn cần sắp xếp.
- Các truy vấn Entity Framework dùng `.Include(...)` **nạp kèm danh sách con** (đánh giá, đăng ký, chương/bài học) cần 6–7 MB.
- Vì vậy SQL Server bắt chúng chờ cấp bộ nhớ (wait `RESOURCE_SEMAPHORE`) tới **25 giây**.

**Đã sửa:** thay các Include danh sách con bằng truy vấn đếm / tính trung bình nhẹ (GROUP BY). Kết quả đo khi máy vẫn đang thiếu RAM:

| Trang | Trước | Sau |
| :--- | :-: | :-: |
| Trang chủ | 25–50 giây | 0,09–0,13 giây |
| Khóa học gợi ý | 25 giây | 0,13–0,19 giây |
| Khám phá | 25 giây | 0,04 giây |
| Khóa học của tôi | 25–30 giây | 0,03–0,11 giây |

**Tối ưu thêm:**
- Bootstrap JS dùng bản có sẵn trong dự án (`Scripts/bootstrap.bundle.min.js`) thay cho CDN; web chỉ dùng phần Modal.
- Ảnh bìa khóa học chỉ tải khi thẻ sắp xuất hiện trên màn hình (`loading="lazy"`).

**Lưu ý khi phát triển:**
- Chạy bằng Ctrl+F5 thay vì F5.
- Mỗi lần build, trang mở lần đầu sẽ chậm vài giây vì phải biên dịch lại view.

File: `Controllers/HomeController.cs`, `Controllers/CourseController.cs`, `Controllers/LearningController.cs`, `Services/RecommendationService.cs`,
`Views/Home/Index.cshtml`, `Views/Course/Index.cshtml`.

---

## 6. Video bài học và Git

- Video được lưu thành file trên server: `assets/uploads/videos/` (và `Uploads/Videos/` ở trang quản lý đề cương). CSDL chỉ lưu đường dẫn trong `BaiHoc.duongDanVideo`.
- Đã thêm hai thư mục video vào `.gitignore`, nên video tải lên mới không vào Git nữa.
- **Chưa làm:** 9 file mp4 cũ (417 MB) vẫn đang được Git theo dõi. Muốn bỏ thì cần `git rm --cached`, nhưng **cả nhóm sẽ mất các file đó khi pull**. Phải chia sẻ video qua kênh khác trước khi làm.

---

## 7. Lỗi đã phát hiện và sửa trong quá trình làm

- **Python gợi ý lại khóa đã đăng ký:** khóa có điểm tương tác 0 (mới đăng ký, chưa học) vẫn bị gợi ý lại. Đã sửa và thêm test.
- **Khóa chưa ai học bị bỏ qua:** khi bù khóa phổ biến, Python bỏ qua các khóa chưa ai học. Đã sửa cho giống SQL.
- **Carousel:** bấm chuyển thật nhanh làm slide bị lệch 48px. Đã sửa.

---

## 8. Việc còn lại / cần quyết định

| Việc | Ghi chú |
| :--- | :--- |
| Commit + push | Chưa commit gì |
| Mục "Khóa học tương tự" ở trang chi tiết | Stored procedure `sp_GoiY_KhoaHocTuongTu` đã có, chỉ cần gắn vào view |
| Ghi click / thích vào `TuongTacKhoaHoc` | Cần thêm nút Thích; công thức đã có trọng số sẵn |
| Trang hồ sơ giảng viên (`UserController`) | Vẫn nạp kèm 2 danh sách con nên có thể chậm khi máy thiếu RAM; sửa giống mục 5 |
| Bộ nhớ tối thiểu cho SQL Server | `EXEC sp_configure 'min server memory (MB)', 512; RECONFIGURE;` (cần bật `show advanced options`). Trị tận gốc việc chậm 25 giây, cần chủ máy đồng ý |
| Thẻ nổi "98% – Web Development Masterclass" trên banner 1 | Vẫn là dữ liệu cố định (khi đã đăng nhập) |
| Video cũ trong Git | Xem mục 6 |
| Hướng triển khai thật cho video | Azure Blob Storage / AWS S3 + link có thời hạn (đưa vào báo cáo như hướng phát triển) |
