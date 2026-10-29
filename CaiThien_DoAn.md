# Cải thiện đồ án E-Learning (14–16/10/2026)

Tóm tắt các phần đã làm thêm: chức năng gợi ý khóa học bằng lọc cộng tác (engine, CSDL, giao diện, "Vì bạn đã xem"),
thả tim khóa học, menu "Khám phá" và "Khóa học của tôi", trang danh mục, trang hồ sơ giáo viên, giao diện trang chủ,
Skeleton Screen, xử lý web chạy chậm và hoàn thiện phân hệ Marketing theo đặc tả (khuyến mãi, coupon, chiến dịch, báo cáo).

> **Trạng thái commit:** phần lớn đã commit trong `3944db4` (nhánh `phu`, đã merge qua PR #4).
> **Chưa commit:** trang danh mục `/danh-muc/{slug}`, sửa lỗi mất viền card danh mục (mục 12),
> xử lý cold-start bằng lĩnh vực quan tâm + khóa phổ biến (mục 13, migration 007)
> phân hệ Marketing / giá / doanh thu / ví mã giảm giá (mục 14, migration 008, 009) và tìm kiếm cho khách (mục 15, migration 010).
> File `Database/Seed/01_du_lieu_demo.sql` và phần "số chạy tăng dần" ở mục thống kê của `Views/Home/Index.cshtml`
> là thay đổi có sẵn từ trước, không thuộc đợt này.

---

## 0. Việc cần làm sau khi pull

1. Chạy `Database/Migrations/005_GoiY_LocCongTac.sql`, `006_GoiY_GoiYLaiKhoaChiXem.sql`, `007_GoiY_SoThichNguoiDung.sql`, `008_Marketing_KhuyenMai.sql`, `009_ViMaGiamGia.sql`, `010_TimKiem.sql` rồi `011_DanhGia_HuuIch_PhanHoi.sql` trong SSMS. Chạy lại nhiều lần vẫn an toàn, không xóa dữ liệu.
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
    Mở **sau 5 giây** kể từ khi vào trang. Nếu lúc đó đang mở hộp thoại khác (vd. xác nhận lưu mã giảm giá) thì chờ hộp thoại đó đóng.
  - Hiện **mọi danh mục đang hoạt động**, kể cả danh mục chưa có khóa ("Chưa có khóa học"); danh mục nhiều khóa xếp trước.
    Mặc định hiện 4 danh mục, còn lại bấm **"Xem thêm N lĩnh vực"** / "Thu gọn". Mở lại để đổi mà lĩnh vực đã chọn nằm trong
    phần ẩn thì danh sách mở sẵn. Chọn 1–3 lĩnh vực (chọn đủ 3 thì các ô khác bị khóa, server cũng chặn). Lưu xong tải lại trang.
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

**Học viên thu thập và dùng mã giảm giá (ví "Mã giảm giá của tôi", migration 009):**

Luồng: Marketing tạo mã công khai → học viên **lưu mã** vào ví → ở bước thanh toán mở mục **Mã khuyến mãi**, chọn mã trong ví và bấm **Áp dụng**.

- **Mã công khai / mã kín:** tùy chọn "Công khai mã" (cột `congKhai`, mặc định bật). Mã kín không hiện ở đâu, Marketing tự gửi,
  học viên nhập tay ở hộp chọn mã. Danh sách chương trình có nhãn "Mã kín".
- **Trang chủ:** mục **"Mã khuyến mãi"** nằm ngay dưới "Danh mục đào tạo" (chỉ học viên / giáo viên đã đăng nhập), mã chưa lưu đứng trước,
  có link "Mã giảm giá của tôi". Hiện dạng **slider**: thẻ mã thu nhỏ, **3 mã một hàng** (2 trên tablet, 1 trên điện thoại); nhiều hơn thì có
  **mũi tên ← →** (mỗi lần dời đúng một lượt), vuốt được trên cảm ứng; tên / điều kiện dài cắt bằng "…". Dùng lại slider `.course-slider` của trang chủ
  (thêm biến CSS `--slide-step`; hai slider khóa học giữ cách bước cũ).
- **Lưu mã:** bấm "Lưu mã" → hộp thoại xác nhận *"Sau khi lưu, mã không thể xóa khỏi danh sách và thao tác này không thể hoàn tác"*
  → Hủy hoặc Lưu mã. Lưu xong mọi nút cùng mã trên trang chuyển "Đã lưu". **Không có thao tác xóa / hoàn tác**, server cũng không có endpoint xóa.
  Server kiểm tra lại: mã công khai, đang chạy, còn lượt, đủ tiêu chí thân thiết, chưa dùng hết lượt; lưu lần hai trả "đã có trong ví".
- **Menu Khám phá → "Mã giảm giá của tôi"** (`/ma-giam-gia-cua-toi`): tab **Có thể dùng** (có nút "Dùng ngay", gồm mã sắp diễn ra),
  **Đã dùng**, **Hết hiệu lực** (hết hạn / hết lượt / tạm ngưng / đã kết thúc), **Thu thập thêm** (mã công khai chưa lưu).
- **Chi tiết mã** (`/ma-giam-gia/{mã}`): thẻ mã, **Chính sách áp dụng** do hệ thống sinh từ cấu hình (mức ưu đãi, thời gian, khóa học áp dụng,
  đơn tối thiểu, đối tượng, số lượt, cách dùng), **Mô tả chương trình** do Marketing viết (cột `chinhSach`, ô mới trong form),
  danh sách khóa áp dụng, **Quy định chung**, tình trạng của học viên và lịch sử dùng. Mã kín chưa lưu hoặc mã thân thiết chưa đạt tiêu chí → 404.
- **Giỏ hàng / thanh toán:** khung tóm tắt chỉ có mục **"Mã khuyến mãi"**: nút "Chọn hoặc nhập mã" (kèm "N mã dùng được").
  Bấm mới mở hộp thoại **Chọn mã khuyến mãi**: ô nhập tay (mã kín) + **chỉ mã trong ví**, mỗi mã có số tiền giảm cho giỏ hiện tại
  và nút **Áp dụng**; mã chưa đủ điều kiện bị khóa nút, kèm lý do. Áp xong hiện mã đang dùng với "Đổi" / "Bỏ" (`Cart/RemoveCoupon`).
- **Trang chi tiết khóa học:** không hiển thị mã khuyến mãi (đã bỏ theo yêu cầu); học viên thu thập mã ở trang chủ hoặc trang "Mã giảm giá của tôi".
- Mã dùng khi thanh toán (kể cả nhập tay) được tự đưa vào ví để có lịch sử. Số tiền giảm hiển thị luôn tính bằng `KiemTraCoupon`, khớp lúc áp mã thật.
- Code: `Services/PromotionService.ViMa.cs` (`LayMaChoHocVien`, `LayViCuaToi`, `LuuMa`, `LayChiTiet`), `Controllers/CouponController.cs`,
  `Views/Coupon/*`, `Views/Shared/_CouponTicket.cshtml`, `Views/Shared/_CouponCollectModal.cshtml`, `assets/js/coupon-wallet.js`,
  `assets/css/coupon.css`, `Views/Cart/_CouponList.cshtml`, `App_Start/RouteConfig.cs` (route `ma-giam-gia-cua-toi`, `ma-giam-gia/{code}`).

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

### 14.9. CSDL: migration 008 và 009

| Bảng | Thay đổi |
| :--- | :--- |
| `ChienDich` (mới) | `maChienDich`, `tenChienDich`, `mucTieu`, `noiDung`, `doiTuong`, `ngayBatDau`, `ngayKetThuc`, `trangThai` (HoatDong / KetThuc), `maNhanVienMarketing`, `ngayTao` |
| `SuDungUuDai` (mới) | Mỗi lần dùng coupon: đơn hàng (UNIQUE), học viên, khuyến mãi, mã giảm giá, tiền được áp dụng, tiền giảm, thời gian |
| `KhuyenMai` | Thêm `loaiUuDai`, `giamToiDa`, `donHangToiThieu`, `phamVi`, `doiTuong`, `soKhoaToiThieu`, `chiTieuToiThieu`, `trangThai`, `maChienDich`, `ngayTao`, `congKhai` (1 = học viên thấy mã). `mucUuDai` là % hoặc số tiền tùy `loaiUuDai` |
| `ViMaGiamGia` (mới, **009**) | Ví mã của học viên: `maHocVien`, `maGiamGia` (PK kép), `ngayLuu`. Không xóa được |
| `KhuyenMai.chinhSach` (**009**) | Mô tả chính sách Marketing viết cho học viên, hiện ở trang chi tiết mã |
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
- Ví mã: mục mã ở trang chủ nằm ngay sau "Danh mục đào tạo"; bấm "Lưu mã" → hộp thoại xác nhận, Hủy thì không lưu, Lưu thì nút thành "Đã lưu";
  lưu lại lần hai → "đã có trong ví"; lưu mã kín qua API → bị từ chối; xem chi tiết mã kín chưa lưu → 404.
- Giỏ trước khi lưu mã: "Chưa có mã dùng được", hộp chọn mã trống kèm link thu thập. Sau khi lưu `TESTWALLET` (20%, tối đa 100.000 đ):
  "1 mã dùng được", Áp dụng → giảm 100.000 đ (690.000 → 590.000), Bỏ → về 690.000; nhập tay mã kín `TESTKIN` → giảm 30.000 đ; mã sai → báo lỗi trong hộp.
- Thanh toán với `TESTWALLET` → mã chuyển sang tab "Đã dùng", trang chi tiết hiện lịch sử đơn và "Đã dùng 1/1 lượt".

File chính: `Services/PromotionService.cs`, `Services/CampaignService.cs`, `Services/RevenueService.cs`, `Services/CourseContentService.cs`,
`ViewModels/PromotionModels.cs`, `ViewModels/CampaignModels.cs`, `Controllers/CartController.cs`, `Views/Cart/Index.cshtml`,
`Areas/Marketing/Controllers/{Promotion,Loyalty,Campaign,CoursePrice,CampaignReport,Dashboard,MarketingBase}Controller.cs`,
`Areas/Marketing/Views/{Promotion,Loyalty,Campaign,CoursePrice,CampaignReport,Dashboard,Shared}/…`, `Areas/Marketing/Models/MarketingReportModels.cs`,
`assets/css/marketing.css`, `Areas/Admin/Controllers/{RevenueReport,SystemReport,CourseApproval}Controller.cs`, `Areas/Admin/Views/RevenueReport/Index.cshtml`,
`Areas/Instructor/Controllers/CourseController.cs`, `Areas/Instructor/Views/Course/{Create,Edit}.cshtml`, `Database/Migrations/008_Marketing_KhuyenMai.sql`, `Database/Migrations/009_ViMaGiamGia.sql`, `ECOLearning.csproj`.

---

## 15. Tìm kiếm khóa học: gợi ý khi gõ và trang /tim-kiem (mới, **chưa commit**)

**Yêu cầu:** khách chưa đăng nhập vẫn tìm được; khi gõ hiện tối đa **7 từ khóa** liên quan và **5 khóa học** có tên gần đúng
(chỉ khóa chưa mua / đăng ký), thêm tối đa **2 giảng viên**; Enter mở trang kết quả.

**Đã chốt với nhóm:** khách xem được trang chi tiết khóa học, trang danh mục và hồ sơ giáo viên (chỉ Mua / Thêm giỏ / Thả tim / Vào học
mới bắt đăng nhập); từ khóa gợi ý lấy từ **lịch sử tìm kiếm + dữ liệu khóa học**; trang kết quả riêng `/tim-kiem`; dropdown có giảng viên.

### 15.1. Cách tìm (Services/SearchService.cs, Services/SearchText.cs)

- **Chỉ mục trong bộ nhớ**, dựng từ CSDL và cache 2 phút (`HttpRuntime.Cache`): khóa đang bán (tên, danh mục, giáo viên, mô tả ngắn,
  mô tả đã bỏ thẻ HTML), danh mục, giáo viên, cụm từ trong tên khóa, từ vựng. Dữ liệu cỡ vài trăm khóa nên đủ nhanh; khi lớn hơn thay bằng
  SQL Server Full-Text Search / Elasticsearch mà không đổi giao diện.
- **Không dấu:** "lap trinh" khớp "Lập trình", "đ" → "d"; giữ ký hiệu "C#", "C++"; "ASP.NET" → "asp net".
- **Gần đúng:** từ cuối khớp **tiền tố** (đang gõ dở: "lap tr"); từ dài cho phép **gõ sai** (4–7 ký tự sai 1, từ 8 ký tự sai 2,
  khoảng cách Levenshtein): "pythn" → Python, "javscript" → JavaScript. Bỏ hư từ ("và", "với", "the", "of"...).
- **Điểm liên quan** = Σ trọng số trường × độ khớp (tên 10, danh mục 6, giáo viên 5, mô tả ngắn 2.5, mô tả 1;
  trùng 1 / tiền tố 0.85 / gõ sai 0.6), **+12** nếu cả cụm nằm liền trong tên (+4 nếu ở đầu tên), cộng nhẹ số học viên và điểm đánh giá.
- Mặc định phải **khớp mọi từ**; không có khóa nào thì nới thành khớp từ nửa số từ và báo "khớp một phần".
- **Ẩn khóa đã đăng ký** và khóa do chính giáo viên dạy (khách thấy tất cả).

### 15.2. Gợi ý khi gõ (assets/js/search-suggest.js, `GET /Search/Suggest?q=`)

- **7 từ khóa** = từ khóa nhiều người đã tìm (bảng `TuKhoaTimKiem`) + cụm 1–3 từ trong tên khóa / tên danh mục.
  Cụm được cắt theo **từ ghép tiếng Việt** (`SearchText.TuGhep`: "cơ bản", "lập trình", "hiện đại"...) để không ra "figma cơ".
- **5 khóa học** (ảnh, tên, giảng viên) xếp theo điểm liên quan; **2 giảng viên** có tên khớp (ảnh, số khóa).
- Kỹ thuật phía trình duyệt: chờ ngừng gõ 180 ms, hủy request cũ (`AbortController`), bỏ kết quả về muộn, cache 30 câu;
  phím ↑ ↓ / Enter / Esc; ARIA `combobox` / `listbox` / `aria-activedescendant`; dựng DOM bằng `textContent` (không chèn HTML).
  Phần người dùng đã gõ được tô đậm. Hộp tự canh trong màn hình hẹp.

### 15.3. Thống kê từ khóa (migration 010, bảng `TuKhoaTimKiem`)

- Ghi khi **mở trang kết quả** (không ghi mỗi phím gõ), mỗi phiên chỉ cộng 1 lượt cho một từ khóa, gộp theo dạng không dấu (`MERGE`).
- **Không lưu ai tìm** (không có mã người dùng).
- Chỉ gợi ý lại từ khóa **có kết quả khớp đủ** và **mọi từ đều có trong dữ liệu khóa học**: câu gõ sai ("pythn"), câu rác,
  câu chỉ khớp một phần không lan sang người khác.

### 15.4. Trang kết quả `/tim-kiem?q=` (Views/Search/Index.cshtml)

- Tiêu đề + số kết quả, chip **danh mục liên quan**, bộ lọc **danh mục / cấp độ / giá / đánh giá** (đếm số khóa từng mục,
  chọn là áp dụng), **sắp xếp** (liên quan, nhiều học viên, đánh giá, giá tăng / giảm), **phân trang** 12 khóa / trang.
- Không có kết quả: "Có phải bạn muốn tìm: …" (sửa từ gõ sai theo từ vựng), từ khóa được tìm nhiều, khóa phổ biến.
- Cho phép tìm chuỗi có ký tự đặc biệt (`<div>`, `C++`): tắt Request Validation cho 2 action này, mọi chỗ xuất ra đều encode.

### 15.5. Mở trang cho khách

- `Course/Details`, `/danh-muc/{slug}`: bỏ bắt đăng nhập. Nút Mua / Thêm giỏ / Thả tim / Đăng ký học vẫn yêu cầu đăng nhập như cũ.
- `User/Index`, `User/Courses` (hồ sơ giáo viên): khách **chỉ xem được tài khoản giáo viên** đang hoạt động (học viên → 404);
  **email / số điện thoại chỉ hiện khi đã đăng nhập**.
- Header: ô tìm kiếm gửi `q` tới `/tim-kiem` (trước đây gửi tới `/Course` vốn bắt đăng nhập).

**Kiểm thử (khách + học viên tạm, dữ liệu test đã xóa):** gợi ý "web", "lap trinh", "pythn", "javscript", "tran minh" (ra giảng viên),
"figma", "c#", "thiet ke ui" đúng; gõ thật "lap tr" → dropdown 4 từ khóa + 5 khóa, ↓↓ Enter mở từ khóa thứ 2; trang kết quả "lap trinh"
8 khóa, lọc "Trung cấp" 2 khóa, sắp xếp giá; "figmaa photoshopp" → khớp một phần và không được gợi ý lại; "xyzabc" → trang không có
kết quả + khóa phổ biến; `<script>` hiển thị an toàn; khách mở chi tiết khóa / danh mục / hồ sơ giáo viên (không thấy email), hồ sơ học viên → 404;
học viên đã đăng ký DMKH01 không còn thấy khóa đó (8 → 7 kết quả).

File: `Services/SearchService.cs`, `Services/SearchText.cs`, `Controllers/SearchController.cs`, `ViewModels/SearchViewModels.cs`,
`Views/Search/Index.cshtml`, `assets/js/search-suggest.js`, `assets/css/search.css`, `Views/Shared/_Layout.cshtml`,
`App_Start/RouteConfig.cs` (route `tim-kiem`), `Controllers/CourseController.cs`, `Controllers/CategoryController.cs`,
`Controllers/UserController.cs`, `Views/User/Index.cshtml`, `Database/Migrations/010_TimKiem.sql`, `ECOLearning.csproj`.

---

## 16. Trang chi tiết khóa học: "Thường xuyên được mua cùng nhau" và "Học viên khác cũng mua" (mới, **chưa commit**)

Hai mục ở cuối trang, **cùng là lọc cộng tác Item-based trên dữ liệu MUA** (đơn hàng "Đã thanh toán"), khác nhau ở điều kiện "mua chung".
Không dùng luật kết hợp (Apriori) vì hiện **không có đơn nào mua từ 2 khóa** (36 đơn, mỗi đơn 1 khóa) nên luật theo đơn hàng sẽ luôn trống.

| Mục | Hai khóa được tính là mua chung khi | Ngưỡng |
| :--- | :--- | :--- |
| **Thường xuyên được mua cùng nhau** (đặt trên) | Cùng một học viên mua cả hai, **cách nhau ≤ 30 ngày** | ≥ 1 học viên |
| **Học viên khác cũng mua** (đặt dưới) | Cùng một học viên mua cả hai, bất kỳ lúc nào; **bỏ các khóa đã có ở mục trên** | ≥ 2 học viên (như `min_common_users`) |

- **Độ tương đồng:** `sim(i, j) = |U(i) ∩ U(j)| / sqrt(|U(i)| · |U(j)|)`, cosine trên vector nhị phân "học viên đã mua"; xếp giảm dần, mỗi mục tối đa 5 khóa.
- **Vì sao là Item-based, không phải User-based:** đầu vào là **khóa đang xem**, so cột (khóa) với cột; danh sách giống nhau với mọi người xem,
  kể cả khách. Đây chính là tính năng "Customers who bought this item also bought" (Linden, Smith, York 2003, *Item-to-Item Collaborative Filtering*).
- Không gợi ý: khóa đang xem, khóa người xem đã đăng ký, khóa do chính họ dạy, khóa không còn bán (tạm khóa, khóa vĩnh viễn).
- Mục không có dữ liệu thì **ẩn** (không bù khóa phổ biến để tiêu đề luôn đúng).
- **Giao diện:** hai mục nằm **cuối cột trái** (dưới Đánh giá). Mỗi khóa một dòng: ảnh nhỏ, tên, điểm ★, số học viên,
  tổng thời lượng bài học ("1,5 giờ" / "39 phút"), "Đã cập nhật: M/yyyy" (lần duyệt gần nhất), giá bán (giá gạch nếu đang giảm).
  Rê chuột hiện lý do ("2 học viên đã mua cả hai khóa").
- **Combo "Thường xuyên được mua cùng nhau":** khóa đang xem (nhãn "Khóa học này", nếu người xem còn mua được) + tối đa 3 khóa hay mua cùng,
  nối bằng dấu "+". Mỗi khóa: ảnh, tên, mô tả ngắn, giảng viên, nhãn "Bán chạy" (≥ 3 học viên) / "Đánh giá cao" (≥ 4.5), điểm, số đánh giá,
  thời lượng, giá và nút **"Thêm vào giỏ hàng"**. Cuối mục: **Tổng (N khóa)** giá bán + giá gốc gạch, nút **"Thêm tất cả vào giỏ hàng"**
  (`Cart/AddManyToCart`, kiểm tra từng khóa: đang bán, không phải khóa mình dạy, chưa sở hữu). Khóa đã trong giỏ hiện "Đã trong giỏ";
  đủ hết thì nút tổng thành "Đến giỏ hàng"; thêm khóa đang xem thì nút giỏ ở thẻ bên phải cũng đổi. Khách: các nút dẫn tới đăng nhập.
  `AddToCart` nay cũng chặn khóa đã sở hữu (trước đây chỉ chặn lúc thanh toán).
- **Thẻ "Mua ngay / Thêm vào giỏ hàng" dính khi cuộn:** trước đây `.sticky-course-card` có `position: sticky` nhưng không dính,
  vì lưới 2 cột dùng `align-items: flex-start` nên cột phải chỉ cao bằng thẻ. Thêm `align-self: stretch` cho cột phải; màn hình ≤ 991 px thì không dính.
- Mỗi mục có nút **Thu gọn / Hiện N khóa học** (`aria-expanded`), lựa chọn nhớ trên trình duyệt (localStorage).
- Ma trận mua dựng từ CSDL, cache 10 phút trong bộ nhớ; dữ liệu đơn hàng nhỏ nên tính trực tiếp, không cần job riêng.
  Khác ma trận `course_similarity` của job Python (dựa trên mọi tương tác: xem, học, đánh giá, tim) dùng ở trang chủ / `/Recommendation`.

**Kiểm thử (khách chưa đăng nhập, so với kết quả tính bằng SQL):** DMKH12 → mua cùng nhau: DMKH10, DMKH02, DMKH04; cũng mua: DMKH05
(DMKH09, DMKH13 đang khóa bị loại). DMKH01 → mua cùng nhau: DMKH10; cũng mua: DMKH02, DMKH05, DMKH04. DMKH03 không có dữ liệu → hai mục ẩn.
Thu gọn → "Hiện 3 khóa học", tải lại trang vẫn thu gọn. Cuộn tới hai mục, thẻ mua vẫn ở top 96 px; thu gọn / hiện không làm thẻ bên phải
nhảy (bù khoảng trống cuối cột trái, giữ vị trí cuộn). Combo DMKH12: tổng 2.560.000 đ (gốc 3.360.000 đ); thêm 1 khóa → giỏ 1; thêm tất cả → giỏ 4,
tạm tính 2.560.000 đ; API thêm khóa đã sở hữu bị chặn.

File: `Services/RelatedCourseService.cs`, `ViewModels/RelatedCourseViewModels.cs`, `Views/Course/_RelatedCourses.cshtml`,
`Views/Course/Details.cshtml` (CSS `.rel-*`, sticky cột phải, script thu gọn), `Controllers/CourseController.cs` (`Details`), `Controllers/CartController.cs` (`AddManyToCart`, `ThemKhoaVaoGio`), `ECOLearning.csproj`.

**Báo cáo:** mục 3.x có thể trình bày hai biến thể Item-based (theo khoảng thời gian mua và toàn bộ lịch sử); luật kết hợp đưa vào hướng phát triển
khi có đủ đơn nhiều khóa.

---

## 17. Trang chi tiết khóa học: mục Giảng viên và Đánh giá (hữu ích, phản hồi của giáo viên) (mới, **chưa commit**)

### 17.1. Giảng viên + "Các khóa học khác của …"

- Đặt sau "Mô tả chi tiết", trước "Đánh giá". Tên (link hồ sơ, tab mới), chuyên môn, ảnh, **xếp hạng giảng viên** (điểm trung bình
  trên mọi lượt đánh giá các khóa đang bán), **số đánh giá**, **số học viên** (không trùng), **số khóa học**, tiểu sử thu gọn có "Hiện thêm".
- Số liệu lấy từ CSDL bằng `Services/InstructorProfileService.cs` — tách từ `UserController.BuildProfile` để trang hồ sơ `/User` và
  trang chi tiết **cùng một cách tính**. Chuyên môn / tiểu sử chỉ hiện khi giáo viên đã khai (`DangKyGiaoVien`), **không điền câu mặc định**
  (trang hồ sơ vẫn giữ câu mặc định cũ).
- "Các khóa học khác của …": tối đa 3 thẻ (khóa đang bán, trừ khóa đang xem), nhiều hơn có "Xem tất cả" → `/User/Courses/{id}`.
  **Giáo viên chỉ có một khóa thì ẩn mục này.**

### 17.2. Đánh giá từ học viên

- Trang chi tiết hiện tổng quan (điểm, phân bố sao) + **5 đánh giá mới nhất**; **nhiều hơn 5** thì có nút **"Xem tất cả N đánh giá"**
  → trang `/Course/Reviews/{id}`: lọc theo số sao (bấm thanh phân bố), tìm nội dung / tên không phân biệt dấu, sắp xếp (mới nhất, hữu ích nhất,
  điểm cao / thấp), phân trang 10 đánh giá. Lọc và phân trang làm trong SQL (`OFFSET / FETCH`).
- **"Bạn thấy hữu ích?"** 👍 / 👎: người dùng đã đăng nhập chọn một trong hai, bấm lại để bỏ, bấm cái kia để đổi; hiện số lượt.
  Không tự chấm đánh giá của mình (hiện "Đánh giá của bạn · N người thấy hữu ích"). Khách bấm → đăng nhập rồi quay lại `#danh-gia`.
- **Phản hồi của giáo viên:** chỉ **giáo viên dạy khóa đó** thấy nút "Phản hồi đánh giá", viết / sửa ("đã sửa") / xóa; mỗi đánh giá tối đa
  một phản hồi, tối đa 1000 ký tự; hiển thị dưới đánh giá cho mọi người. Server kiểm tra quyền (học viên / giáo viên khác gọi API bị từ chối).
- An toàn: POST có AntiForgeryToken; nội dung luôn được encode (Razor / `textContent`), gõ `<b>` hiện đúng chữ; ghi bình chọn có khóa
  `UPDLOCK, HOLDLOCK` để hai lần bấm liên tiếp không tạo trùng.

**CSDL — migration 011** (`011_DanhGia_HuuIch_PhanHoi.sql`):

| Bảng | Cột | Ghi chú |
| :--- | :--- | :--- |
| `DanhGiaHuuIch` | `maDanhGia`, `maNguoiDung` (PK kép), `huuIch` (1 / 0), `ngayCapNhat` | FK đánh giá `ON DELETE CASCADE` |
| `PhanHoiDanhGia` | `maDanhGia` (PK), `maGiaoVien`, `noiDung` (≤ 1000), `ngayTao`, `ngayCapNhat` | FK đánh giá `ON DELETE CASCADE` |

`ON DELETE CASCADE` để xóa đánh giá (vd. `99_xoa_du_lieu_demo.sql`) không bị khóa ngoại chặn.

**Kiểm thử (khách, 4 học viên tạm có tiến độ học thật để qua trigger 25%, giáo viên demo; dữ liệu test đã xóa):**
DMKH12 → Trần Minh Khoa: số liệu khớp SQL; 3 khóa khác; DMKH05 (Phạm Quốc Bảo, 1 khóa) ẩn mục khóa khác. 6 đánh giá → hiện 5 + "Xem tất cả 6".
Thích → bỏ → không thích → đổi thích đúng; 2 người thích đếm 2; tự chấm mình bị chặn. Giáo viên phản hồi / sửa / xóa; học viên và giáo viên khác
gọi API bị chặn. Trang tất cả: lọc 3 sao ra 2, tìm "rat hay" ra "rất hay", sắp "hữu ích nhất" đúng thứ tự.

**Dữ liệu hiện tại:** mỗi khóa có tối đa 4 đánh giá nên nút "Xem tất cả" chưa xuất hiện; không thêm đánh giá ảo — nút tự hiện khi khóa có trên 5 đánh giá.

File: `Services/InstructorProfileService.cs`, `Services/CourseReviewService.cs`, `Services/ReviewTime.cs`, `ViewModels/CourseReviewViewModels.cs`,
`Views/Course/_InstructorInfo.cshtml`, `_ReviewSection.cshtml`, `_ReviewItem.cshtml`, `Reviews.cshtml`, `Views/Course/Details.cshtml`,
`assets/css/course-reviews.css`, `assets/js/course-reviews.js`, `Controllers/CourseController.cs` (`Details`, `Reviews`),
`Controllers/ReviewController.cs` (`Helpful`, `Reply`, `DeleteReply`), `Controllers/UserController.cs`, `Database/Migrations/011_DanhGia_HuuIch_PhanHoi.sql`.

---

## 18. Thông tin ngân hàng của giáo viên: trang đăng ký `/Instructor/TeacherApplication/Register` và trang hồ sơ (mới, **chưa commit**)

- **Màu sắc:** bỏ màu xanh ngọc `#0D9488`, dùng màu chủ đạo `#434CE8` của hệ thống (banner, số bước, nút, ô chọn); trang trạng thái hồ sơ cũng đổi theo.
- **Thông tin nhận thanh toán** (trước đây gõ tự do, dữ liệu thật đã có "VietComBank", "ACB Chi nhánh Gò Mây", "Techcombank Chi nhánh TP.HCM"):
  - **Ngân hàng chọn từ danh mục** `Services/BankDirectory.cs` (46 ngân hàng, mã theo VietQR / Napas): ô tìm có gợi ý, gõ mã / tên viết tắt /
    tên đầy đủ, có dấu hay không ("vcb", "ngoai thuong", "a chau"); xếp theo mức khớp (gõ "mb" ra MB Bank trước); ↑ ↓ Enter Esc.
    **Không cho gõ tên ngân hàng tự do** (đã bỏ lựa chọn "Ngân hàng khác"); cần thêm ngân hàng thì bổ sung vào `BankDirectory`.
    Lưu tên viết tắt chuẩn (vd. `Vietcombank`).
  - **Dùng chung** ở trang đăng ký và **trang hồ sơ giáo viên** (phần "Thông tin nhận thù lao"): `Views/Shared/_BankPicker.cshtml`,
    `assets/js/bank-picker.js`, `assets/css/bank-picker.css`; server kiểm tra như nhau (`TeacherApplicationController.Register`,
    `ProfileController.UpdateTeacherProfile` nhận `bankCode`, không còn nhận tên ngân hàng gõ tay).
  - **Dữ liệu cũ gõ tự do** ("VietComBank", "ACB Chi nhánh Gò Mây", "Techcombank Chi nhánh TP.HCM"): `BankDirectory.DoanNganHang`
    nhận ra ngân hàng (trùng tên / mã, hoặc tên viết tắt đứng đầu) để chọn sẵn, kèm ghi chú vàng "Đã nhận ra … — vui lòng kiểm tra lại rồi lưu";
    không nhận ra thì nhắc chọn lại. Dữ liệu trong CSDL chỉ đổi khi giáo viên bấm lưu.
  - **Số tài khoản:** tự bỏ ký tự không phải số khi gõ / dán, 6–19 chữ số, bàn phím số trên điện thoại.
  - **Tên chủ tài khoản:** điền sẵn từ họ tên tài khoản ở dạng IN HOA KHÔNG DẤU ("Nguyễn Thị Đào" → "NGUYEN THI DAO"); chuẩn hóa khi rời ô
    (không đổi trong lúc gõ để không phá bộ gõ tiếng Việt).
  - **Khung xem trước** "Doanh thu sẽ được chuyển tới: Vietcombank · STK 1012 3456 789 · NGUYEN THI DAO" để tự soát trước khi gửi.
  - Server **kiểm tra và chuẩn hóa lại** tất cả (mã ngân hàng giả, số tài khoản sai, tên rỗng đều bị chặn), không tin dữ liệu trình duyệt.
- **Sửa lỗi:** ô "Tên chủ tài khoản" có 2 thuộc tính `style` nên mất định dạng; nộp bị lỗi thì **giữ lại mọi giá trị đã nhập**
  (trước đây xóa sạch); hồ sơ bị từ chối khi nộp lại được **điền sẵn** thông tin cũ; lỗi hiện ngay dưới từng ô.
- **Hộp thoại "Điều khoản hợp tác đối tác giảng dạy"** (link trong ô cam kết, trước đây là `href="#"`): 8 mục (phạm vi, điều kiện, nội dung khóa học,
  doanh thu và thanh toán, chất lượng, vi phạm, bảo mật, thay đổi / chấm dứt). Số liệu lấy đúng theo hệ thống: giáo viên nhận **80%** doanh thu,
  nền tảng 20% (`PayoutController`), rút tối thiểu **500.000đ**, khóa phải có video và được Admin duyệt, tạm khóa / khóa vĩnh viễn khi vi phạm.
  Nút "Tôi đã đọc và đồng ý" chỉ bật khi đã cuộn hết nội dung và tự tích ô cam kết; đóng bằng nút X, "Đóng", Esc hoặc bấm ra ngoài.

**Kiểm thử (giáo viên / học viên tạm, dữ liệu test đã xóa, `App_Data/TeacherApplications.json` khôi phục):** hồ sơ "ACB Chi nhánh Gò Mây" → chọn sẵn ACB
+ ghi chú; gõ tên tự do → lỗi, không lưu được; chọn "quan doi" → MB Bank, lưu `MB Bank` / `0720015799` / `TRAN THI HONG NHUNG`; gọi API với tên tự do
→ bị chặn. Nộp lại hồ sơ bị từ chối "Techcombank Chi nhánh TP.HCM" → chọn sẵn Techcombank, điền sẵn các ô khác.

File: `Services/BankDirectory.cs`, `ViewModels/TeacherRegisterViewModels.cs` (`BankPickerViewModel`), `Views/Shared/_BankPicker.cshtml`,
`assets/js/bank-picker.js`, `assets/css/bank-picker.css`, `Areas/Instructor/Controllers/TeacherApplicationController.cs`,
`Areas/Instructor/Views/TeacherApplication/Register.cshtml`, `Status.cshtml`, `Controllers/ProfileController.cs`, `Views/Profile/Index.cshtml`.

---

## 19. Phân trang danh sách khóa học và trang "Tất cả lĩnh vực" (mới, **chưa commit**)

### 19.1. Phân trang 12 khóa / trang

- **Trang áp dụng:** `/danh-muc/{slug}`, `/Course`, `/Learning/MyCourses`. Có từ 13 khóa trở lên thì sang trang tiếp theo.
- **Dùng chung** `ViewModels/PhanTrangViewModel.cs`, `Views/Shared/_Pagination.cshtml` và `assets/css/pagination.css`:
  - thanh "‹ Trước 1 … 4 [5] 6 … 10 Sau ›", kèm dòng "Hiển thị 13–24 trong 40 khóa học";
  - link giữ mọi bộ lọc / sắp xếp trên URL, chỉ đổi `?trang=`;
  - bấm sang trang thì nhảy tới đầu danh sách (`#ds-khoa-hoc`);
  - số trang sai (`trang=99`, `-3`, `abc`) được đưa về trang hợp lệ gần nhất;
  - đổi bộ lọc hoặc sắp xếp thì quay về trang 1.
- **`/Course`:** lọc "Đánh giá tối thiểu" chuyển xuống SQL (trước đây lọc trong bộ nhớ sau khi tải hết). Đếm tổng rồi chỉ tải 12 khóa của trang (OFFSET/FETCH).
  - Sắp xếp theo giá có thêm khóa phụ là mã khóa để thứ tự giữa các trang không nhảy.
  - Đầu danh sách hiện "Tìm thấy N khóa học • Trang x/y".
- **`/Learning/MyCourses`:**
  - Hai tab "Đang học" / "Đã hoàn thành" thành link (`?tab=hoan-thanh`), mỗi tab phân trang riêng.
  - Tiến độ của mọi khóa tính bằng 3 truy vấn cho cả trang. Trước đây là 3 truy vấn cho mỗi khóa.
  - Thứ tự bài theo thứ tự chương, như trang Study.

### 19.2. Trang "Tất cả lĩnh vực" `/danh-muc`

- **Nút "Tất cả các lĩnh vực"** đặt cạnh mũi tên ở mục "Khám phá theo lĩnh vực" trang chủ. Nút luôn hiện, mũi tên vẫn chỉ hiện khi có trên 4 danh mục.
- **Trang mới** `CategoryController.All`, `Views/Category/All.cshtml`, route `TatCaDanhMuc`. Trang mở cho cả khách.
  - Mỗi lĩnh vực đang hoạt động là một thẻ gồm biểu tượng, tên, mô tả, số khóa, số giảng viên và số học viên. Bấm thẻ sang `/danh-muc/{slug}`.
  - Số khóa bỏ khóa người dùng đã sở hữu, khớp với trang danh mục.
  - Lĩnh vực chưa có khóa vẫn hiện, ghi "Chưa có khóa học".
  - Lĩnh vực có nhiều khóa xếp lên trước.
- **Biểu tượng:** dùng chung `IconDanhMuc`, trang chủ cũng sắp danh mục theo mã, nên một lĩnh vực mang cùng biểu tượng ở cả hai nơi.
- **Breadcrumb:** trang danh mục đổi "Khóa học" thành "Tất cả lĩnh vực". `/danh-muc/` không có slug giờ ra trang này.

**Kiểm thử (14 khóa tạm `TESTPG01–14` và 2 học viên tạm, đã xóa):**
- Danh mục 21 khóa ra 2 trang (12 + 9), giữ `sort` khi sang trang. Link cũ `/danh-muc/DM01?trang=2` chuyển về đúng trang 2.
- `/Course` có 24 khóa ra 2 trang. Tìm "test" + giá giảm dần ở trang 2 cho 13–14 / 14 khóa.
- Khóa học của tôi có 14 khóa đang học ra 2 trang.
- Tiến độ học viên thật (an.nguyen) trùng với SQL: 2/5, 5/6, 5/5, 6/6.
- Ở 390 px thanh phân trang nằm gọn trong màn hình.

File: `ViewModels/PhanTrangViewModel.cs`, `Views/Shared/_Pagination.cshtml`, `assets/css/pagination.css`, `Views/Shared/_Layout.cshtml`,
`Controllers/CategoryController.cs` (`Index`, `All`), `ViewModels/CategoryPageViewModels.cs` (`IconDanhMuc`, `LinhVucItem`, `TatCaLinhVucViewModel`),
`Views/Category/Index.cshtml`, `Views/Category/All.cshtml`, `App_Start/RouteConfig.cs`, `Controllers/CourseController.cs`, `Views/Course/Index.cshtml`,
`Controllers/LearningController.cs` (`MyCourses`), `ViewModels/MyCoursesViewModel.cs`, `Views/Learning/MyCourses.cshtml`,
`Controllers/HomeController.cs`, `Views/Home/Index.cshtml`.

---

## 20. Ảnh minh họa danh mục (Admin) và thẻ lĩnh vực có ảnh ở trang chủ (mới, **chưa commit**)

- **Admin > Quản lý danh mục** (`/Admin/Category`): form "Thêm / Chỉnh sửa danh mục" có ô **Ảnh minh họa**:
  - bấm hoặc kéo thả ảnh vào khung; khung xem trước có cùng tỉ lệ 16:10 với thẻ ở trang chủ, nên thấy trước ảnh sẽ bị cắt thế nào;
  - chỉ có nút "Chọn ảnh" (không có nút bỏ ảnh / ghi chú); sửa danh mục mà không chọn ảnh mới thì giữ ảnh cũ;
  - bảng danh mục có thêm cột ảnh thu nhỏ.
- **Kiểm tra phía server** (`Services/CategoryImageService.cs`), không tin kiểm tra của trình duyệt:
  - chỉ nhận JPG / PNG / WEBP, tối đa 2 MB, tối thiểu 200 × 120 px;
  - JPG / PNG được đọc lại thành ảnh thật (file giả đuôi `.jpg` bị chặn), thu nhỏ còn tối đa 1200 px chiều ngang rồi lưu lại;
    PNG trong suốt giữ PNG, còn lại lưu JPG chất lượng 85. WEBP chỉ kiểm tra chữ ký file (GDI+ của .NET Framework không đọc được WEBP);
  - tên file sinh mới `{maDanhMuc}-{8 ký tự ngẫu nhiên}.jpg` trong `assets/images/categories/`;
  - kiểm tra ảnh **trước** khi ghi CSDL: ảnh lỗi thì không tạo / sửa danh mục;
  - đổi ảnh hay xóa danh mục thì **xóa file ảnh cũ** (chỉ xóa file trong thư mục ảnh danh mục).
- **Trang chủ, mục "Khám phá theo lĩnh vực":** thẻ đổi thành ảnh ở trên (khung 16:10 cố định) + tên lĩnh vực ở dưới.
  Ảnh khác tỉ lệ được **cắt giữa cho vừa khung** (`object-fit: cover`), không méo. Danh mục chưa có ảnh hiện biểu tượng trên nền màu cùng khung,
  nên các thẻ luôn cao bằng nhau.
- **Trang danh mục `/danh-muc/{slug}`:** ảnh của danh mục phủ nửa phải banner tối ở đầu trang. Ảnh đặt `position: absolute`
  nên banner **giữ nguyên kích thước** (đo: 1425 × 290 trên máy tính, 375 × 314 ở 390 px, có ảnh hay không đều như nhau);
  ảnh cắt vừa khung (`object-fit: cover`), mép trái mờ dần vào nền (`mask-image`) nên không lộ đường nối.
  Điện thoại: ảnh làm nền cả banner, phủ tối để chữ đọc được. Danh mục chưa có ảnh thì banner như cũ.
- **CSDL:** migration `012_DanhMuc_AnhMinhHoa.sql` thêm cột `DanhMuc.anhMinhHoa`. Cột đọc / ghi bằng SQL (không sửa file `.edmx`).
  Chưa chạy migration thì form hiện lời nhắc chạy 012 và trang chủ hiện biểu tượng như cũ.

**Kiểm thử (Admin và học viên tạm, danh mục test `DM07` đã xóa qua nút Xóa):** ảnh 1800 × 900 lưu thành 1200 × 600;
file giả `.jpg`, ảnh 100 × 80, ảnh 14 MB đều bị chặn; đổi sang ảnh dọc 600 × 1200, PNG trong suốt, WEBP đều lưu được và file cũ bị xóa
(thư mục chỉ còn 1 file); ảnh dọc trên trang chủ hiện trong khung 290 × 181 (16:10), cắt giữa; xóa danh mục thì file ảnh bị xóa theo.

File: `Database/Migrations/012_DanhMuc_AnhMinhHoa.sql`, `Services/CategoryImageService.cs`, `Areas/Admin/Controllers/CategoryController.cs`,
`Areas/Admin/Models/` (`CategoryViewModel.AnhMinhHoa`), `Areas/Admin/Views/Category/Index.cshtml`, `Controllers/HomeController.cs`, `Views/Home/Index.cshtml`.

- **Trang Tất cả lĩnh vực `/danh-muc`:** thẻ có ảnh minh họa ở trên (khung 16:9, cắt vừa khung), bỏ biểu tượng.
  Danh mục chưa có ảnh hiện nền màu kèm chữ cái đầu tên lĩnh vực.
- **Migration 012 có thêm phần dữ liệu:** tạo `CAT-002`..`CAT-006` nếu máy chưa có (các danh mục này được tạo trên máy phát triển,
  không có trong seed), điền mô tả còn trống và gắn 10 ảnh danh mục trong `assets/images/categories/`. Chỉ điền chỗ trống, không ghi đè.

---

## 21. Dữ liệu demo: mỗi danh mục ít nhất 10 khóa học (migration 013) và thẻ "Khóa học của tôi" gọn hơn (mới, **chưa commit**)

- **`Database/Migrations/013_BoSung_KhoaHoc_DanhMuc.sql`:** 100 khóa soạn sẵn (10 khóa / danh mục), mã `K13-<danh mục>-<stt>`.
  - Chỉ thêm số khóa còn thiếu để danh mục đạt **10 khóa đang bán** (không tính khóa K13). Trên máy này đã thêm 89 khóa
    (DM01 +3, DM02 +9, DM03 +8, DM06 +9, DM04 và CAT-002..006 +10); máy có ít khóa hơn sẽ được thêm nhiều hơn.
  - Mỗi khóa: tên, mô tả ngắn, mô tả và mục tiêu đầu ra **viết riêng theo nội dung khóa**, cấp độ, giá (một số có giá khuyến mãi),
    ảnh bìa Unsplash đã kiểm tra tải được và đúng chủ đề (100 ảnh khác nhau), 1 chương + 1 bài **xem thử**, lịch sử duyệt "Đã duyệt".
  - Mục tiêu đầu ra không chứa dấu phẩy, vì trang chi tiết tách mục tiêu theo dấu `;` và `,`.
  - Video: dùng chung `vid_KH260929150333_20260930121911_38701957.mp4` (14 MB, 5:17, **có trong Git**) nên máy khác cũng phát được.
  - Giáo viên: khoa.tran (Lập trình, Y tế), ha.le (UI/UX, Thiết kế, Nhiếp ảnh, Ngoại ngữ), bao.pham (Dữ liệu, Kinh doanh, Marketing, Tài chính).
    Bài sức khỏe ghi rõ mang tính giáo dục, không thay thế tư vấn của bác sĩ.
  - Chạy lại được (lần hai không thêm gì); cuối file có đoạn lệnh gỡ dữ liệu K13 (đã comment).
- **Khóa học của tôi:** thẻ thu gọn, 4 thẻ / hàng (3 ở ≤ 1200 px, 2 ở ≤ 900 px, 1 ở ≤ 560 px); ảnh, chữ, nút nhỏ lại; tên khóa và dòng
  "Bài học tiếp theo" tối đa 2 dòng. Đo ở 1440 px: thẻ 295 × 412 (trước đây ~610 px chiều ngang, 2 thẻ / hàng).

**Kiểm thử (học viên tạm, đã xóa):** `/danh-muc` 10 thẻ đều có ảnh, mỗi lĩnh vực 10 khóa; `/danh-muc/y-te-suc-khoe` 10 thẻ, không ảnh lỗi;
trang chi tiết khóa K13 hiện đúng bài "Bài 1: …"; vào học khóa K13 phát được video (HTTP 206 video/mp4); thẻ "Khóa học của tôi" 4/3/2/1 thẻ
mỗi hàng theo bề rộng, nút "Làm Quizz ngay" không tràn.

---

## 22. Sửa lỗi lưu tiến độ học ở trang Study (mới, **chưa commit**)

**Nguyên nhân (3 lỗi chồng nhau):**
1. **Request video giữ khóa Session.** Video phát qua `Learning/StreamVideo`, cùng controller có Session. ASP.NET xử lý lần lượt các request
   dùng Session của cùng một người, mà request video kéo dài suốt lúc xem, nên mọi request `UpdateProgress` phải xếp hàng chờ và không được
   ghi (đo được: phát 17 giây rồi tạm dừng, CSDL không có dòng nào). Chuyển sang trang khác khi video đang phát cũng bị treo.
2. **Chỉ lưu một lần khi xem đủ 80%.** Thoát giữa chừng thì không có gì được lưu, mở lại bài video chạy từ đầu.
3. **`BaiHoc.thoiLuong` lệch xa video thật** (vd. `DMKH01_1` ghi 420 giây, video dài 1828 giây). Trigger `trg_TienDoBaiHoc_CapNhatHoanThanh`
   tự đánh dấu hoàn thành khi `thoiGianXemDungLai >= 80% thoiLuong`, nên nếu lưu vị trí định kỳ thì bài bị tính xong khi mới xem ~18%.
   Ngoài ra trigger `trg_TienDoBaiHoc_CapNhatTienDoKhoaHoc` chỉ tính bài trong chương, nên khóa có bài không phân chương luôn 0% ở trang giáo viên.

**Cách sửa:**
- `Controllers/VideoStreamController.cs` (mới): phát video tại `/VideoStream/Play?file=...` với `[SessionState(Disabled)]`; xóa
  `LearningController.StreamVideo`; `Study.cshtml` và `Course/Details.cshtml` dùng đường dẫn mới.
- `Views/Learning/Study.cshtml`: lưu vị trí **mỗi 15 giây khi đang phát, khi tạm dừng, khi chuyển bài và khi rời trang / ẩn tab**
  (`navigator.sendBeacon`, dự phòng `fetch keepalive`, để request không bị hủy khi đóng trang). Mở lại bài thì **tiếp tục từ vị trí đã lưu**
  (gần hết bài thì xem lại từ đầu). Bài đã hoàn thành được tua tự do; bài chưa xong vẫn chỉ tua tới đoạn đã xem.
- `Services/VideoDuration.cs` (mới): đọc độ dài thật của file MP4 / MOV / M4V từ hộp `mvhd` (chỉ đọc vài KB, có cache).
- `LearningController`:
  - `Study` đồng bộ `BaiHoc.thoiLuong` của khóa theo độ dài video thật và trả vị trí xem dở (`ViewBag.ResumeSeconds`);
  - `UpdateProgress` dùng độ dài thật (không tin số trình duyệt gửi), đồng bộ `thoiLuong` **trước** khi ghi tiến độ để trigger không đánh dấu sai,
    giới hạn vị trí không vượt độ dài video, không bao giờ bỏ trạng thái đã hoàn thành;
  - sau mỗi lần lưu tính lại `TienDoHocTap.phanTramHoanThanh` (cả bài trong chương lẫn không phân chương) và `lanTruyCapGanNhat`.

**Kiểm thử (học viên tạm, đã xóa):** phát 17 giây → lưu định kỳ vị trí 31; tạm dừng → 36; xem tiếp rồi rời trang → 48, chuyển trang không còn
treo; mở lại → video tiếp tục từ 48; gửi vị trí 600 với độ dài giả 420 → **chưa** hoàn thành (độ dài thật 1828); vị trí 1500 → hoàn thành,
% khóa `DMKH01` = 16,67; khóa `KH260929150333` (bài không phân chương) 1/8 bài → 12,5%.
`thoiLuong` của các bài thuộc `DMKH01` và `KH260929150333` đã được đồng bộ theo video thật; các khóa khác tự đồng bộ khi có người mở trang học.

---

## 23. Thanh toán và chi trả doanh thu ở chế độ thử nghiệm (mới, **chưa commit**)

Chưa có pháp nhân nên chưa ký được với cổng thật; hệ thống dùng **cổng thử nghiệm** nhưng đi đúng luồng của cổng thật (VNPay / MoMo).

**Học viên mua khóa học** (`Services/PaymentService.cs`, `Controllers/PaymentController.cs`, `Controllers/CartController.Checkout`):
1. Giỏ hàng tạo đơn **"Chờ thanh toán"**, hạn 15 phút, lưu phương thức chọn ở giỏ. **Chưa ghi danh.** Mỗi học viên chỉ một đơn chờ
   (tạo đơn mới thì đơn chờ cũ chuyển "Đã hủy"); đơn quá hạn chuyển "Hết hạn" khi mở cổng / giỏ hàng / lịch sử.
2. Chuyển sang **`/Payment/Gateway`** (trang cổng thử nghiệm, tách khỏi layout chính): số tiền, các khóa, đồng hồ hết hạn, giao diện theo phương thức
   (QR minh họa / thẻ ATM thử nghiệm / thẻ quốc tế thử nghiệm) và 3 nút: **Xác nhận thanh toán**, **Mô phỏng thất bại** (mã 51), **Hủy giao dịch** (mã 24).
3. Cổng tạo kết quả có **chữ ký HMAC-SHA512** (cách ký giống VNPay) và gửi **IPN**: `PaymentService.XuLyKetQua` kiểm tra chữ ký, đơn tồn tại,
   số tiền khớp, trạng thái đơn, khóa theo đơn (`sp_getapplock`) để IPN gọi lặp chỉ ghi nhận một lần. Thành công thì ghi dòng `ThanhToan` "Đã thanh toán"
   (trigger có sẵn `trg_ThanhToan_TuDongDangKy` **ghi danh** các khóa trong đơn) và đơn chuyển "Đã thanh toán"; thất bại / hủy ghi `ThanhToan` "Thanh toán thất bại".
   Mã phản hồi cho cổng: 00, 01 không thấy đơn, 02 đã xử lý / đơn không còn chờ, 04 sai số tiền, 97 sai chữ ký.
4. Trình duyệt quay về `/Payment/Return` (chỉ kiểm tra chữ ký, ghi nhật ký) → `/Payment/Result` hiển thị theo CSDL; thanh toán xong mới bỏ khóa khỏi giỏ.
5. `POST /Payment/Ipn` là endpoint công khai cho cổng gọi server-to-server (cổng thật sẽ dùng); cổng thử nghiệm chạy cùng ứng dụng nên gọi thẳng cùng hàm xử lý.
6. **Đơn 0đ** (mã giảm giá trừ hết) không qua cổng, ghi danh ngay. **Lịch sử mua hàng** `/Payment/History` (menu "Khóa học của tôi").
7. Mọi bước ghi `NhatKyThanhToan` (TAO_DON, IPN, RETURN, HET_HAN, HUY) để tra soát. Báo cáo doanh thu, giới hạn lượt coupon và doanh thu giáo viên
   chỉ tính đơn "Đã thanh toán" nên đơn chờ / thất bại / hết hạn tự động không được tính.

**Giáo viên nhận doanh thu** (`Areas/Instructor/Controllers/PayoutController.cs`, `Areas/Admin/Controllers/PayoutRequestController.cs`):
- Số dư = **80% doanh thu thực thu** (sau khi trừ mã giảm giá, phân bổ theo `RevenueService.TyLeThucThuTheoDon`) − các yêu cầu chưa bị từ chối.
  Trước đây tính trên giá trước coupon và phiếu chi còn trừ thêm `tiLeChietKhau = 0.2` (thực nhận 99,8%); nay phiếu chi không trừ thêm.
- Giáo viên gửi yêu cầu rút (tối thiểu 500.000đ) → Admin vào **"Chi trả giáo viên"** (menu Admin mới): **Duyệt chi** = chuyển khoản thử nghiệm
  (sinh mã `SBXCHI...`, ghi người duyệt, thời điểm) hoặc **Từ chối** kèm lý do (5–500 ký tự, số tiền trở lại số dư). Chỉ xử lý phiếu còn "Chờ duyệt".
- Trang Nhận thanh toán của giáo viên hiện doanh thu thực thu, phần 80%, mã giao dịch chi / lý do từ chối của từng phiếu.

**CSDL:** `Database/Migrations/014_ThanhToan_ThuNghiem.sql` (index duy nhất `(congThanhToan, maGiaoDichCong)` là index lọc nên lệnh sửa bảng `ThanhToan`
bằng `sqlcmd` cần `-I`; ứng dụng không bị ảnh hưởng). Khóa ký mặc định dùng cho thử nghiệm; có thể đặt `appSettings["PaymentSandbox:HashSecret"]`.

**Kiểm thử (học viên / Admin tạm, dữ liệu đã xóa):** 2 khóa 1.139.000đ: "Mô phỏng thất bại" → đơn "Thanh toán thất bại", không ghi danh, giỏ còn 2 khóa;
thanh toán lại thành công → đơn "Đã thanh toán", ghi danh 2 khóa, giỏ trống, `ThanhToan` có mã giao dịch `SBX...`; IPN giả chữ ký → 97; IPN hợp lệ gọi lặp → 02,
không ghi danh thêm; chữ ký đúng nhưng sai số tiền → 04; đơn quá hạn → mở cổng chuyển sang "Đơn hàng đã hết hạn", IPN tới muộn → 02, không ghi danh;
"Hủy giao dịch" → thất bại. Giáo viên `khoa.tran`: số dư 4.726.400 → gửi 2 × 500.000 → 3.726.400 → Admin duyệt 1, từ chối 1 → 4.226.400, đã chi +500.000.

### 23.1. Tích hợp VNPay Sandbox (cổng thật của VNPay, môi trường test)

- **Cấu hình** trong `Secrets.config` (đã gitignore, `Web.config` đọc qua `<appSettings file="Secrets.config">`; mẫu: `Secrets.config.example`):
  `Payment:Mode = VNPay`, `VNPay:TmnCode`, `VNPay:HashSecret`, `VNPay:PaymentUrl`, `VNPay:ApiUrl`. Máy không có file này (hoặc Mode khác VNPay)
  tự dùng cổng thử nghiệm nội bộ ở trên, nên demo vẫn chạy khi mất mạng: đổi `Payment:Mode` thành `Sandbox`.
- **`Services/VnPayService.cs`** (API 2.1.0):
  - `TaoUrlThanhToan`: số tiền ×100, `vnp_CreateDate` / `vnp_ExpireDate` giờ GMT+7 (hạn = hạn đơn 15 phút), `vnp_TxnRef = mã đơn_HHmmss`
    (mỗi lần mở cổng là giao dịch mới vì VNPay không nhận trùng TxnRef trong ngày), `vnp_BankCode` theo phương thức chọn ở giỏ
    (`VNBANK` thẻ nội địa, `INTCARD` thẻ quốc tế), ký HMAC-SHA512 trên chuỗi tham số sắp theo tên, mã hóa URL như mã mẫu .NET của VNPay.
  - `XuLyKetQua`: kiểm chữ ký + `vnp_TmnCode`, thành công khi `vnp_ResponseCode = 00` và `vnp_TransactionStatus = 00`, rồi gọi
    `PaymentService.GhiNhanKetQua` (lõi chung với cổng thử nghiệm: kiểm tra đơn, số tiền, trạng thái, khóa theo đơn, ghi `ThanhToan`).
  - `TruyVan` (API **querydr**): trên máy phát triển VNPay không gọi được IPN vào `localhost`, nên trang quay về **hỏi lại VNPay** trước khi
    ghi nhận thành công (kiểm cả chữ ký phản hồi). `vnp_TransactionDate` lấy từ nhật ký `TAO_URL`. VNPay chặn (403) request không có
    User-Agent nên request đặt `UserAgent` và tắt `Expect: 100-continue`. Không gọi được API thì dùng dữ liệu quay về (đã đúng chữ ký VNPay).
- **`PaymentController`**: `Gateway` chuyển thẳng sang VNPay khi bật; `GET /Payment/VnpayReturn`; `GET /Payment/VnpayIpn` (trả `RspCode`
  00/01/02/04/97/99 theo quy ước VNPay; dùng khi chạy trên máy chủ có HTTPS công khai, cấu hình IPN URL ở trang quản trị merchant).
- **Kiểm thử thật trên VNPay Sandbox (học viên tạm, đã xóa; giao dịch test còn lưu ở phía VNPay):** thẻ NCB `9704198526191432198` + OTP `123456`
  → quay về "Thanh toán thành công", mã giao dịch VNPay `15709177`, querydr `ResponseCode=00, TransactionStatus=00, chữ ký hợp lệ`, ghi danh;
  thẻ NCB không đủ số dư `9704195798459170488` → VNPay báo lỗi, bấm "Hủy thanh toán" → mã 24 → đơn "Thanh toán thất bại", giỏ còn khóa;
  IPN giả chữ ký → 97; IPN hợp lệ gửi lặp cho đơn đã thanh toán → 02, không ghi danh thêm.
- Thẻ test: [trang demo VNPay](https://sandbox.vnpayment.vn/apis/vnpay-demo/). Sandbox không hỗ trợ thanh toán QR; khi demo chọn "Thẻ ATM nội địa".
- **Chặn phương thức chưa dùng được**: trên Sandbox chỉ **thẻ ATM nội địa** hoàn tất được. Ví / VNPAY-QR / App VNPAY: app thật không thanh toán
  mã QR test; thẻ quốc tế: tài khoản sandbox chưa được VNPay mở kênh thẻ quốc tế. Khi bật VNPay, giỏ hàng khóa "Ví điện tử MoMo / VNPay" và
  "Thẻ Quốc Tế Visa / Mastercard / JCB" (nhãn "Chưa hỗ trợ", bấm vào hiện hộp thoại nêu lý do riêng từng phương thức, nút chuyển sang thẻ ATM nội địa),
  mặc định chọn thẻ ATM nội địa; `CartController.Checkout` từ chối mọi phương thức khác `Napas` (`VnPayService.HoTroPhuongThuc`);
  `TaoUrlThanhToan` luôn gửi `vnp_BankCode=VNBANK` nên VNPay mở thẳng danh sách ngân hàng. Ở cổng thử nghiệm nội bộ (không bật VNPay) cả 3 phương thức vẫn dùng được.

---

## 24. Chấm đỏ nhắc việc ở menu Admin (mới, **chưa commit**)

Mục ở menu Admin có yêu cầu từ người dùng đang chờ xử lý thì hiện chấm đỏ nhỏ bên phải (rê chuột xem số lượng), để Admin không bỏ sót:

| Mục menu | Đếm |
| --- | --- |
| Kiểm duyệt khóa học | Khóa học giáo viên gửi duyệt (`Chờ duyệt` / `Chờ kiểm duyệt`) |
| Duyệt hồ sơ Giáo viên | Hồ sơ đăng ký giáo viên `Pending` (`DangKyGiaoVien`) |
| Báo cáo vi phạm | Báo cáo `Chờ xử lý` + khóa học tạm khóa mà giáo viên đã phản hồi (`Chờ Admin xem xét`) |
| Chi trả giáo viên | Phiếu rút tiền `Chờ duyệt` |

Dashboard, Quản lý danh mục, Báo cáo hệ thống, Doanh thu theo ngày không nhận yêu cầu từ người dùng nên không có chấm.

- `Services/AdminPendingService.cs`: đếm từng mục riêng (một bảng chưa có thì mục đó là 0, không làm hỏng trang).
- `AdminBaseController.OnResultExecuting`: mọi trang Admin trả về View đều có `ViewBag.AdminPending`; `_LayoutAdmin` hiện chấm (viền trắng để vẫn thấy trên mục đang chọn màu xanh, có hiệu ứng nhấp nháy nhẹ, tắt khi người dùng chọn giảm chuyển động).
  Chấm đặt tuyệt đối trong phần đệm bên phải của mục (không chiếm chỗ của chữ): mục giữ nguyên chiều cao 48px, chữ dài không bị xuống dòng, menu không hiện thanh cuộn ngang.
- `GET /Admin/Dashboard/PendingCounts` (JSON): menu tự làm mới chấm mỗi 60 giây, khi quay lại tab và sau khi Admin gửi thao tác AJAX, nên yêu cầu mới đến trong lúc đang mở trang vẫn hiện chấm.
- Kiểm thử: chấm hiện đúng với dữ liệu hiện có (2 hồ sơ giáo viên, 3 yêu cầu vi phạm); thêm tạm một phiếu rút tiền thì chấm "Chi trả giáo viên" tự hiện không cần tải lại trang, xóa phiếu thì chấm tắt (đã xóa dữ liệu test).

---

## 25. API cho ứng dụng di động `/api/v1` (mới, **chưa commit**)

Tài liệu đầy đủ (danh sách API, mẫu JSON, quy tắc, phân công): `D:\Nam4_DH\KLTN\Tuan10\Task_Tuan10.docx`;
bộ Postman: `Tuan10\ECOLearning_API.postman_collection.json`; app Flutter: repo `ECOLearning_Application`.

- **Cách làm**: controller MVC 5 trả JSON (không cài thêm gói Web API), attribute routing `routes.MapMvcAttributeRoutes()`;
  đường dẫn `/api/...` không khớp API nào trả 404 dạng JSON (`ApiNotFoundController`). `Web.config` mở thêm PUT / DELETE cho handler
  mặc định của IIS. Mọi phản hồi: `{ success, message, data }` / `{ success: false, code, message, errors }`, JSON camelCase,
  thời gian ISO 8601 `+07:00`, tiền là số nguyên VND, ảnh / video là đường dẫn đầy đủ.
- **Xác thực**: token `Authorization: Bearer` (bảng `ApiToken`, migration 015, chỉ lưu SHA-256, hạn 30 ngày; đăng xuất thu hồi token,
  đổi / đặt lại mật khẩu thu hồi token ở thiết bị khác). OTP đăng ký / quên mật khẩu lưu bảng `ApiOtp` (app không có Session),
  cùng quy tắc với website: 6 số, hết hạn 5 phút, sai 5 lần thì vô hiệu, gửi lại sau 60 giây.
- **File**: `Controllers/Api/` (ApiControllerBase, ApiRules, Ping, Auth, Profile, Catalog, Recommendation, Order + PaymentReturn,
  Learning, Quiz), `Services/Api/` (ApiTokenService, ApiOtpService, ApiCourseMapper), `Services/LearningService.cs`,
  `Services/OrderService.cs`, `Tools/chay_tunnel_api.bat`.
- **Dùng chung nghiệp vụ với website** (không viết lại): gợi ý lọc cộng tác (`RecommendationService.GetForHome`), tìm kiếm không dấu
  (`SearchService`), mã giảm giá (`PromotionService`), VNPay (`VnPayService`), quiz và chứng chỉ (`QuizService`), yêu thích, sở thích.
  - `LearningService`: phần lưu tiến độ / đồng bộ thời lượng video / tính % khóa chuyển từ `LearningController` sang; web và app gọi
    chung nên học ở đâu cũng ra cùng tiến độ (đã kiểm tra lại trang Study của web: lưu dở 200, dữ liệu sai 400, xem hết tính hoàn thành
    theo độ dài thật của file video).
  - `OrderService`: tạo đơn cho app với cùng các bước của `CartController.Checkout` (giỏ hàng của app nằm trên máy).
  - `RecommendationService`: thêm `GhiLuotXem` (không cần Session) và `GhiLuotClick` (cột `luotClick` trước đây không có chỗ nào ghi).
- **Thanh toán trên app**: F3 trả `paymentUrl` của VNPay với `vnp_ReturnUrl = /api/v1/payments/vnpay-return`; trang này ghi nhận kết quả
  (cùng hàm với `/Payment/VnpayReturn`) và **không chuyển tiếp** nên WebView của app luôn bắt được địa chỉ để đóng lại rồi gọi F4.
- **Địa chỉ public cho app** (máy ảo / điện thoại không gọi được `localhost`): `Tools/chay_tunnel_api.bat` (Cloudflare Tunnel).
  Link ảnh / video / VNPay lấy theo `Api:PublicBaseUrl` (Secrets.config) → header `X-Forwarded-Host` → địa chỉ request;
  `GET /api/v1/ping` trả `publicBaseUrl` để kiểm tra.
- **Kiểm thử** (gọi thật vào API, tài khoản `test.*@demo.ecolearning.vn`, đã xóa sạch sau khi chạy): 101/101 trường hợp đạt, gồm lỗi
  401 / 403 / 404 / 409 / 429, đăng ký OTP 3 bước, đổi / đặt lại mật khẩu thu hồi token, tìm kiếm không dấu, ghi lượt xem / click, gợi ý
  "Vì bạn đã xem…", và trọn luồng: tạo đơn → trả bằng thẻ NCB trên VNPay Sandbox → đơn `paid` → học 8 bài (video tua được, HTTP 206)
  → quiz 10/10 → nhận chứng chỉ → đánh giá → thống kê hồ sơ.
- **Chưa làm**: link video có hạn dùng (video nội bộ vẫn mở công khai theo tên file như website). A12 đăng nhập Google: đã làm, xem mục 37.

---

## 26. Admin: Quản lý người dùng, Quản lý giáo viên, Giám sát Marketing (mới, **chưa commit**)

Chạy `Database/Migrations/016_QuanTri_NguoiDung.sql` (trên hosting: xóa dòng `USE`, chọn CSDL hosting). Chưa chạy thì 3 trang mới
báo "Chưa khởi tạo dữ liệu", phần còn lại của web vẫn chạy. Menu Admin thêm 3 mục ngay dưới Dashboard.

### 26.1. Quản lý người dùng (`/Admin/User`)
- **Danh sách**: 5 ô thống kê (bấm để lọc), tìm theo họ tên / email / SĐT / mã, lọc vai trò và trạng thái, 20 dòng mỗi trang.
- **Chi tiết** (học viên, nhân viên; giáo viên chuyển sang 26.2): thông tin, khóa học đã đăng ký + % tiến độ, đơn hàng, đánh giá,
  số chứng chỉ, nhật ký quản trị.
- **Khóa tài khoản** 7 ngày / 30 ngày / vĩnh viễn, bắt buộc lý do (gửi email cho người bị khóa). Người đang đăng nhập bị **đăng xuất
  ngay** ở lần bấm kế tiếp (`App_Start/AccountStatusFilter.cs`, bộ lọc toàn cục; trước đây phiên cũ vẫn dùng được đến khi hết hạn),
  token app bị thu hồi. Đăng nhập lại (web, Google, API) báo lý do và hạn khóa (chỉ khi nhập đúng mật khẩu, tránh lộ thông tin).
  Hết hạn thì **tự mở** khi người đó đăng nhập hoặc khi Admin mở danh sách (nhật ký ghi "Hệ thống").
- **Quy tắc**: không xóa tài khoản (còn đơn hàng, doanh thu, chứng chỉ); không tự khóa mình; không khóa Admin cuối cùng.
- **Đặt lại mật khẩu**: hệ thống sinh mật khẩu tạm 12 ký tự và **chỉ gửi qua email** (Admin không thấy), mật khẩu cũ hết hiệu lực,
  người dùng **bắt buộc đổi mật khẩu** ở lần đăng nhập kế tiếp (mọi trang chuyển về Hồ sơ > Đổi mật khẩu). Gửi email lỗi thì hoàn tác.
- **Tạo tài khoản nhân viên** Marketing / Quản trị viên (trước đây chỉ tạo được bằng SQL): cùng cơ chế mật khẩu tạm.
- **Cảnh cáo**: gửi email + ghi nhật ký, không khóa.
- Mọi thao tác ghi bảng `NhatKyQuanTri`. File: `Services/AccountAdminService.cs`, `Services/AdminUserQueryService.cs`,
  `Areas/Admin/Controllers/UserController.cs`, `Areas/Admin/Views/User/*`, partial `_AccountActions`, `_NhatKyQuanTri`, `_AdminFlash`.

### 26.2. Quản lý giáo viên (`/Admin/Teacher`)
- Tách khỏi "Duyệt hồ sơ Giáo viên" (đơn đăng ký). Danh sách: số khóa đang bán / chờ duyệt, học viên, điểm đánh giá, **doanh thu thực thu**
  (cùng cách tính với trang Rút tiền của giáo viên), tiền chờ chi, số vi phạm chờ xử lý. Mặc định xếp người cần chú ý lên đầu.
- **Hồ sơ**: tab Khóa học, Hồ sơ đăng ký (chuyên môn, ngân hàng, chứng chỉ), Chi trả, Đánh giá, Vi phạm (link sang trang xử lý), Nhật ký.
  Trang xử lý vi phạm có link ngược "Hồ sơ giáo viên".
- **Khóa giáo viên**: khóa đang bán chuyển trạng thái **"Tạm ẩn"** (`CourseStatus.TamAn`, trạng thái cũ lưu ở `KhoaHocTamAn`) nên mọi
  trang bán hàng / tìm kiếm / gợi ý tự loại ra mà không phải sửa từng truy vấn; **học viên đã mua vẫn học** (trang học chỉ chặn
  "Khóa vĩnh viễn"). Phiếu chi bị **tạm giữ** (Chi trả giáo viên hiện nhãn "Tạm giữ", nút Duyệt chi bị chặn ở server); Kiểm duyệt khóa học
  không xuất bản được khóa mới của giáo viên đang bị khóa. Mở khóa thì khôi phục trạng thái cũ (chỉ khóa còn "Tạm ẩn").
- File: `Services/AdminTeacherService.cs`, `Areas/Admin/Controllers/TeacherController.cs`, `Areas/Admin/Views/Teacher/*`;
  sửa `PayoutRequestController`, `CourseApprovalController.Approve`, `InstructorBaseController` (bị khóa thì đăng xuất hẳn).

### 26.3. Giám sát Marketing (`/Admin/Marketing`)
- Admin **không tạo khuyến mãi thay Marketing**, chỉ giám sát: chương trình đang chạy, hết hạn trong 7 ngày, tiền đã giảm so với doanh thu
  sau giảm, chiến dịch đang mở, bảng nhân viên Marketing (số chương trình / chiến dịch đã tạo, số bị tạm dừng).
- **Cảnh báo tự động**: giảm từ 50% (ghi rõ nếu không giới hạn số tiền), tiền đã giảm vượt doanh thu sau giảm, giảm 100%.
- **Tạm dừng** kèm lý do: mã ngừng áp dụng ngay, nhân viên tạo chương trình nhận email; phía Marketing hiện khung đỏ có lý do, nút
  "Kích hoạt lại" bị vô hiệu và **server cũng chặn** (`PromotionService.DoiTrangThai`). Chỉ Admin "Cho chạy lại" (nếu chưa hết hạn).
- **Admin không vào khu Marketing** (trước đây `MarketingBaseController` cho Admin toàn quyền "để kiểm thử": tạo / sửa khuyến mãi,
  đặt giá bán, chiến dịch). Nay chỉ nhân viên Marketing vào được; Admin mở `/Marketing/...` được chuyển về Giám sát Marketing kèm thông báo,
  yêu cầu ghi gửi thẳng cũng bị chặn. Bù lại, trang chi tiết bên Admin có thêm khóa học áp dụng và lịch sử dùng mã. Kiểm thử 26/26.
- File: `Services/AdminMarketingService.cs`, `Areas/Admin/Controllers/MarketingController.cs`, `Areas/Admin/Views/Marketing/*`,
  partial `_PromotionAdminActions`, `_PromotionAdminForms`; sửa `Areas/Marketing/Controllers/PromotionController.cs`, view Promotion Index / Details.

### 26.4. Chế độ chỉ xem của Admin / Marketing ở trang người học
- Mục tiêu (phân tách vai trò): tài khoản vận hành thấy **đúng giao diện học viên** để kiểm tra nội dung, giá, mã giảm giá, nhưng
  **không tạo dữ liệu** làm sai doanh thu, thống kê khuyến mãi, lượt yêu thích và dữ liệu gợi ý. Giáo viên không thuộc chế độ này.
- Giao diện: dải vàng dưới header "Bạn đang xem với vai trò … chế độ chỉ xem" + nút về trang quản trị; mọi nút tương tác (Mua ngay,
  Thêm vào giỏ, Học miễn phí, combo mua cùng, thả tim ở mọi nơi, Lưu mã, Hữu ích / Không hữu ích, đánh giá, báo cáo vi phạm, giỏ hàng
  và "Khóa học của tôi" trên header) **giữ nguyên vị trí nhưng xám**, con trỏ "không cho phép", rê chuột hiện chú thích, bấm hiện thông báo.
  `assets/js/staff-readonly.js` đánh dấu theo danh sách bộ chọn (cả nút tải thêm bằng AJAX) và chặn bấm ở pha capture nên xử lý gốc không chạy.
- Server: `[StaffReadOnly]` (App_Start/StaffReadOnlyAttribute.cs) trên 21 action (giỏ hàng, thanh toán, mã giảm giá, ghi danh miễn phí,
  yêu thích, sở thích, đánh giá, hữu ích, báo cáo vi phạm, tiến độ, quiz) trả `{ success: false, staffReadOnly: true }`; trang cá nhân
  (giỏ hàng, ví mã, khóa học của tôi, yêu thích, lịch sử thanh toán) chuyển về Trang chủ. Tìm kiếm của Admin / Marketing không ghi thống kê từ khóa.
- Kiểm thử: 85/85 (Admin, Marketing gọi thẳng 16 action đều bị chặn, dữ liệu trong mọi bảng liên quan không đổi; học viên, giáo viên vẫn
  thêm giỏ / thả tim / tìm kiếm bình thường); kiểm tra JS trong Edge: nút bị khóa không chạy xử lý gốc, kể cả nút thêm sau khi tải trang.

### 26.5. Kiểm thử
Gọi thật vào web (IIS Express), tài khoản / khóa học / đơn hàng / khuyến mãi tạm, **đã xóa sạch** sau khi chạy; email test gửi về chính
hộp thư hệ thống. Mục 26.1: 51/51 đạt; 26.2 + 26.3: 53/53 đạt (gồm gửi thẳng yêu cầu bỏ qua giao diện: thiếu AntiForgeryToken, Marketing
gọi "resume" khi Admin đã tạm dừng, duyệt chi phiếu tạm giữ, Marketing vào trang Admin); hồi quy 47 trang cũ của khách / học viên /
giáo viên / Admin / Marketing / API đều 200.

---

## 27. Marketing theo đặc tả nghiệp vụ đã chốt (mới, **chưa commit**)

Theo "Đặc tả luồng Marketing và API mã giảm giá" (Minh Thư). Giữ **tên cột của 008 / 009** (không đổi sang cấu trúc 007 của đặc tả), chỉ bổ sung phần còn
thiếu bằng `Database/Migrations/018_Marketing_DacTa.sql`. Ánh xạ tên: `loaiGiam` = `loaiUuDai`, `donToiThieu` = `donHangToiThieu`,
`soLuotToiDa` = `MaGiamGia.gioiHanLuotDung`, `soLuotMoiNguoi` = `MaGiamGia.gioiHanMoiHocVien`, `laUuDaiThanThiet` = `doiTuong = ThanThiet`,
`tongChiToiThieu` = `chiTieuToiThieu`, `phamVi ToanSan` = `TatCa`, bảng `ChienDichMarketing` = `ChienDich`, `tongTienGoc / tienGiam` của đơn = `SuDungUuDai`.

- **Bỏ giá khuyến mãi theo khóa** (mục 1, 12): xóa trang "Giá khuyến mãi khóa học" (`CoursePriceController`). Giá ở mọi nơi (trang khóa học, thẻ khóa,
  tìm kiếm, sắp xếp / lọc giá, giỏ hàng, đơn hàng, API, khu giáo viên, Admin) = `giaBan`; không còn chỗ nào đọc `giaKhuyenMai` (cột giữ nguyên trong CSDL).
- **Áp mã** (mục 9): `PromotionService.KiemTraCoupon` là hàm `Check()` duy nhất cho web và API, đúng 13 bước và câu thông báo của đặc tả (hết hạn → tạm ngưng
  → chưa bắt đầu → chiến dịch tạm dừng / hủy → hết lượt → cần đăng nhập (`requireLogin`) → hết lượt của mình → thân thiết → giỏ trống → phạm vi → đơn tối thiểu).
  Giảm % làm tròn, có trần; thêm phạm vi **danh mục**; số lượt mỗi người để trống = không giới hạn.
- **Ví mã** (mục 8): lưu được cả mã "Chưa bắt đầu"; mã riêng chỉ lưu qua ô **"Nhập mã"** trong ví (`Coupon/Redeem`) hoặc API; mã nhập tay ở giỏ vào ví
  **khi đơn đã thanh toán** (trước đây vào ví lúc tạo đơn). Tab Có thể dùng (đang chạy trước, hạn gần trước, nhãn "Sắp hết hạn") / Đã dùng (kèm lịch sử đơn) /
  Hết hiệu lực (kèm lý do) / Thu thập mã; nhãn Mã công khai / Mã riêng; khối **"Ưu đãi thân thiết sắp mở"** ở ví và giỏ hàng. Giỏ hàng gợi ý mã giảm nhiều nhất, không tự áp.
- **Thân thiết** (mục 6): số khóa = số khóa khác nhau đã mua trong đơn đã thanh toán (không tính khóa miễn phí đăng ký trực tiếp). Menu "Ưu đãi thân thiết"
  giữ lại như bộ lọc của danh sách khuyến mãi, dùng chung form.
- **Chiến dịch** (mục 4): kênh, ngân sách, chi phí thực tế, chỉ tiêu doanh thu / lượt dùng, ghi chú; 5 trạng thái với bước chuyển hợp lệ; % đạt chỉ tiêu, ROI;
  sửa thời gian mà có khuyến mãi nằm ngoài khoảng mới thì báo lỗi; khuyến mãi phải nằm trong thời gian chiến dịch; xóa chỉ khi chưa gắn khuyến mãi.
- **Khuyến mãi** (mục 5, 7): giảm số tiền ≤ đơn tối thiểu, lượt mỗi người ≤ tổng lượt; trạng thái hiển thị thêm "Hết lượt"; **kết thúc sớm** = ngày kết thúc hôm qua
  + tắt mã; **xóa** khi chưa có đơn hàng nào dùng mã; trang chi tiết có số học viên đã lưu mã.
- **Báo cáo, Dashboard** (mục 11): mặc định 30 ngày, nút 7 ngày / 30 ngày / Tháng này / Năm nay; KPI lượt dùng (kèm số học viên, lượt thân thiết), doanh thu,
  tiền giảm, giá trị đơn trung bình, tỷ lệ đơn có mã; biểu đồ lượt dùng + doanh thu theo ngày / tháng / năm; bảng theo chiến dịch (ROI), theo khuyến mãi,
  chi tiết từng lượt; CSV theo bộ lọc. Dashboard: khuyến mãi đang chạy, chiến dịch đang triển khai (5 sắp kết thúc), số liệu tháng này, 12 tháng, top 5 mã,
  5 mã sắp hết hạn / sắp hết lượt. Đơn đã thanh toán = `Đã thanh toán`, `Hoàn tất`, `Success`.
- **API cho app** (mục 13, `Controllers/Api/CouponApiController.cs`): `POST coupons/check`, `GET coupons/collectable`, `POST coupons/{code}/save`,
  `GET coupons/wallet`, `GET coupons/my` (viết lại, có `bestCode`), `GET coupons/{code}`; `cart/quote` trả khối `coupon` giống `coupons/check`;
  `orders` trả `COUPON_INVALID`, đã sở hữu khóa đổi sang `COURSE_OWNED`. Mã lỗi `COUPON_NOT_SAVABLE`, `COUPON_NOT_FOUND`, `CART_EMPTY`. Bật **CORS** cho `/api/...`.
- **Không theo đặc tả (giữ quyết định mục 26.3)**: Admin không vào khu Marketing ở chế độ chỉ xem; Admin giám sát và tạm dừng ở `/Admin/Marketing`.
- **Kiểm thử**: gọi thật vào web và API (IIS Express): 96/96 đạt (13 bước kiểm tra mã, lưu mã, ví, chọn mã ở giỏ, `cart/quote`, `orders`, CORS, các trang học viên /
  Marketing / Admin / giáo viên, form chiến dịch và khuyến mãi, kết thúc sớm, xóa); đơn 0đ dùng mã nhập tay → mã vào ví sau khi thanh toán. Dữ liệu test đã xóa.

## 28. Lưu video bài học trên Cloudflare R2, nén bằng ffmpeg (mới, **chưa commit**)

- **Cấu hình**: khóa `R2:AccountId`, `R2:BucketName`, `R2:AccessKeyId`, `R2:SecretAccessKey`, `R2:PublicUrl` trong `Secrets.config` (mẫu ở `Secrets.config.example`,
  không đưa lên Git). Chưa điền thì video vẫn lưu trong `assets/uploads/videos` như trước. Thư viện `AWSSDK.S3` v4 (R2 cần `ForcePathStyle`, vùng `auto`,
  tắt checksum mặc định của SDK).
- **Luồng tải lên** (`Services/VideoStorageService.cs`, `Services/R2Storage.cs`): giáo viên tải video (trình tạo khóa học, thư viện tài nguyên) → file nằm tạm trong
  `App_Data/video-tmp` → hàng đợi nền (mỗi lần một video) nén bằng `App_Data/ffmpeg.exe` (720p, H.264 CRF 28, AAC 96k, `faststart`) → đẩy lên R2 → xóa file tạm.
  ffmpeg không chạy được, nén lỗi, quá 45 phút hoặc bản nén không nhỏ hơn thì đẩy **bản gốc**. `BaiHoc.duongDanVideo` lưu `r2:videos/{id}.mp4` ngay khi tải lên;
  độ dài video lưu trong metadata `duration` của object. Mỗi việc có file `.job.json`, ứng dụng khởi động lại thì chạy tiếp (nén lỗi 2 lần thì đẩy bản gốc).
- **Phát video**: `/VideoStream/Play?src=r2:...` phát từ file tạm khi còn chờ, đã lên R2 thì chuyển hướng sang link ký có hạn 6 giờ (bucket để riêng tư).
  Trang khóa học, trang học, API app, trang duyệt khóa của Admin, khu giáo viên đều đi qua `VideoStorageService.LinkPhat`. Kiểm tra gửi duyệt / phê duyệt
  (`CourseContentService.VideoHopLe`) và tiến độ học (`VideoDuration`) hiểu đường dẫn `r2:`.
- **Admin > Lưu trữ video** (`Areas/Admin/Controllers/VideoStorageController.cs`): trạng thái R2 và ffmpeg, hàng đợi, lịch sử, nút **Chuyển video cũ lên R2**
  (gom file trùng nội dung theo MD5, nén, tải lên; xong video nào thì đổi đường dẫn bài học trong CSDL đó và ghi câu `UPDATE` vào `App_Data/r2-migration.sql`,
  nút "Tải SQL cho hosting"). File gốc trên máy chủ được giữ lại.
- **Kiểm thử** (IIS Express + bucket thật): tải video 13,8 MB → `r2:videos/...`, phát ngay từ file tạm (206), lên R2, 302 sang link ký, R2 trả 206 với Range,
  metadata `duration` = 317 giây, thư viện có `PlayUrl`, đường dẫn sai → 400, video cũ vẫn phát: 15/15 đạt. Object test và tài khoản test đã xóa.
  Phần **nén** chưa chạy thử vì `App_Data/ffmpeg.exe` hiện là bản shared thiếu DLL (trang Admin báo rõ).
- **Lên hosting**: thêm khóa R2 vào `Secrets.config` trên hosting và tải `ffmpeg.exe` (bản static) vào `App_Data` bằng File Manager (cả hai không đi theo Publish).
  Chạy `r2-migration.sql` trên CSDL hosting sau khi đã chuyển video cũ ở máy local.

## 29. Ghi chú bài học đồng bộ web và ứng dụng (mới, **chưa commit**)

- Trước đây ghi chú ở trang học (Learning/Study) lưu trong `localStorage` của trình duyệt: đổi máy / trình duyệt là mất, app không thấy.
  Nay lưu trong bảng `GhiChuBaiHoc` (`Database/Migrations/020_GhiChuBaiHoc.sql`), nghiệp vụ chung ở `Services/LessonNoteService.cs`
  (nội dung 1–1000 ký tự, tối đa 200 ghi chú mỗi bài, mốc giây của video; chỉ học viên đã ghi danh, chỉ sửa / xóa ghi chú của mình;
  xóa bài học thì xóa luôn ghi chú).
- **Web**: `Learning/Notes`, `AddNote`, `DeleteNote`; mở bài học thì ghi chú cũ còn trong `localStorage` được chuyển lên máy chủ một lần
  (`ImportNotes`, bỏ trùng). Mở bảng ghi chú là tải lại nên thấy ngay ghi chú vừa tạo trên app.
- **API** (Bearer token, cùng kiểm tra quyền học với G2–G4):

  | | Đường dẫn | Ghi chú |
  |---|---|---|
  | G5 | `GET /api/v1/learning/{courseId}/notes?lessonId=` | Ghi chú cả khóa (theo thứ tự bài rồi mốc giây), hoặc một bài |
  | G6 | `GET /api/v1/learning/{courseId}/lessons/{lessonId}/notes` | Ghi chú của một bài |
  | G7 | `POST /api/v1/learning/{courseId}/lessons/{lessonId}/notes` | Body `{ "positionSeconds": 75, "content": "..." }` |
  | G8 | `PUT /api/v1/notes/{id}` | Body `{ "content": "...", "positionSeconds": 80 }` (bỏ `positionSeconds` thì giữ mốc cũ) |
  | G9 | `DELETE /api/v1/notes/{id}` | |

  Mỗi ghi chú: `id, courseId, lessonId, lessonTitle, positionSeconds, content, createdAt, updatedAt`. Mã lỗi: `NOTE_EMPTY`, `NOTE_TOO_LONG`,
  `NOTE_LIMIT`, `NOTE_NOT_FOUND` (cả khi là ghi chú của người khác), `LESSON_NOT_FOUND`, `NOT_ENROLLED`, `COURSE_LOCKED`, `NOTES_NOT_INSTALLED` (503, chưa chạy 020).
- **Kiểm thử** (IIS Express): tạo trên app → web thấy, tạo trên web → app thấy, sửa / xóa, kiểm tra dữ liệu, người khác không sửa / xóa được,
  chuyển ghi chú cũ từ trình duyệt không nhân đôi: 27/27 đạt. Dữ liệu test đã xóa.
- **Lên hosting**: chạy `020_GhiChuBaiHoc.sql` (bỏ dòng `USE`) cùng lúc với bản publish này.

## 30. Trợ lý ECO: chatbot Gemini + công cụ tra dữ liệu (mới, **chưa commit**)

Trang thử: `/Chatbot` (chưa gắn khung chat nổi vào mọi trang). **Đang tắt**: chỉ bật khi `Chatbot:Enabled = true` (mục 32). Hiện có công cụ **tìm khóa học**; tiến độ học, đơn hàng, mã giảm giá, chính sách là bước sau.

| Thành phần | Việc làm |
| :--- | :--- |
| `Services/GeminiClient.cs` | Gọi `generateContent` bằng HttpClient. Mô hình chính `Gemini:Model`, dự phòng `Gemini:FallbackModel` (ghi được nhiều tên, ngăn cách dấu phẩy). 5xx / hết giờ: thử lại 3 lần (1 s, 2 s). 429 hoặc 404: sang mô hình kế ngay, nhớ mô hình hết hạn mức tới khi hết thời gian Google báo (tối đa 30 phút). 400 / 401 / 403: báo ngay. `Gemini:ThinkingLevel` mặc định `low` (nhanh hơn khoảng 2-3 lần, vẫn gọi công cụ đúng) |
| `Services/ChatbotCongCu.cs` | Khai báo công cụ cho Gemini và chạy chúng. Gemini chỉ đề nghị gọi (tên + tham số), **server chạy**. `tim_khoa_hoc` (tuKhoa, giaToiDa, capDo, sapXep, soLuong) dùng lại `SearchService` nên có tìm gần đúng. Mã người dùng lấy từ Session, không nhận từ Gemini; công cụ chỉ đọc |
| `Services/ChatbotService.cs` | Hướng dẫn hệ thống (chỉ dùng dữ liệu từ công cụ, từ chối ngoài phạm vi, chống chèn lệnh), vòng lặp gọi công cụ tối đa 4 lượt, nhớ 10 lượt gần nhất |
| `Controllers/ChatbotController.cs` | `POST /Chatbot/Hoi`, `/Chatbot/Xoa` (có token chống giả mạo). Lịch sử ở Session. Giới hạn 20 câu / 10 phút cho mỗi người (khách theo IP). Câu hỏi tối đa 500 ký tự |
| `Views/Chatbot/Index.cshtml` | Trang chat, thẻ khóa học bấm được, dòng nhỏ hiện mô hình và thời gian trả lời (để thử nghiệm) |

**Cấu hình** (`Secrets.config`, mẫu ở `Secrets.config.example`): `Gemini:ApiKey`, `Gemini:Model` (đang dùng `gemini-3.5-flash`), `Gemini:FallbackModel` (`gemini-3.5-flash-lite`), `Gemini:ThinkingLevel`. Khóa lấy ở aistudio.google.com bằng dự án **không gắn thanh toán** (gói miễn phí). Không cần migration.

**Giới hạn gói miễn phí (đo ngày 27/10/2026):** mỗi mô hình chỉ **20 yêu cầu mỗi ngày** (`GenerateRequestsPerDayPerProjectPerModel-FreeTier`), reset khoảng 21 giờ sau. Một câu hỏi dùng 2 yêu cầu (chọn công cụ, rồi viết câu trả lời), nên mỗi mô hình trả lời khoảng 10 câu mỗi ngày. Gói miễn phí còn được Google dùng nội dung gửi lên để cải thiện sản phẩm: chỉ dùng dữ liệu demo. `gemini-3.8-flash` hay báo 503 quá tải, không dùng làm mô hình chính.

**Đã thử:** 16 kiểm thử tự động (đầu vào rỗng / quá dài / sai token, giá lọc đúng với CSDL, mọi thẻ là khóa thật, nhớ ngữ cảnh, từ chối ngoài phạm vi, không lộ hướng dẫn hệ thống), cộng chuyển mô hình dự phòng khi chính hỏng, cả hai hỏng, sai khóa, 429, danh sách nhiều mô hình dự phòng.

## 31. API tài liệu bài học cho ứng dụng (mới, **chưa commit**)

Trước đây app không lấy được tài liệu giáo viên đính kèm bài học. Nay app đọc **cùng bảng `TaiNguyen`** với trang học trên web,
nên giáo viên tải tài liệu lên là cả web và app thấy ngay, không cần đồng bộ riêng. Không cần migration.

- **File**: `Services/LessonDocumentService.cs` (đọc thông tin tệp, tìm đường dẫn thật trên máy chủ), `Controllers/Api/LearningApiController.cs`
  (G10–G12, thêm trường vào G2 / G3), `Controllers/Api/CatalogApiController.cs` (B3). Postman: đã thêm G10–G12 và biến `documentId`
  vào `Tuan10\ECOLearning_API.postman_collection.json`.
- **API** (Bearer token; phải đã ghi danh khóa, cùng kiểm tra quyền với G2–G9):

  | | Đường dẫn | Ghi chú |
  |---|---|---|
  | G10 | `GET /api/v1/learning/{courseId}/documents?lessonId=` | Tài liệu cả khóa theo thứ tự bài (thêm `lessonId` để lọc một bài). Trả `{ courseId, total, items }` |
  | G11 | `GET /api/v1/learning/{courseId}/lessons/{lessonId}/documents` | Tài liệu một bài |
  | G12 | `GET /api/v1/documents/{id}/download?inline=true` | Trả **nội dung tệp**, không phải JSON. `inline=true` mở xem pdf / txt / csv; loại khác luôn tải về |
  | G2 | (bổ sung) | Mỗi bài thêm `documentsCount`; thêm `totalDocuments` của khóa |
  | G3 | (bổ sung) | Thêm mảng `documents` của bài (cùng dạng G10), màn hình bài học không cần gọi thêm |
  | B3 | (bổ sung) | Thêm `documentsCount` của khóa (không cần đăng nhập), để trang giới thiệu hiện "x tài liệu" |

  Mỗi tài liệu: `id, courseId, lessonId, lessonTitle, title` (tên hiển thị, không đuôi), `fileName` (tên lưu về máy, có đuôi),
  `fileType` (pdf, docx, xlsx, pptx, zip, txt, csv), `mimeType, sizeBytes` (null nếu không đọc được), `uploadedAt`,
  `isAvailable` (false: tệp đã mất trên máy chủ, app nên làm mờ nút tải), `canPreview, downloadUrl, previewUrl` (null nếu không xem trực tiếp được).
- **Cách app tải tệp**: gọi `downloadUrl` **kèm header `Authorization: Bearer`** (ví dụ Dio `download()`), lưu bằng `fileName`, rồi mở bằng
  ứng dụng trên máy (open_file...). Không mở thẳng `downloadUrl` bằng trình duyệt ngoài vì trình duyệt không gửi token (sẽ nhận 401).
  Tên tệp tiếng Việt nằm trong header `Content-Disposition` dạng `filename*=UTF-8''...`; app dùng sẵn `fileName` trong JSON là đủ.
  Tài liệu là liên kết ngoài (http) thì G12 trả 302 tới địa chỉ đó.
- **Mã lỗi**: `UNAUTHORIZED` (401), `NOT_ENROLLED` / `COURSE_LOCKED` (403), `COURSE_NOT_FOUND`, `LESSON_NOT_FOUND`,
  `DOCUMENT_NOT_FOUND`, `FILE_NOT_FOUND` (404, tệp không còn trên máy chủ).
- **An toàn**: API không trả đường dẫn tệp tĩnh `/Uploads/...`; chỉ đọc tệp trong `~/Uploads/` hoặc `~/Content/Uploads/` với đuôi giáo viên
  được phép tải lên (đường dẫn lạ trong CSDL, ví dụ `../Web.config`, bị coi là tệp không có).
- **Kiểm thử** (IIS Express, dữ liệu tạm đã xóa): 34/34 đạt, gồm danh sách / lọc theo bài / thứ tự bài, tên và loại tệp với dữ liệu cũ
  (`loaiTaiNguyen` ".pdf" hoặc "DOCX", tên thiếu đuôi), tải đúng từng byte, inline / attachment, tên tệp tiếng Việt, 401 / 403 / 404,
  liên kết ngoài 302, đường dẫn `../Web.config` không lộ nội dung, trang học web hiện đúng tài liệu app nhận được.
- **Còn lại**: trang học **web** vẫn link thẳng tệp tĩnh `/Uploads/...` (ai có link đều tải được, giống video cũ). Có thể chuyển web sang
  tải qua một action kiểm tra quyền như G12 nếu cần.

## 32. API trang chủ học viên, tìm giáo viên; tắt chatbot khi publish (mới, **chưa commit**)

- **I1 `GET /api/v1/home`** (`Controllers/Api/HomeApiController.cs`): gọi `HomeSectionService` như trang chủ web, trả các mục theo đúng thứ tự:
  `continue-learning`, `favorites`, `best-sellers`, `because-you-learned`, `top-rated`, `new-releases`, `featured-teachers`, `recently-viewed`.
  Mọi mục cùng khung `{ key, type, title, subtitle, total, period, anchorCourseId, anchorCourseTitle, items }`; thẻ khóa học = trường của B2 +
  `badge`, `note`, `enrollmentsInPeriod`. Không token: chỉ 4 mục chung. Mục không có dữ liệu / lỗi riêng thì bỏ qua, mục khác vẫn trả.
  Danh mục (B1), mã khuyến mãi (`/coupons/collectable`), gợi ý riêng (C1) dùng API có sẵn.
- **Tìm giáo viên** (`CatalogApiController`, dùng `SearchService.TimGiaoVien` của web): B5 thêm `teachers`; **B9** `GET /teachers/search?q=`
  (`isApproximate` = chỉ có người tên gần giống); **B10** `GET /teachers/{id}` hồ sơ công khai + khóa đang bán (không trả email, số điện thoại,
  ngân hàng; không phải giáo viên thì 404 `TEACHER_NOT_FOUND`); **B2** thêm `instructorId`.
- **Tắt chatbot**: `ChatbotController` chỉ chạy khi `Secrets.config` có `Chatbot:Enabled = true` (kiểm tra ở `OnAuthorization` để cả POST cũng
  trả 404). Không khai báo = tắt, nên bản publish lên hosting không mở `/Chatbot`.
- **Kiểm thử** (so trực tiếp với trang web cùng tài khoản): 43/43 (từng mục trang chủ cùng khóa, cùng thứ tự, cùng nhãn với web; tìm giáo viên
  5 cách gõ cho cùng kết quả với trang tìm kiếm và gợi ý của web; hồ sơ không lộ thông tin riêng; chatbot 404) + mục Yêu thích 2/2 (thả tim tạm rồi bỏ).
- **Tài liệu cho nhóm App**: `Tuan11\Tuan11.docx` (phần "Hướng dẫn API cho ứng dụng": Ghi chú, Tài liệu, Trang chủ, Tìm giáo viên);
  Postman `Tuan10\ECOLearning_API.postman_collection.json` đã có G5 – G12, B9, B10, nhóm I.

## 33. Nhập nhanh bài học từ CSV cho giáo viên (**đã gỡ bỏ**)

- Chức năng tạm thời phục vụ demo (chọn file CSV 3 cột Chương, Bài học, Video + các file video, hệ thống tạo chương / bài và tải video lần lượt),
  kiểm thử đạt 31/31, sau khi nhóm test xong đã **gỡ khỏi hệ thống**: xóa `CourseImportController`, view `CourseImport/Index`, mục menu trái
  và các nút "Nhập nhanh từ CSV" (trang đề cương, chi tiết, sửa khóa học). Các chương / bài đã nhập trước đó là dữ liệu bình thường, vẫn giữ nguyên.

## 34. Sửa lỗi mục tiêu đầu ra dài bị cắt vụn (**chưa commit**)

- **Lỗi**: trang chi tiết khóa học và thẻ khóa học (trang chủ, danh sách) tách `mucTieuDauRa` theo cả dấu phẩy, nên một mục tiêu như
  "Hiểu cú pháp cơ bản, biến, kiểu dữ liệu (chuỗi, danh sách...)" bị cắt thành nhiều mục rời, vỡ bố cục 2 cột.
- **Sửa**: `Services/CourseOutcomes.cs` (`Tach`) dùng chung cho `Views/Course/Details.cshtml`, `Views/Shared/_CourseCard.cshtml`,
  `Views/Home/Index.cshtml` và API B3 (`outcomes`): tách theo dòng; dữ liệu cũ không có xuống dòng thì tách theo `;`; **không** tách theo dấu phẩy.
- **Ô nhập của giáo viên** (`Areas/Instructor/Views/Course/Create.cshtml`, dùng cho cả Tạo và Sửa khóa học): mỗi mục tiêu là ô tự giãn chiều cao
  (`assets/js/outcome-field.js`), đọc được hết câu dài, tối đa 250 ký tự, Enter thêm dòng mới, dán nhiều dòng thì gộp thành một dòng.
- `Areas/Instructor/Views/Course/Edit.cshtml` **không được dùng** (action Edit trả về view Create): sửa giao diện trang Sửa khóa học ở Create.cshtml.
- **Kiểm thử**: 13/13 (trang chi tiết, API, dữ liệu cũ dạng `;`, ô nhập trang Tạo / Sửa trên Edge headless).

## 35. Giáo viên Tạm ngưng / Mở bán lại / Xóa khóa học (**chưa commit**)

- **Trạng thái mới** (`Services/CourseStatus.cs`, không cần migration): `Tạm ngưng` (giáo viên ngưng bán), `Đã xóa` (xóa mềm).
  Cả hai không nằm trong nhóm "đang bán" nên trang chủ, tìm kiếm, danh sách, giỏ hàng, API tự loại ra; trang học chỉ chặn
  `Khóa vĩnh viễn` nên **học viên đã mua vẫn học** ở cả hai trạng thái.
- **Quy tắc** (`Services/CourseLifecycleService.cs`):
  - Tạm ngưng: chỉ khóa đang xuất bản, không có bản sửa đang chờ duyệt. Mở bán lại: về `Đã xuất bản` ngay, không cần Admin duyệt.
    Trong lúc tạm ngưng không sửa nội dung / đề cương / gửi duyệt (mở bán lại trước, sửa đi qua luồng duyệt bản sửa như cũ).
  - Xóa: không xóa khóa đang chờ duyệt, đang xử lý vi phạm (Tạm khóa, Khóa vĩnh viễn) hoặc đang tạm ẩn.
    Khóa **chưa có lịch sử** (đăng ký, đơn hàng, đánh giá, tiến độ, báo cáo / xử lý vi phạm, quiz) thì **xóa hẳn** cả chương, bài, bài lẻ,
    tài liệu, yêu cầu duyệt; video vẫn còn trong Thư viện tài nguyên. **Đã có lịch sử** thì **xóa mềm**: khóa biến mất khỏi khu giáo viên
    (danh sách, chi tiết, tổng quan, đề cương), doanh thu / phiếu chi / danh sách học viên vẫn tính khóa này.
  - Mọi thao tác kiểm tra khóa thuộc giáo viên đang đăng nhập; đổi trạng thái có điều kiện trạng thái cũ (tránh bấm hai lần).
- **Giao diện**: nút ở danh sách khóa học (biểu tượng, thêm tab "Tạm ngưng") và thanh thao tác trang chi tiết (partial
  `Areas/Instructor/Views/Course/_CourseLifecycleActions.cshtml`), hộp xác nhận nói rõ hệ quả (`instructorConfirmAction` trong layout).
  Nút "Xóa khóa học" cũ chỉ báo "đang phát triển" đã được thay bằng chức năng thật. Sửa kèm: trang Tổng quan giáo viên trước đây hiện khóa
  `Đã xuất bản` thành "Bản nháp" (dùng `CourseStatus.TeacherBadgeText`).
- **Admin**: vẫn Tạm khóa / Khóa vĩnh viễn được khóa đang tạm ngưng (`ViolationReportService`), để giáo viên không né xử lý vi phạm
  bằng cách tạm ngưng; mở lại sau vi phạm trả về `Tạm ngưng`. Thống kê hiện nhóm "Tạm ngưng (GV ngưng bán)", "Đã xóa (GV xóa)".
- **Kiểm thử**: 31/31 (nút đúng theo trạng thái, tạm ngưng → ẩn trang bán / API / giỏ hàng nhưng học viên vẫn học, chặn sửa và đề cương,
  mở bán lại, xóa hẳn khóa nháp và khóa đang bán chưa có học viên, xóa mềm khóa có học viên, chặn khóa chờ duyệt / khóa người khác / xóa lại).

## 36. Báo cáo doanh thu Admin: giảm trực tiếp + chia giáo viên / nền tảng (**chưa commit**)

- **Trước**: báo cáo "Doanh thu theo ngày" chỉ có Trước giảm, Giảm (coupon), Thực thu. Thực thu là 100% tiền học viên trả nên dễ hiểu nhầm
  là doanh thu của nền tảng; khuyến mãi giảm trực tiếp (migration 021) không hiện vì "Trước giảm" lấy giá đã giảm.
- **Nay** (`Services/RevenueService.cs`, `Areas/Admin/Views/RevenueReport/Index.cshtml`, CSV ở `RevenueReportController`):
  - Trước giảm = **giá gốc** (`ChiTietDonHang.giaGoc`; đơn cũ / chưa chạy 021 thì = đơn giá), thêm **Giảm trực tiếp** = giá gốc − đơn giá,
    Giảm coupon như cũ. Thực thu không đổi (tổng đơn giá − coupon).
  - Thêm **Phần của giáo viên (80%)** và **Doanh thu nền tảng (20%)**: tính từng đơn, nền tảng = thực thu − phần giáo viên nên tổng luôn khớp.
    Tiền giảm hai bên cùng chịu theo tỷ lệ, giống trang Nhận thanh toán. Biểu đồ theo ngày tô đậm phần nền tảng, bảng và CSV có cột tương ứng.
  - Tỷ lệ 80% gom về một hằng số `RevenueService.TyLeGiaoVien` (Nhận thanh toán của giáo viên và Quản lý giáo viên dùng lại).
- **Kiểm thử**: câu SQL + phép tính đúng trên đơn thử (giá gốc 700k, giảm trực tiếp 100k, coupon 60k → thực thu 540k, GV 432k, nền tảng 108k);
  41 đơn thật trong 1 năm: thực thu khớp công thức cũ, GV + nền tảng = thực thu. Trang Admin + file CSV: 12/12 (ô tổng, dòng đơn, biểu đồ, cột CSV, dòng tổng cộng).

## 37. API học viên: Google, đánh giá hữu ích, báo cáo vi phạm, liên hệ, Trợ lý ECO (**chưa commit**)

Đưa các chức năng học viên còn thiếu trên app sang API, dùng chung service và quy tắc với website.

| Mã | Endpoint | Ghi chú |
| :--- | :--- | :--- |
| A12 | `POST /api/v1/auth/google` | Body `{ "idToken" }` (ID token của `google_sign_in`, `serverClientId` = Client ID web). Trả như A1. Token sai: 401 `GOOGLE_INVALID_TOKEN`; khóa / sai vai trò như A1 |
| B4 | `GET /api/v1/courses/{id}/reviews` | Thêm `notHelpfulCount`, `myVote` (`true` / `false` / `null`), `isMine` |
| E2 | `POST /api/v1/reviews/{reviewId}/helpful` | Cần token. Body `{ "helpful": true \| false }`; gửi lại lựa chọn đang có = bỏ bình chọn. Trả `{ helpfulCount, notHelpfulCount, myVote }`. 403 `OWN_REVIEW`, 404 `REVIEW_NOT_FOUND` |
| J1 | `GET /api/v1/violation-types` | 6 loại vi phạm (như website) |
| J2 | `POST /api/v1/courses/{id}/reports` | Cần token. Body `{ "type", "description", "lessonId"? }`. 400 (lỗi theo trường), 403 `NOT_ENROLLED` / `OWN_COURSE`, 404 `COURSE_NOT_FOUND`, 409 `REPORT_PENDING` |
| J3 | `POST /api/v1/contact` | Body `{ "fullName", "email", "subject"?, "content" }`; đã đăng nhập thì bỏ trống họ tên / email = lấy theo tài khoản. Tối đa 5 lần / 10 phút (429), lỗi gửi mail 502 `EMAIL_FAILED` |
| K1 | `POST /api/v1/chatbot/ask` | Body `{ "question" }` (≤ 500 ký tự) → `{ reply, courses: [{ id, title, thumbnail, category, instructor, price, rating, reviewCount }] }`. Gemini lỗi: 503 `CHATBOT_QUOTA` / `CHATBOT_BUSY` / `CHATBOT_UNAVAILABLE` |
| K2 | `GET /api/v1/chatbot/history` | `{ messages: [{ role: user \| model, content }] }` |
| K3 | `DELETE /api/v1/chatbot/history` | Bắt đầu cuộc trò chuyện mới |

- **Dùng chung với website**: `Services/GoogleAuthService.cs` (tách từ `AccountController.GoogleLogin`: xác thực token, tạo / cập nhật tài khoản),
  `CourseReviewService.BinhChon`, `ViolationReportService.CreateReport`, `ChatbotService`, `EmailService.SendContactMessage`.
  `Services/RateLimiter.cs` thay bộ đếm riêng của `ChatbotController`: hỏi trợ lý trên web và app tính chung 20 câu / 10 phút.
- **Trợ lý trên app**: tắt theo `Chatbot:Enabled` như website (mọi đường dẫn K trả 404 `CHATBOT_DISABLED`). App không có Session nên lịch sử
  lưu trên máy chủ theo tài khoản (giữ 2 giờ từ lần dùng cuối, mất khi khởi động lại IIS); khách vẫn hỏi được nhưng không nhớ câu trước.
- **Code**: `Controllers/Api/SupportApiController.cs` (J), `Controllers/Api/ChatbotApiController.cs` (K), `CatalogApiController` (B4, E2), `AuthApiController` (A12).
- **Kiểm thử** `Tests/StudentSupportApiSmoke.py` (IIS Express trên máy, tài khoản demo, dọn dữ liệu tạm trong `finally`): **52/52** đạt, gồm
  A12 (thiếu / sai token), E2 (chọn, đổi, bỏ bình chọn; đối chiếu DB; B4 hiện đúng `myVote`, `isMine` cho người chọn / tác giả / khách),
  J2 (mọi lỗi 400 / 403 / 404 / 409 không ghi DB, báo cáo hợp lệ ghi đúng 1 dòng "Chờ xử lý" kèm bài học), J3 (lỗi theo trường, lấy tên / email
  theo tài khoản), K1–K3 (gọi Gemini thật, lịch sử 1 lượt, tài khoản khác không thấy, xóa lịch sử). Chưa chạy: gửi email liên hệ thật
  (`--send-email`), giới hạn 429 (cần gửi thật nhiều lần), A12 với token Google thật (thử bằng app).

## 38. Việc còn lại / cần quyết định

| Việc | Ghi chú |
| :--- | :--- |
| Commit trang danh mục (mục 12), cold-start (mục 13) và Marketing (mục 14) | Đang ở thư mục làm việc nhánh `phu`. Thành viên khác cần chạy migration 007 (cả `007_GoiY_SoThichNguoiDung.sql` và `007_Quiz_ChungChi.sql` của nhánh `ngoc`), 008, 009, 010, 011 |
| Đã merge nhánh `ngoc` (Quiz cuối khóa, cấp chứng chỉ tự động) | Conflict ở `LearningController.MyCourses` và `MyCourses.cshtml`: giữ phân trang (mục 19), thêm trạng thái quiz cho các khóa của trang đang xem, tab "Đã học đủ bài", làm tròn tiến độ xuống. Học viên phải đạt quiz mới nhận chứng chỉ và mới gửi được đánh giá |
| Thành viên khác chạy migration 006, 007 và job Python | Xem mục 0. Không chạy job thì chỉ thấy khóa phổ biến, không có gợi ý lọc cộng tác |
| Mục "Khóa học tương tự" (theo mọi tương tác) ở trang chi tiết | Trang chi tiết đã có 2 mục dựa trên dữ liệu mua (mục 16). `sp_GoiY_KhoaHocTuongTu` (dựa trên `course_similarity`) vẫn chưa gắn; Hình 4.13 trong báo cáo cần vẽ lại theo 2 mục mới |
| Link danh mục ở footer | `_Layout.cshtml` vẫn dùng mã giả (`category = "CNTT"`, `"Design"`, `"Business"`), nên đổi sang trang danh mục |
| `ReviewController.SubmitReview` | Chỉ kiểm tra đăng nhập, **chưa kiểm tra đã đăng ký khóa và tiến độ**. Giao diện học yêu cầu 100% bài, hàm `fn_DuDieuKienDanhGia` (25%) chưa được gọi, báo cáo ghi 70%. Cần chốt một ngưỡng và kiểm tra phía server |
| Quản lý người dùng của Admin | **Đã làm** (mục 26). Hình 4.37–4.38 trong báo cáo cần chụp lại theo giao diện mới; tài khoản tạo qua Google trước 21/10 còn mật khẩu chung `GoogleOAuth2026` trong CSDL, nên "Đặt lại mật khẩu" hoặc đổi sang mật khẩu ngẫu nhiên |
| Chạy migration 016 trên hosting | Xóa dòng `USE` rồi chạy trên CSDL hosting trước khi Publish bản có mục 26 |
| Bảo mật cấu hình | `Web.config` (được đưa lên Git) đang chứa `SmtpPassword` (mật khẩu ứng dụng Gmail). Nên chuyển sang `Secrets.config` và thu hồi / tạo lại mật khẩu ứng dụng |
| Thanh toán thật | Đã tích hợp **VNPay Sandbox** (mục 23.1) và cổng thử nghiệm dự phòng. Khi có pháp nhân: ký hợp đồng VNPay, đổi `VNPay:TmnCode`, `VNPay:HashSecret`, `VNPay:PaymentUrl`, `VNPay:ApiUrl` sang môi trường thật trong `Secrets.config`, cấu hình IPN về tên miền HTTPS công khai. Chưa có hoàn tiền; trang Dashboard / Báo cáo của giáo viên vẫn tính doanh thu theo giá bán trước mã giảm giá (trang Nhận thanh toán đã tính theo thực thu) |
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
| Màu khu vực giáo viên | Layout / Dashboard / Payout / Analytics / Student của khu vực giáo viên còn dùng `#0D9488`; đổi nếu muốn đồng bộ toàn hệ thống |
| Header chưa co giãn trên điện thoại | Ở màn hình ~390 px trang bị tràn ngang (có từ trước, không thuộc phần tìm kiếm) |
| Khóa có `giaKhuyenMai` (dữ liệu cũ) | **Đã xử lý** (mục 27): không còn đọc `giaKhuyenMai`, giá = `giaBan`. Khóa từng có giá giảm nay hiện giá gốc; Marketing tạo mã nếu muốn giảm tiếp |
| Chạy migration 018 trên hosting | Chạy **cùng lúc** với bản publish có mục 27 (code cũ ghi trạng thái chiến dịch `HoatDong` sẽ bị từ chối sau 018) |
| Video trên R2 | **Đã làm** (mục 28). Còn: thay `App_Data/ffmpeg.exe` bằng bản static rồi bấm "Chuyển video cũ lên R2"; `VideoStream/Play` chưa kiểm tra quyền xem (giống video cũ: ai có link đều xem được); xóa video khỏi thư viện chưa xóa object trên R2 |
