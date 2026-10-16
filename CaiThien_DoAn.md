# Cải thiện đồ án E-Learning (14–16/10/2026)

Tóm tắt các phần đã làm thêm: chức năng gợi ý khóa học bằng lọc cộng tác (engine, CSDL, giao diện, "Vì bạn đã xem"),
thả tim khóa học, menu "Khám phá" và "Khóa học của tôi", trang danh mục, trang hồ sơ giáo viên, giao diện trang chủ,
Skeleton Screen, xử lý web chạy chậm và hoàn thiện phân hệ Marketing theo đặc tả (khuyến mãi, coupon, chiến dịch, báo cáo).

> **Trạng thái commit:** phần lớn đã commit trong `3944db4` (nhánh `phu`, đã merge qua PR #4).
> **Chưa commit:** trang danh mục `/danh-muc/{slug}`, sửa lỗi mất viền card danh mục (mục 12),
> xử lý cold-start bằng lĩnh vực quan tâm + khóa phổ biến (mục 13, migration 007)
> và phân hệ Marketing / giá / doanh thu (mục 14, migration 008).
> File `Database/Seed/01_du_lieu_demo.sql` và phần "số chạy tăng dần" ở mục thống kê của `Views/Home/Index.cshtml`
> là thay đổi có sẵn từ trước, không thuộc đợt này.

---

## 0. Việc cần làm sau khi pull

1. Chạy `Database/Migrations/005_GoiY_LocCongTac.sql`, `006_GoiY_GoiYLaiKhoaChiXem.sql`, `007_GoiY_SoThichNguoiDung.sql` rồi `008_Marketing_KhuyenMai.sql` trong SSMS. Chạy lại nhiều lần vẫn an toàn, không xóa dữ liệu.
2. Visual Studio: **Rebuild Solution**.
3. **Bắt buộc chạy job gợi ý ít nhất một lần.** Migration chỉ tạo bảng, view và thủ tục; các bảng `course_similarity`,
   `GoiY_TrongSo`, `KhoaHocKhuyenNghi` vẫn **trống** cho tới khi job chạy, nên web không có gợi ý lọc cộng tác nào
   (mục gợi ý chỉ hiện khóa phổ biến).
   Cần Python và ODBC Driver 17 for SQL Server:
   ```bash
   cd Recommender
   pip install -r requirements.txt
   python build_recommendations.py --source sql
   ```
   Kiểm tra nhanh trong SSMS: `SELECT COUNT(*) FROM course_similarity;` phải lớn hơn 0. Log job nằm ở `Recommender/output/job.log`.
   Máy không dùng `.\SQLEXPRESS` thì đặt biến môi trường `ECO_SQL_CONN` (xem `Recommender/README.md`).
4. **Muốn thấy gợi ý khi test cần dữ liệu cộng đồng:** hai khóa chỉ tương đồng khi có **ít nhất 2 người khác nhau** cùng
   tương tác với cả hai khóa (`min_common_users = 2`). Một tài khoản bấm nhiều khóa sẽ không tạo ra cặp tương đồng nào.
   Cách test: 2–3 tài khoản cùng xem vài khóa giống nhau → chạy job → tài khoản khác xem một trong các khóa đó sẽ thấy gợi ý.
   Chỉ để demo nhanh: `python build_recommendations.py --source sql --min-common 1` (không khớp tham số trong báo cáo).
5. Tùy chọn: đặt lịch chạy job mỗi đêm (xem mục 1.5).

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
| `recommender/data_sources.py` | Nạp dữ liệu từ CSV hoặc SQL Server, cùng một cấu trúc cột; `load_consumed_courses` lấy khóa đã sở hữu / đã học |
| `recommender/sql_store.py` | Ghi `GoiY_TrongSo`, `course_similarity`, `KhoaHocKhuyenNghi` |
| `recommender/evaluation.py`, `evaluate.py` | Đánh giá Precision / Recall / HitRate@K |
| `build_recommendations.py` | Job tính định kỳ |
| `run_demo.py` | Chạy mẫu trên CSV |
| `tests/test_item_cf.py` | 10 test đơn vị, ví dụ nhỏ tính tay được |
| `README.md` | Hướng dẫn chi tiết, kết quả đánh giá, cách tích hợp |

**Công thức điểm tương tác:**

`score = 0.4·rating/5 + 0.2·views/max + 0.1·clicks/max + 0.1·likes + 0.2·progress/100`

- Trọng số khai báo ở **một chỗ duy nhất** là `SIGNAL_WEIGHTS`. Tổng phải bằng 1, nếu không code sẽ báo lỗi.
- `likes` lấy từ `TuongTacKhoaHoc.daThich`, tức nút thả tim (mục 9).
- **Thêm bình luận sau này:** thêm cột `comments` vào view `vw_GoiY_TuongTac`, thêm một dòng vào view `vw_GoiY_TinHieu` và một dòng vào `SIGNAL_WEIGHTS`. Không phải sửa thuật toán.

**Điểm dự đoán:** `pred(j) = Σ sim(i, j) × score(u, i)` trên các khóa i mà người dùng đã tương tác (tổng có trọng số,
không chia mẫu số, vì mục tiêu là xếp hạng Top-N chứ không dự đoán số sao).
Cặp khóa có dưới 2 người dùng chung thì độ tương đồng = 0.

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
- Đánh giá offline vẫn dùng quy tắc mặc định "bỏ mọi khóa đã tương tác" (khác với job chạy thật ở mục 1.3), để giữ đúng cách chia train/test.

### 1.3. CSDL: migration 005 và 006

**`Database/Migrations/005_GoiY_LocCongTac.sql`**

| Đối tượng | Vai trò |
| :--- | :--- |
| `TuongTacKhoaHoc` | Lượt xem / click / thích của người dùng với khóa học, kèm `lanCapNhat` (lần xem gần nhất) |
| `course_similarity` | Ma trận độ tương đồng (chỉ lưu cặp > 0, tối đa 50 khóa tương tự cho mỗi khóa) |
| `GoiY_TrongSo` | Trọng số job đã dùng, để SQL tính điểm giống hệt Python |
| `vw_GoiY_TuongTac` | Dữ liệu tương tác cùng cấu trúc với file CSV (`rating` ← `DanhGiaKhoaHoc`, `progress` ← `TienDoHocTap`, `views/clicks/likes` ← `TuongTacKhoaHoc`) |
| `vw_GoiY_TinHieu`, `fn_GoiY_KhoaHocDuocGoiY` | Hỗ trợ tính điểm và lọc khóa được phép gợi ý |
| `sp_GoiY_KhoaHocTuongTu` | "Khóa học tương tự" (chưa đưa lên giao diện) |
| `sp_GoiY_KhoaHocChoNguoiDung` | Đọc gợi ý đã tính sẵn |
| `sp_GoiY_TinhGoiYChoNguoiDung` | **Tính lại gợi ý cho một người** từ ma trận đã lưu, không tính lại cosine, mất khoảng 70 ms |

**`Database/Migrations/006_GoiY_GoiYLaiKhoaChiXem.sql`** (mới)

- Viết lại `fn_GoiY_KhoaHocDuocGoiY`: chỉ loại khóa **đã sở hữu / đã học** (có trong `DangKyKhoaHoc`, `TienDoHocTap` hoặc `DanhGiaKhoaHoc`).
- Khóa **chỉ mới xem qua** vẫn được gợi ý lại.
- Lý do: trước đây loại mọi khóa đã tương tác, nên học viên xem hết các khóa (ví dụ tài khoản demo đã xem cả 10 khóa) thì **không còn gợi ý nào**, cả trang chủ lẫn `/Recommendation`.
- Job Python dùng cùng quy tắc qua tham số `seen_by_user` (`load_consumed_courses`), nên SQL và Python vẫn khớp nhau.
- Câu kiểm tra sau khi chạy migration có trong `Database/HUONG_DAN_CHAY_SQL.md`.

Mọi gợi ý đều **không** gồm: khóa đã sở hữu / đã học, khóa không còn mở bán, và khóa do chính giáo viên đó dạy.

**Kiểm chứng:** kết quả SQL **khớp 100%** với Python (353/353 user trên dữ liệu CSV, 21/21 user trên bản sao CSDL thật).
So sánh cả danh sách, thứ tự, điểm và "khóa làm lý do".

### 1.4. Các luồng tính gợi ý

| Luồng | Khi nào | Làm gì |
| :--- | :--- | :--- |
| Job Python | Mỗi đêm lúc 0:00 | Tính lại **cả 4 bước** cho mọi người dùng |
| Trang "Khóa học gợi ý" | Mỗi lần mở trang / bấm "Làm mới gợi ý" | Chỉ tính lại cho **người đang xem**, từ tương tác mới nhất × ma trận đã lưu |
| Trang chủ "Vì bạn đã xem X" | Mỗi lần mở trang chủ | Lấy khóa vừa xem gần nhất × ma trận đã lưu (mục 1.7) |

### 1.5. Lịch chạy job

- `Recommender/chay_job_goi_y.bat`: chạy job và ghi log vào `Recommender/output/job.log`.
- Đã tạo tác vụ `ECOLearning_GoiY` trong Task Scheduler trên máy LAPTOP-L3MTTMQ3, chạy 0:00 mỗi đêm. Lệnh tạo tác vụ có trong `Recommender/README.md`.
- Máy thành viên khác **chưa có** tác vụ này, nên phải tự chạy job (mục 0) hoặc tạo tác vụ:
  `schtasks /Create /TN ECOLearning_GoiY /TR "<đường dẫn>\Recommender\chay_job_goi_y.bat" /SC DAILY /ST 00:00`
- Khi triển khai thật nên dời sang 2:00–4:00 sáng. SQL Server Express không có SQL Agent nên phải dùng Task Scheduler.

### 1.6. Giao diện gợi ý

- **Trang "Khóa học gợi ý"** `/Recommendation`: mở từ nút "Xem gợi ý cá nhân" trên banner, hoặc "Xem tất cả" ở mục gợi ý trang chủ.
  Đầu trang có các chỉ số: số khóa đã tương tác, số khóa được gợi ý, giờ tính, giờ ma trận cập nhật.
- **Trang chủ, mục "Khóa học dành riêng cho bạn":**
  - Tối đa **10 gợi ý** (`HomeController.SoGoiYTrangChu = 10`).
  - Dòng phụ đề: **Vì bạn đã xem "<tên khóa>"** (tên khóa là link, mở tab mới).
  - Trên 5 khóa: thành slider có mũi tên. Dưới 5 khóa: thẻ tự giãn to để lấp đầy hàng (mục 8.3).
- **Thẻ gợi ý:** nhãn "Phù hợp với bạn" (icon ngón tay cái, đã bỏ icon 3 ngôi sao) cho kết quả lọc cộng tác, nhãn "Phổ biến" cho khóa bù. **Đã bỏ dòng mô tả lý do** trên thẻ. Có nút thả tim.
- **Khóa phổ biến đã bật lại** (`RecommendationService.HienThiKhoaPhoBien = true`): mục gợi ý không còn trống khi lọc cộng tác chưa đủ dữ liệu, xem mục 13.
- **Trạng thái trống:** chỉ còn khi học viên đã sở hữu tất cả khóa ("Bạn đã đăng ký tất cả khóa học hiện có.")
  hoặc chưa chạy migration 005 ("Chức năng gợi ý khóa học chưa sẵn sàng.").
- **Ghi lượt xem** (`RecommendationService.RecordView`): mở trang chi tiết khóa học thì
  - `lanCapNhat` cập nhật **mỗi lần xem** (để biết khóa xem gần nhất);
  - `luotXem` chỉ tăng 1 lần mỗi khóa mỗi phiên;
  - bỏ qua Admin và giáo viên xem khóa của chính mình.

File: `Services/RecommendationService.cs`, `Controllers/RecommendationController.cs`, `ViewModels/RecommendationViewModels.cs`,
`Views/Recommendation/Index.cshtml`, `Views/Shared/_RecommendedCourseCard.cshtml`, `Controllers/HomeController.cs`, `Controllers/CourseController.cs`.

### 1.7. "Vì bạn đã xem X" (mới)

Giống cách Udemy hiển thị, nhưng chỉ dùng Item-based (không dùng nội dung khóa học).

- **Khóa mốc (anchor):** khóa học viên **xem gần nhất** (`TuongTacKhoaHoc.lanCapNhat`, `luotXem > 0`) **và** còn khóa tương tự để gợi ý.
- Gợi ý = các khóa trong `course_similarity` của khóa mốc, lọc qua `fn_GoiY_KhoaHocDuocGoiY`, sắp giảm dần theo độ tương đồng.
- Khóa xem gần nhất không còn ứng viên → lùi về khóa xem trước đó → nếu vẫn không có thì dùng các bậc tiếp theo ở mục 13.
- Thả tim **không** cập nhật `lanCapNhat`, nên không làm đổi khóa mốc.

File: `RecommendationService.GetForHome` (bước 1: `LayKhoaMoc`, `LayKhoaTuongTu`), lớp `KhoaMocRow`, `GoiYTrangChuViewModel` trong `ViewModels/RecommendationViewModels.cs`.

---

## 2. Menu "Khám phá" 3 tầng trên header

- Rê chuột vào "Khám phá": **Danh mục → Cấp độ (Cơ bản / Trung cấp / Nâng cao) → Khóa học**. Tầng nào cũng bấm được.
  - Tầng 1 và 2 mở **trang danh mục** `/danh-muc/{slug}` (lọc sẵn cấp độ nếu bấm ở tầng 2), xem mục 12.
  - Tầng 3 mở trang chi tiết khóa học (tab mới).
- Đầu menu có lối tắt "Tất cả khóa học".
- **Ẩn khóa đã sở hữu.** Danh mục mà người dùng đã sở hữu hết khóa thì hiện "Không có khóa học mới trong danh mục này."
- Danh mục tạm ngưng bị ẩn (vì vậy khóa thuộc danh mục DM06 không xuất hiện). Không hiện con số. Không cache, nên dữ liệu mới hiện ngay.
- Xử lý hover tách ra `assets/js/explore-menu.js` (event delegation), dùng chung cho mọi menu dạng `.explore-menu`.
- Dùng được bằng bàn phím (Tab).

File: `Controllers/NavigationController.cs`, `ViewModels/ExploreMenuViewModels.cs`, `Views/Shared/_ExploreMenu.cshtml`,
`assets/js/explore-menu.js`, `Views/Shared/_Layout.cshtml`.

---

## 3. Carousel banner trang chủ (Hero Section)

- 4 banner: banner cũ, "Gợi ý khóa học cá nhân hóa", "Học mọi lúc, mọi nơi", "Trở thành giảng viên".
- Tự chuyển sau **2 giây**. Rê chuột vào thì tạm dừng. Điều hướng được bằng mũi tên hai bên, chấm bên dưới, phím ← → và vuốt trên điện thoại.
- Nút bấm đổi theo người xem:
  - Khách: Đăng ký / Đăng nhập.
  - Học viên: Xem gợi ý, Tiếp tục học, Đăng ký làm giảng viên.
  - Giáo viên: Vào kênh giảng viên.
- **Icon mới** (bỏ icon 3 ngôi sao): `bi-lightbulb-fill`, `bi-arrow-repeat`, `bi-lightbulb`, `bi-compass-fill`.
- **Hover nút:** chữ phóng to nhẹ (1.06), icon phóng to (1.2), mũi tên trượt sang phải.
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

Trang hồ sơ giáo viên (`UserController.BuildProfile`) cũng đã chuyển sang GROUP BY (mục 7).

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

## 7. Trang hồ sơ giáo viên (mới)

- **Bỏ card "Thông tin hồ sơ".** Các mục thông tin của giáo viên có thể **thu gọn / mở rộng** (nút toggle từng mục).
- **Danh sách khóa học của giáo viên:**
  - Từ 5 khóa trở xuống: thẻ khóa học như cũ.
  - Trên 5 khóa: chuyển sang **danh sách dạng dòng thu gọn**, kèm nút **"Xem tất cả"**.
- **Trang mới `/User/Courses/{id}`:** toàn bộ khóa học của giáo viên, sắp xếp theo Mặc định / Đánh giá cao / Nhiều học viên / Giá tăng dần.
- Đếm số học viên, đánh giá bằng truy vấn GROUP BY (không nạp kèm danh sách con).
- Trang chi tiết khóa học: bấm tên giáo viên mở hồ sơ ở **tab mới**.

File: `Controllers/UserController.cs` (`BuildProfile`, `Courses`), `ViewModels/InstructorProfileViewModel.cs`
(`SoKhoaHocTrenTrangHoSo = 5`, `KhoaHocCuaGiaoVienItem`), `Views/User/Index.cshtml`, `Views/User/Courses.cshtml`, `Views/User/_TeacherCourseCard.cshtml`.

---

## 8. Giao diện trang chủ và trang khóa học (mới)

### 8.1. Đổi tên hiển thị
Mọi chữ "ECOLearning" hiển thị cho người dùng đã đổi thành **"E-Learning"**. "ECOLearning" chỉ còn là tên source / namespace.

### 8.2. Hai mục giới thiệu (giá trị và các bước)
- Hiệu ứng xuất hiện khi cuộn tới (scroll-reveal), **mỗi mục một kiểu khác nhau** (`value-rise`, `value-icon-pop`, `step-flip`).
- Card có màu theo tông chủ đạo xanh – trắng – xám (`#434CE8`, `#2563EB`, `#0369A1`, `#0EA5E9`), không dùng card đen hay màu chói.
- Có hiệu ứng hover khi rê chuột.

### 8.3. Slider khóa học dùng chung
- **"Khóa học nổi bật nhất tuần":** 5 thẻ mỗi lượt, thẻ nhỏ hơn. Bấm mũi tên trượt 4 thẻ, nên **thẻ cuối lượt trước là thẻ đầu lượt sau**.
- **"Khóa học dành riêng cho bạn":** cùng kiểu slider khi trên 5 khóa; dưới 5 khóa thì thẻ giãn to lấp đầy khung.
- Code dùng chung: CSS `.course-slider` (5/3/2/1 thẻ theo độ rộng màn hình) và hàm JS `initCourseSlider`.

### 8.4. Ẩn khóa học đã sở hữu
Khóa đã đăng ký hoặc đã mua (kể cả khóa 0đ) **không còn hiện** ở:
- trang chủ (mục nổi bật);
- trang `/Course`, có dòng nhắc "N khóa bạn đã sở hữu nằm ở Khóa học của tôi";
- menu "Khám phá";
- trang danh mục.

Các khóa này chỉ hiện trong "Khóa học của tôi".

### 8.5. Mở tab mới
Bấm xem chi tiết khóa học (thẻ khóa học, thẻ gợi ý, menu) và bấm tên giáo viên ở trang chi tiết đều mở **tab mới** (`target="_blank"`).

### 8.6. Báo cáo vi phạm
Đã bỏ nút, modal và script báo cáo vi phạm ở **trang chi tiết khóa học**. Học viên báo cáo tại **màn hình học** (`Views/Learning/Study.cshtml`), nơi biết đúng bài học đang xem.

File: `Views/Home/Index.cshtml`, `Views/Course/Index.cshtml`, `Views/Course/Details.cshtml`, `Controllers/HomeController.cs`, `Controllers/CourseController.cs`.

---

## 9. Thả tim khóa học (mới)

- **Không thêm bảng mới:** dùng cột có sẵn `TuongTacKhoaHoc.daThich`. Đây cũng là tín hiệu `likes` (trọng số 0,1) của lọc cộng tác.
- **Quy tắc:**
  - Chỉ người đã đăng nhập mới tim được.
  - Chỉ tim được khóa **đã xuất bản**. Không có nút tim trên khóa của chính mình hoặc khóa đã đăng ký.
  - Khóa đã tim **vẫn được gợi ý**. Khóa đã tim rồi mua / đăng ký thì tự rời khỏi danh sách đã tim.
  - Thả / bỏ tim **không** cập nhật `lanCapNhat`.
- **Nút tim:** dạng icon trên thẻ khóa học; dạng **ô vuông cạnh nút giỏ hàng** ở trang chi tiết (cao bằng nút chính).
- **Không hiện thông báo khi thành công**, chỉ báo khi lỗi.
- **Đồng bộ dữ liệu** sau khi tim / bỏ tim: menu "Khóa học của tôi", trang "Khóa học đã tim" và các thẻ đang mở ở tab khác
  được cập nhật qua `BroadcastChannel` / `storage`, và tự đồng bộ lại khi quay về trang (`pageshow`, `visibilitychange`).
- Server: `Toggle` dùng `MERGE … OUTPUT inserted.daThich` trong một câu lệnh.

File: `Services/FavoriteService.cs`, `Controllers/FavoriteController.cs` (`Toggle` POST có AntiForgery, `State` GET),
`ViewModels/FavoriteViewModels.cs`, `Views/Shared/_FavoriteButton.cshtml` (biến thể `icon` / `square`),
`Views/Learning/Favorites.cshtml`, `LearningController.Favorites`, `assets/js/favorite.js`, `assets/css/components.css`.

---

## 10. Menu "Khóa học của tôi" trên header (mới)

- Rê chuột hiện menu **2 tầng**, giao diện giống "Khám phá", **không chèn icon**:
  - **Khóa học đã đăng ký:** 8 khóa đăng ký gần nhất, kèm link xem tất cả (trang "Khóa học của tôi" giữ nguyên như cũ).
  - **Khóa học đã tim:** danh sách đã tim, kèm link tới trang quản lý riêng.
- **Trang "Khóa học đã tim"** `/Learning/Favorites` tách riêng, **không gộp thành tab** với trang khóa học đã đăng ký.
- Chiều cao menu cố định (bố cục `is-stacked`), không bị tràn khung khi một bên nhiều khóa hơn bên kia.
- Menu được tải lại qua AJAX (`MyCoursesMenuPanel`, không cache) sau mỗi lần tim / bỏ tim.

File: `Controllers/NavigationController.cs` (`MyCoursesMenu`, `MyCoursesMenuPanel`, `BuildMyCoursesMenu`),
`Views/Shared/_MyCoursesMenu.cshtml`, `Views/Shared/_Layout.cshtml`.

---

## 11. Lỗi đã phát hiện và sửa trong quá trình làm

- **Python gợi ý lại khóa đã đăng ký:** khóa có điểm tương tác 0 (mới đăng ký, chưa học) vẫn bị gợi ý lại. Đã sửa và thêm test.
- **Khóa chưa ai học bị bỏ qua:** khi bù khóa phổ biến, Python bỏ qua các khóa chưa ai học. Đã sửa cho giống SQL.
- **Carousel:** bấm chuyển thật nhanh làm slide bị lệch 48px. Đã sửa.
- **Học viên không có gợi ý, `/Recommendation` trống:** do quy tắc loại mọi khóa đã tương tác. Sửa bằng migration 006 và tham số `seen_by_user` (mục 1.3).
- **Menu "Khóa học của tôi" tràn khung** khi rê vào "Khóa học đã đăng ký", và khoảng trắng hai trạng thái không đều. Sửa bằng bố cục `is-stacked`.
- **Ô tim vuông cao hơn nút giỏ hàng 12px:** do quy tắc `.details-page-main .mb-3 { margin-bottom: 12px !important }`. Sửa bằng `.course-cta-row > .btn.btn`.
- **Card danh mục trang chủ mất viền khi hover:** khung slider cắt mất viền khi card nhích lên. Sửa bằng padding / margin âm cho `.category-slider-viewport`.

---

## 12. Trang danh mục `/danh-muc/{slug}` (mới, **chưa commit**)

- **Một view cho mọi danh mục:** sau này có 100 danh mục vẫn chỉ cần một trang, nội dung render theo danh mục người dùng bấm.
- **URL thân thiện:** slug sinh từ tên danh mục (bỏ dấu, `đ` → `d`), ví dụ `/danh-muc/lap-trinh-web-cntt`.
  Truy cập bằng mã danh mục (`/danh-muc/DM01`) sẽ chuyển hướng về slug.
- **Nội dung trang:** tên và mô tả danh mục, số khóa đang mở, tab lọc **cấp độ** (`?level=`), **sắp xếp** (`?sort=` Phổ biến / Đánh giá cao / Giá tăng / Giá giảm), mục "Khám phá danh mục khác".
- Ẩn khóa đã sở hữu (kèm dòng nhắc). Danh mục tạm ngưng hoặc không tồn tại trả về 404.
- Card danh mục trên trang chủ và menu "Khám phá" đều trỏ tới trang này.
- Tách thẻ khóa học thành partial `_CourseCard` dùng chung cho `/Course` và trang danh mục.
- Khu vực Admin có `CategoryController` riêng (namespace khác), không đụng nhau.

File: `Services/SlugHelper.cs`, `App_Start/RouteConfig.cs` (route `DanhMuc` đặt trước `Default`), `Controllers/CategoryController.cs`,
`ViewModels/CategoryPageViewModels.cs`, `Views/Category/Index.cshtml`, `Views/Shared/_CourseCard.cshtml`, `ECOLearning.csproj`.

---

## 13. Cold-start: lĩnh vực quan tâm và khóa phổ biến (mới, **chưa commit**)

**Vấn đề:** học viên mới vào trang chủ chỉ thấy một khung trống "Bạn cần xem một số khóa học để hiển thị gợi ý",
và phải tự đi xem khóa học rồi quay lại mới có gợi ý. Với dữ liệu ít, cả học viên đã có tương tác cũng hay gặp khung trống.

**Cách xử lý:** mục "Khóa học dành riêng cho bạn" luôn có nội dung, nguồn đổi theo lượng dữ liệu đã biết về học viên
(`RecommendationService.GetForHome`). Thuật toán lọc cộng tác **giữ nguyên**; phần dự phòng chỉ ở tầng hiển thị.

| Bậc | Khi nào | Tiêu đề / mô tả | Nguồn |
| :-: | :--- | :--- | :--- |
| 1 | Có khóa vừa xem còn khóa tương tự | "Vì bạn đã xem X" | Lọc cộng tác (mục 1.7) |
| 2 | Có gợi ý tổng hợp đã tính sẵn | "Gợi ý dựa trên các khóa học bạn đã học, xem và đánh giá" | Lọc cộng tác (`KhoaHocKhuyenNghi`) |
| 3 | Đã chọn lĩnh vực quan tâm | "Vì bạn quan tâm <lĩnh vực>" + link "Đổi lĩnh vực" | Lọc cộng tác từ **khóa mồi** |
| 4 | Chưa đủ dữ liệu | Tiêu đề "Khóa học phổ biến", mô tả "Phổ biến với học viên mới" + link chọn lĩnh vực | Khóa phổ biến |

- **Khóa mồi:** mỗi lĩnh vực đã chọn lấy khóa nổi bật nhất (nhiều học viên quan tâm nhất, hòa thì đánh giá cao hơn) có trong
  `course_similarity`; điểm gợi ý = Σ sim(khóa mồi, j). Gợi ý vẫn do lọc cộng tác tạo ra, chỉ điểm khởi đầu lấy từ sở thích.
- **Bù khóa phổ biến:** ở bậc 1–3, lọc cộng tác trả về **ít hơn 5 khóa** thì bù khóa phổ biến **cùng lĩnh vực** trước, rồi toàn hệ thống.
  Thẻ bù có nhãn **"Phổ biến"** để phân biệt với "Phù hợp với bạn".
- **Hộp thoại "Bạn quan tâm lĩnh vực nào?"** (`Views/Shared/_InterestPicker.cshtml`):
  - Tự mở **một lần** với học viên chưa có tương tác nào và chưa trả lời lần nào (kể cả đăng nhập bằng Google).
  - Chọn 1–3 lĩnh vực (chỉ hiện danh mục đang hoạt động và có khóa đã xuất bản). Lưu xong tải lại trang.
  - "Bỏ qua", nút X, phím Esc hay bấm ra ngoài đều tính là bỏ qua, không tự mở lại.
  - Mở lại bất cứ lúc nào qua link "Đổi lĩnh vực" / "Chọn lĩnh vực quan tâm" ở trang chủ và trang `/Recommendation`.
  - Giáo viên cũng chọn được (giáo viên vẫn mua / học khóa của người khác), nhưng không tự mở.
- **Sở thích không ghi vào `TuongTacKhoaHoc`**, để không làm sai lệch tín hiệu thật trong ma trận.
- **Trang `/Recommendation`:** học viên chưa có lịch sử nhưng đã chọn lĩnh vực thì gợi ý theo khóa mồi, kèm thông báo
  "Gợi ý dựa trên lĩnh vực bạn quan tâm…". Chỉ có khóa phổ biến thì thông báo rõ lý do.
- Đã bỏ ý định "không lấy lại khóa ở mục Khóa học nổi bật nhất tuần": mục đó đang liệt kê **mọi** khóa đã xuất bản,
  loại trùng sẽ làm phần bù trống.

**Kiểm thử (tài khoản học viên tạm, đã xóa sau khi test):**
- Học viên mới: hộp thoại tự mở; mục gợi ý hiện 10 khóa "Phổ biến".
- Chọn 2 lĩnh vực → "Vì bạn quan tâm Lập trình Web & CNTT, Khoa học dữ liệu & AI", 8 khóa "Phù hợp với bạn".
- Xem một khóa → chuyển sang "Vì bạn đã xem …". Xem khóa chỉ có 3 khóa tương tự → 3 "Phù hợp với bạn" + 2 "Phổ biến" cùng lĩnh vực.
- "Bỏ qua" → lần sau không tự mở, `KhaoSatSoThich.daBoQua = 1`. Tài khoản có lịch sử (`tam.ly`) không bị hỏi.

**CSDL:** `Database/Migrations/007_GoiY_SoThichNguoiDung.sql`

| Bảng | Cột | Ý nghĩa |
| :--- | :--- | :--- |
| `SoThichNguoiDung` | `maNguoiDung`, `maDanhMuc` (PK kép, FK), `ngayChon` | Lĩnh vực học viên đã chọn |
| `KhaoSatSoThich` | `maNguoiDung` (PK, FK), `daBoQua`, `ngayCapNhat` | Đã trả lời / đã bỏ qua, để không hỏi lại |

File: `Services/InterestService.cs`, `Controllers/InterestController.cs` (`Save`, `Skip`, POST có AntiForgery),
`ViewModels/InterestViewModels.cs`, `Views/Shared/_InterestPicker.cshtml`, `Services/RecommendationService.cs`
(`GetForHome`, `Compute`, `LayTheoSoThich`, `BuPhoBien`), `ViewModels/RecommendationViewModels.cs` (`CheDoGoiY`, `GoiYTrangChuViewModel`),
`Controllers/HomeController.cs`, `Controllers/RecommendationController.cs`, `Views/Home/Index.cshtml`, `Views/Recommendation/Index.cshtml`,
`Database/HUONG_DAN_CHAY_SQL.md`, `ECOLearning.csproj`.

**Báo cáo:** sửa dòng sự kiện phụ của UC7 theo đoạn đề xuất (chọn lĩnh vực ở **lần đăng nhập đầu tiên**, khóa mồi, khóa phổ biến
có thứ tự ưu tiên, bù khi lọc cộng tác chưa đủ). Ghi rõ đây là "lọc cộng tác + chiến lược dự phòng cold-start ở tầng hiển thị";
số liệu đánh giá Precision/Recall không đổi vì chỉ đo lọc cộng tác thuần.

---

## 14. Phân hệ Marketing, giá bán và doanh thu (mới, **chưa commit**)

Làm theo `DacTa_LuongChucNang_Marketing_ECOLearning.md`. Bốn điểm đã chốt với nhóm:

| Mục đặc tả | Quyết định |
| :--- | :--- |
| Mục 3: ưu đãi học viên thân thiết | Là **một chương trình coupon riêng** có tiêu chí (số khóa đã đăng ký, tổng chi tiêu); server kiểm tra khi áp mã |
| Mục 4: chiến dịch Marketing | Tạo bảng `ChienDich`, một chiến dịch gắn nhiều chương trình khuyến mãi |
| Mục 6: giảm giá của giáo viên | **Chuyển cho Marketing.** Giáo viên chỉ đặt giá gốc; `giaKhuyenMai` do Marketing quản lý |
| Mục 7: nội dung trước khi gửi duyệt / xuất bản | **Bắt buộc có video** ở mọi bài học; tài liệu không bắt buộc |

### 14.1. Khuyến mãi và coupon (`/Marketing/Promotion`)

- Danh sách lọc theo từ khóa, trạng thái, đối tượng, khoảng ngày, chiến dịch. Trang **Chi tiết** có số lượt dùng,
  tổng tiền đã giảm, doanh thu sau giảm và lịch sử từng lần dùng.
- Tạo / sửa: giảm **%** (số nguyên 1–100, có thể đặt **giảm tối đa**) hoặc **số tiền** (≥ 1.000 đ); đơn tối thiểu;
  phạm vi **tất cả khóa** hoặc **khóa được chọn** (chỉ khóa đã xuất bản, giá > 0); giới hạn tổng lượt và số lượt mỗi học viên (mặc định 1).
- Kiểm tra: mã gồm `A–Z 0–9 _ -`, dài 3–30 ký tự, không trùng; mã đã có người dùng thì không đổi được; tổng lượt không nhỏ hơn số lượt đã dùng;
  ngày kết thúc ≥ ngày bắt đầu và không ở quá khứ; chiến dịch phải đang mở.
- **Tạm ngưng / Tiếp tục / Kết thúc** chỉ đổi trạng thái, **không xóa**, để giữ lịch sử đơn hàng. Chương trình đã kết thúc không sửa được.

### 14.2. Ưu đãi học viên thân thiết (`/Marketing/Loyalty`)

- Tạo coupon có đối tượng **Thân thiết** và ít nhất một tiêu chí: đã đăng ký từ N khóa, tổng chi tiêu từ X đ (đơn đã thanh toán).
- Trang "Học viên đạt tiêu chí" liệt kê ai đã đạt / chưa đạt, để Marketing gửi mã cho đúng người.
- Học viên chưa đạt mà áp mã sẽ nhận thông báo rõ, ví dụ "cần đã đăng ký từ 3 khóa (bạn có 0)".

### 14.3. Chiến dịch Marketing (`/Marketing/Campaign`)

- Tên, mục tiêu, nội dung, đối tượng, thời gian; gắn được nhiều chương trình khuyến mãi.
- Trang chi tiết tổng hợp lượt dùng, tiền giảm, doanh thu của các chương trình trong chiến dịch. **Kết thúc** chỉ đổi trạng thái.

### 14.4. Giá khuyến mãi khóa học (`/Marketing/CoursePrice`)

- Marketing đặt hoặc bỏ `giaKhuyenMai` cho từng khóa. Quy tắc: 0 < giá khuyến mãi < giá gốc; để trống là bỏ giảm.
- **Giáo viên:** trang tạo / sửa khóa học không còn ô giá khuyến mãi, chỉ có dòng "Giá khuyến mãi và mã giảm giá do bộ phận Marketing quản lý".
  Nếu giáo viên giảm giá gốc xuống bằng hoặc thấp hơn giá khuyến mãi thì giá khuyến mãi tự bị bỏ.

### 14.5. Giỏ hàng và thanh toán

- **Giá luôn tính lại ở server** từ CSDL (`PromotionService.GiaBanHienTai`: dùng `giaKhuyenMai` nếu 0 < giá KM < giá gốc), không tin giá trong session hay từ trình duyệt.
- Coupon được kiểm tra theo thứ tự: trạng thái → thời gian → tổng lượt → lượt của học viên → tiêu chí thân thiết → phạm vi khóa → đơn tối thiểu → giảm tối đa.
  Tiền giảm không vượt tổng tiền các khóa được áp dụng.
- Coupon đã lưu được **kiểm tra lại** mỗi lần mở giỏ và khi xóa khóa khỏi giỏ; hết hiệu lực thì tự bỏ và báo lý do.
- **Thanh toán trong một transaction**, khóa theo mã coupon (`sp_getapplock`) để hai người không cùng lúc vượt giới hạn lượt.
  Ghi `DonHang`, `ChiTietDonHang`, `DangKyKhoaHoc`, `SuDungUuDai`; một đơn chỉ dùng một coupon (UNIQUE `maDonHang`).
  Không cho mua lại khóa đã sở hữu.

**Học viên thấy mã giảm giá ở đâu** (trước đây Marketing tạo mã nhưng học viên không thấy ở đâu cả):

- Mỗi chương trình có tùy chọn **"Công khai mã"** (cột `congKhai`, mặc định bật). Bỏ chọn thì thành **mã kín**: không hiện trên hệ thống,
  Marketing tự gửi (email, mạng xã hội), học viên vẫn nhập tay được. Danh sách chương trình có nhãn "Mã kín".
- **Giỏ hàng:** khối "Mã giảm giá dành cho bạn" dưới ô nhập mã. Mỗi mã có mức giảm, tên chương trình, điều kiện, hạn dùng,
  số tiền giảm cho giỏ hiện tại và nút **"Dùng mã"**. Mã chưa đủ điều kiện bị làm mờ, kèm lý do (ví dụ "Mã áp dụng cho đơn từ 1.000.000 đ").
  Mã đang dùng đứng đầu, ghi "Đang dùng". Hiện 2 mã, còn lại bấm "Xem thêm" (khung tóm tắt dính theo màn hình nên không để quá dài).
  Danh sách tự tải lại sau khi áp mã hoặc xóa khóa khỏi giỏ.
- **Trang chi tiết khóa học:** dưới giá bán hiện tối đa 2 mã áp dụng cho khóa đó, kèm "Còn X đ khi dùng mã ở giỏ hàng" và nút "Sao chép".
  Không hiện với giáo viên chủ khóa, học viên đã đăng ký, khóa miễn phí.
- Chỉ hiện mã: công khai, đang chạy, trong thời gian, còn lượt, học viên chưa dùng hết lượt của mình.
  **Mã thân thiết chỉ hiện với học viên đã đạt tiêu chí.** Số tiền giảm hiển thị tính bằng chính `KiemTraCoupon`, nên khớp với lúc áp mã thật.
- Code: `PromotionService.LayMaChoHocVien`, `CartController.AvailableCoupons`, `Views/Cart/_CouponList.cshtml`, `CourseController.Details`.

### 14.6. Báo cáo

- **Báo cáo Marketing** (`/Marketing/CampaignReport`): lọc theo khoảng ngày, chương trình, chiến dịch; số chương trình, lượt dùng,
  doanh thu trước / sau giảm, tổng tiền ưu đãi, biểu đồ lượt dùng theo ngày, xuất CSV. **Đã bỏ toàn bộ số liệu giả** trước đây.
- **Dashboard Marketing:** dùng số liệu thật.
- **Admin → Doanh thu theo ngày** (`/Admin/RevenueReport`): chọn từ ngày – đến ngày (tính trọn ngày kết thúc, tối đa 366 ngày),
  chỉ tính đơn **Đã thanh toán**; thực thu = tổng giá khóa − tiền giảm coupon; có biểu đồ theo ngày, danh sách đơn, xuất CSV.
- **Báo cáo hệ thống** của Admin nay trừ tiền giảm coupon (phân bổ theo tỷ lệ giá từng khóa trong đơn).

### 14.7. Nội dung khóa học trước khi gửi duyệt / xuất bản

`Services/CourseContentService.cs` yêu cầu: có ít nhất 1 chương và 1 bài học; chương nào cũng có bài; **bài nào cũng có video**
(file còn trên máy chủ hoặc link http(s) hợp lệ). Kiểm tra ở 3 chỗ: nút "Gửi duyệt" khi tạo / sửa khóa học,
`SubmitForApproval`, và **Admin phê duyệt** (thiếu thì báo "Chưa thể xuất bản: …" kèm tên bài).

### 14.8. Lỗi có sẵn đã sửa

- **Mọi lần thanh toán đều lỗi:** mã đơn / chi tiết / đăng ký sinh ra dài hơn `varchar(20)`. Nay mã đơn là `DH` + `yyMMddHHmmss` + 4 số (18 ký tự).
- **Trigger ghi đè tổng tiền:** `trg_ChiTietDonHang_CapNhatTongTien` đặt `tongTien = SUM(donGia)`, làm mất tiền giảm. Đã sửa trong 008.
- **Dashboard / báo cáo Marketing hiện số giả** khi chưa có dữ liệu. Đã bỏ.
- **Báo cáo hệ thống không trừ tiền giảm.** Đã sửa.
- **Tạo nhiều chương trình / chiến dịch trong cùng một giây bị lỗi** do trùng mã (`KM`/`CD` + thời gian + 2 số `new Random()`).
  Nay dùng 4 số ngẫu nhiên lấy từ Guid và kiểm tra mã chưa tồn tại trước khi ghi (`PromotionService.HauToMaMoi`).

### 14.9. CSDL: migration 008

| Bảng | Thay đổi |
| :--- | :--- |
| `ChienDich` (mới) | `maChienDich`, `tenChienDich`, `mucTieu`, `noiDung`, `doiTuong`, `ngayBatDau`, `ngayKetThuc`, `trangThai` (HoatDong / KetThuc), `maNhanVienMarketing`, `ngayTao` |
| `SuDungUuDai` (mới) | Mỗi lần dùng coupon: đơn hàng (UNIQUE), học viên, khuyến mãi, mã giảm giá, tiền được áp dụng, tiền giảm, thời gian |
| `KhuyenMai` | Thêm `loaiUuDai`, `giamToiDa`, `donHangToiThieu`, `phamVi`, `doiTuong`, `soKhoaToiThieu`, `chiTieuToiThieu`, `trangThai`, `maChienDich`, `ngayTao`, `congKhai` (1 = học viên thấy mã). `mucUuDai` là % hoặc số tiền tùy `loaiUuDai` |
| `MaGiamGia` | Thêm `gioiHanLuotDung`, `gioiHanMoiHocVien` (mặc định 1) |
| Trigger | `tongTien = SUM(donGia) − tiền giảm trong SuDungUuDai` |

**Kiểm thử end-to-end (tài khoản Marketing / Admin / học viên tạm; toàn bộ dữ liệu test đã xóa):**
- Form: bỏ trống, trùng mã `BLF2026`, giảm 150%, ngày kết thúc trước ngày bắt đầu đều bị chặn, báo lỗi đúng ô.
- Tạo coupon `TEST10` (10%, tối đa 50.000 đ, 2 khóa, 1 lượt) và `TESTVIP` (thân thiết, ≥ 3 khóa); chiến dịch gắn `TEST10`.
- Giá khuyến mãi 700.000 cho khóa giá 690.000 bị từ chối; 600.000 được nhận; để trống thì trở về như cũ.
- Học viên mới: `TESTVIP` bị từ chối; `TEST10` giảm 50.000 đ (chạm mức tối đa); giỏ hiện 1.380.000 / −50.000 / 1.330.000;
  thanh toán thành công, `DonHang.tongTien` = 1.330.000; dùng lại mã → "đã hết lượt".
- Báo cáo Marketing, chi tiết chương trình, danh sách thân thiết, Dashboard, Admin doanh thu theo ngày đều khớp số liệu; khoảng ngày sai thì báo lỗi.
- Giáo viên gửi duyệt khóa có bài thiếu video → bị chặn, nêu tên bài. Admin phê duyệt khóa có video mất file → bị chặn.
- Hiển thị mã: mã kín `TESTKIN` không hiện nhưng nhập tay vẫn giảm 30.000 đ; `TESTUI` (20%, chỉ 1 khóa) hiện ở trang khóa đó và ở giỏ;
  `TESTMIN` (đơn từ 1.000.000 đ) bị làm mờ kèm lý do khi giỏ 690.000 đ, dùng được khi giỏ 1.380.000 đ;
  bấm "Dùng mã" → giảm 138.000 đ, thẻ chuyển "Đang dùng"; xóa khóa → mã tự bỏ, danh sách cập nhật lý do.

File chính: `Services/PromotionService.cs`, `Services/CampaignService.cs`, `Services/RevenueService.cs`, `Services/CourseContentService.cs`,
`ViewModels/PromotionModels.cs`, `ViewModels/CampaignModels.cs`, `Controllers/CartController.cs`, `Views/Cart/Index.cshtml`,
`Areas/Marketing/Controllers/{Promotion,Loyalty,Campaign,CoursePrice,CampaignReport,Dashboard,MarketingBase}Controller.cs`,
`Areas/Marketing/Views/{Promotion,Loyalty,Campaign,CoursePrice,CampaignReport,Dashboard,Shared}/…`, `Areas/Marketing/Models/MarketingReportModels.cs`,
`assets/css/marketing.css`, `Areas/Admin/Controllers/{RevenueReport,SystemReport,CourseApproval}Controller.cs`, `Areas/Admin/Views/RevenueReport/Index.cshtml`,
`Areas/Instructor/Controllers/CourseController.cs`, `Areas/Instructor/Views/Course/{Create,Edit}.cshtml`, `Database/Migrations/008_Marketing_KhuyenMai.sql`, `ECOLearning.csproj`.

---

## 15. Việc còn lại / cần quyết định

| Việc | Ghi chú |
| :--- | :--- |
| Commit trang danh mục (mục 12), cold-start (mục 13) và Marketing (mục 14) | Đang ở thư mục làm việc nhánh `phu`. Thành viên khác cần chạy migration 007, 008 |
| Thành viên khác chạy migration 006, 007 và job Python | Xem mục 0. Không chạy job thì chỉ thấy khóa phổ biến, không có gợi ý lọc cộng tác |
| Mục "Khóa học tương tự" ở trang chi tiết | Stored procedure `sp_GoiY_KhoaHocTuongTu` đã có, chỉ cần gắn vào view. Hình 4.13 trong báo cáo đang có mục này |
| Link danh mục ở footer | `_Layout.cshtml` vẫn dùng mã giả (`category = "CNTT"`, `"Design"`, `"Business"`), nên đổi sang trang danh mục |
| `ReviewController.SubmitReview` | Chỉ kiểm tra đăng nhập, **chưa kiểm tra đã đăng ký khóa và tiến độ**. Giao diện học yêu cầu 100% bài, hàm `fn_DuDieuKienDanhGia` (25%) chưa được gọi, báo cáo ghi 70%. Cần chốt một ngưỡng và kiểm tra phía server |
| Quản lý người dùng của Admin | Báo cáo có Hình 4.37–4.38 nhưng code chưa có. Làm thêm hoặc bỏ hình |
| Thanh toán | `Checkout` ghi "Đã thanh toán" ngay và **chưa tạo dòng `ThanhToan`**; `PaymentController` (VNPay/MoMo) mới là khung. Ghi rõ "thanh toán mô phỏng" trong báo cáo |
| Hoàn tiền / hủy đơn | Đặc tả có nhắc nhưng CSDL chưa có trạng thái hoàn tiền, nên báo cáo chỉ tính đơn "Đã thanh toán". Lượt coupon của đơn bị hủy chưa được trả lại |
| Ưu đãi cho "nhóm học viên được chỉ định" | Mới có tiêu chí số khóa và tổng chi tiêu; chưa có danh sách học viên chỉ định cụ thể |
| "Mở lại" khóa học sau xử lý vi phạm | Admin mở lại khóa đã tạm khóa **không** kiểm tra lại video (chỉ Gửi duyệt và Phê duyệt có kiểm tra) |
| Khóa demo đang chờ duyệt thiếu file video | Ví dụ `DMKH06`: video bài 1 không có trên máy (video không đưa lên Git, mục 6) nên Admin không duyệt được cho tới khi có file |
| Nút tim trên trang hồ sơ giáo viên | Chưa có |
| Áp dụng `is-stacked` cho menu "Khám phá" | Tùy chọn, cho chiều cao cố định giống "Khóa học của tôi" |
| Kiểm thử tay | Đồng bộ tim khi bấm nút Back (bfcache) và hover sau khi menu tải lại chưa test tự động |
| Bộ nhớ tối thiểu cho SQL Server | `EXEC sp_configure 'min server memory (MB)', 512; RECONFIGURE;` (cần bật `show advanced options`). Trị tận gốc việc chậm 25 giây, cần chủ máy đồng ý |
| Thẻ nổi "98% – Web Development Masterclass" trên banner 1 | Vẫn là dữ liệu cố định (khi đã đăng nhập) |
| Video cũ trong Git | Xem mục 6 |
| Hướng triển khai thật cho video | Azure Blob Storage / AWS S3 + link có thời hạn (đưa vào báo cáo như hướng phát triển) |
