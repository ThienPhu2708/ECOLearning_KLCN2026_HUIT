# Gợi ý khóa học: Item-based Collaborative Filtering

Lọc cộng tác dựa trên khóa học (memory-based), **không huấn luyện mô hình**. Gồm 4 bước:
ma trận user × course → cosine item-item → sắp xếp giảm dần → top-K.

Có hai luồng, cùng công thức và cho cùng kết quả (đã kiểm tra khớp 100% giữa Python và SQL):

| Luồng | Khi nào | Làm gì |
| :--- | :--- | :--- |
| **Job định kỳ** (Python, `build_recommendations.py`) | Mỗi đêm (Task Scheduler) hoặc chạy tay | Tính lại **cả 4 bước** cho mọi người: ma trận cosine → `course_similarity`, trọng số → `GoiY_TrongSo`, gợi ý → `KhoaHocKhuyenNghi` |
| **Nút "Gợi ý khóa học"** (SQL, `sp_GoiY_TinhGoiYChoNguoiDung`) | Người dùng bấm | Chỉ tính lại gợi ý cho **người đang bấm** từ tương tác mới nhất của họ × ma trận đã lưu (không tính lại cosine), lưu vào `KhoaHocKhuyenNghi` rồi trả danh sách. Khoảng 70 ms |

Web ASP.NET không bao giờ tính cosine khi có request. Khóa học mới chỉ có trong ma trận sau lần chạy job tiếp theo
(trước đó vẫn được bù vào danh sách như khóa phổ biến).

```
Recommender/
├── recommender/
│   ├── config.py          # trọng số tín hiệu, min_common_users, K, chuỗi kết nối
│   ├── scoring.py         # điểm tương tác tổng hợp [0, 1]
│   ├── item_cf.py         # 4 bước CF: ItemCF.fit / recommend_for_user / similar_courses / popular_courses
│   ├── data_sources.py    # CsvDataSource, SqlServerDataSource (cùng cấu trúc cột)
│   ├── sql_store.py       # ghi GoiY_TrongSo, course_similarity, KhoaHocKhuyenNghi
│   └── evaluation.py      # chia train/test, Precision/Recall/HitRate@K
├── run_demo.py            # chạy mẫu trên CSV
├── evaluate.py            # đánh giá offline, so với baseline phổ biến
├── build_recommendations.py  # JOB định kỳ (csv -> output/, sql -> CSDL)
├── tests/test_item_cf.py  # ví dụ nhỏ tính tay
└── data/                  # user_interactions_clean.csv, courses_metadata.csv (214 khóa + course_url)
Database/Migrations/005_GoiY_LocCongTac.sql   # bảng, view, stored procedure
```

## Chạy

```bash
cd Recommender
pip install -r requirements.txt
python run_demo.py --user U0001 --course C2392 --k 10
python evaluate.py --min-common 1 2 3
python -m unittest discover tests
python build_recommendations.py --source csv        # xuất output/*.csv để xem
python build_recommendations.py --source sql        # đọc + ghi SQL Server (cần chạy migration 005 trước)
```

Chuỗi kết nối mặc định là `.\SQLEXPRESS` / `QL_HETHONG_CUNGCAP_KHOAHOC` (Windows Authentication).
Máy khác thì đặt biến môi trường `ECO_SQL_CONN`, ví dụ:
`DRIVER={ODBC Driver 17 for SQL Server};SERVER=.;DATABASE=QL_HETHONG_CUNGCAP_KHOAHOC;Trusted_Connection=yes;TrustServerCertificate=yes;`

## Logic

| Bước | Hàm | Ghi chú |
| :--- | :--- | :--- |
| Điểm ô | `scoring.compute_interaction_score` | `Σ weight × min(giá trị / max, 1)`. `max=None` nghĩa là lấy max thực tế (views = 100, clicks = 63 trên CSV) |
| 1 | `item_cf.build_interaction_matrix` | pivot user × course, ô chưa tương tác = 0 |
| 2 | `item_cf.cosine_item_similarity` | `XᵀX / (‖xᵢ‖‖xⱼ‖)`; đường chéo = 0; cặp có < `min_common_users` user chung thì = 0 |
| 3–4 | `item_cf.top_k_indices` | bỏ giá trị 0, sắp giảm dần (hòa thì theo course_id), cắt K |
| Gợi ý cho user | `ItemCF.recommend_for_user` | `pred(j) = Σᵢ sim(i, j) × score(u, i)` trên các khóa i user đã tương tác; bỏ khóa đã học và điểm 0. Cột `because_course_id` là khóa đóng góp nhiều nhất, dùng để hiển thị "Vì bạn đã học …" |
| Khóa tương tự | `ItemCF.similar_courses` | một hàng của ma trận tương đồng, sắp xếp rồi lấy top-K |
| Cold-start | `ItemCF.popular_courses` | xếp theo số người tương tác, hòa thì theo rating TB; cũng dùng để bù khi CF trả về ít hơn K khóa |

Trọng số nằm ở `config.SIGNAL_WEIGHTS`:

```python
SIGNAL_WEIGHTS = {
    "rating":           {"weight": 0.4, "max": 5},
    "views":            {"weight": 0.2, "max": None},
    "clicks":           {"weight": 0.1, "max": None},
    "likes":            {"weight": 0.1, "max": 1},
    "progress_percent": {"weight": 0.2, "max": 100},
}
```

Trọng số chỉ khai báo ở `config.py`. Mỗi lần chạy, job ghi bộ trọng số và giá trị chuẩn hóa thực tế vào `GoiY_TrongSo`,
và nút bấm đọc bảng này, nên SQL luôn tính điểm giống hệt lúc dựng ma trận. Đổi trọng số thì chạy lại job.

**Thêm bình luận sau này** (không sửa thuật toán):
1. Trong view `vw_GoiY_TuongTac`, thêm cột `comments` (đếm số bình luận theo cặp user–course).
2. Trong view `vw_GoiY_TinHieu`, thêm một dòng `('comments', CAST(t.comments AS FLOAT))`.
3. Trong `SIGNAL_WEIGHTS`, thêm `"comments": {"weight": 0.1, "max": None}` rồi giảm trọng số tín hiệu khác để tổng vẫn bằng 1
   (code kiểm tra điều này và báo lỗi nếu tổng ≠ 1). Chạy lại job.

## CSDL (migration `005_GoiY_LocCongTac.sql`)

| Đối tượng | Vai trò |
| :--- | :--- |
| `TuongTacKhoaHoc(maHocVien, maKhoaHoc, luotXem, luotClick, daThich, lanCapNhat)` | Lưu views / clicks / likes. CSDL hiện chưa có các hành vi này; ứng dụng sẽ ghi vào bảng ở giai đoạn làm giao diện |
| `vw_GoiY_TuongTac` | Trả về đúng các cột như file CSV: `rating` lấy từ `DanhGiaKhoaHoc.soSao`, `progress_percent` từ `TienDoHocTap`, `views/clicks/likes` từ `TuongTacKhoaHoc`. Mỗi cặp có đăng ký, tiến độ, đánh giá hoặc tương tác là một dòng; thiếu tín hiệu nào thì tín hiệu đó = 0 |
| `course_similarity(course_id_a, course_id_b, similarity, computed_at)` | PK `(a, b)`, chỉ lưu cặp > 0, mỗi cặp lưu cả hai chiều, mỗi khóa giữ tối đa `SIMILARITY_TOP_N = 50` láng giềng. Index `(course_id_a, similarity DESC)` để lấy top-K |
| `GoiY_TrongSo(tinHieu, trongSo, giaTriChuanHoa, capNhatLuc)` | Trọng số + giá trị chuẩn hóa job đã dùng (views 100, clicks 63 trên dữ liệu thử) |
| `vw_GoiY_TinHieu` | `vw_GoiY_TuongTac` xoay dọc thành `(user, course, tinHieu, giaTri)` để nhân với trọng số |
| `fn_GoiY_KhoaHocDuocGoiY(@maHocVien)` | Khóa được phép gợi ý (đang mở bán, không do chính người đó dạy, chưa tương tác) kèm độ phổ biến; dùng chung cho các SP |
| `KhoaHocKhuyenNghi` (bảng sẵn có) | Job ghi top-20 gợi ý CF cho mỗi người; nút bấm ghi đè phần của người bấm. `maGoiY = GY{maHocVien}_{hạng}` |
| `sp_GoiY_KhoaHocTuongTu @maKhoaHoc, @topK` | Mục "Khóa học tương tự" |
| `sp_GoiY_KhoaHocChoNguoiDung @maHocVien, @topK` | Mục "Khóa học dành cho bạn" hiển thị tự động: đọc `KhoaHocKhuyenNghi`, lọc lại khóa đã tương tác / không còn mở bán / do chính mình dạy; thiếu thì bù khóa phổ biến. Cột `nguon` = `LocCongTac` / `PhoBien` |
| `sp_GoiY_TinhGoiYChoNguoiDung @maHocVien, @topK` | Nút **"Gợi ý khóa học"**: tính lại cho một người (bước 1: vector điểm mới nhất; bước 2: ma trận đã lưu; bước 3–4: Σ sim × điểm, sắp xếp, top-K), lưu vào `KhoaHocKhuyenNghi`, trả thêm `maKhoaHocLyDo`, `tenKhoaHocLyDo` ("Vì bạn đã học …") và `maTranCapNhatLuc` |

Job ghi lại toàn bộ các bảng trong transaction. Chỉ lưu gợi ý cho khóa `Đã xuất bản` và người dùng
`HocVien`/`GiaoVien` (trigger `trg_KhoaHocKhuyenNghi_KiemTraDuLieu` yêu cầu như vậy). Nút bấm với Admin hoặc khách vẫn
trả về danh sách nhưng không lưu. Hai lần bấm cùng lúc của một người được khóa bằng `sp_getapplock`, nên không ghi chồng lên nhau.

**Lịch chạy** (Windows Task Scheduler, 0:00 mỗi đêm). `chay_job_goi_y.bat` chạy job và ghi log vào `output\job.log`.
Tạo tác vụ bằng PowerShell (thay đường dẫn cho đúng máy, tham số là `python.exe` đã cài pandas/pyodbc):

```powershell
$dir = 'D:\...\ECOLearning\Recommender'
$a = New-ScheduledTaskAction -Execute "$dir\chay_job_goi_y.bat" -Argument 'C:\Python314\python.exe' -WorkingDirectory $dir
$t = New-ScheduledTaskTrigger -Daily -At '00:00'
$s = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit (New-TimeSpan -Minutes 30)
Register-ScheduledTask -TaskName 'ECOLearning_GoiY' -Action $a -Trigger $t -Settings $s
Start-ScheduledTask -TaskName 'ECOLearning_GoiY'      # chạy thử ngay, xem output\job.log
```

`-StartWhenAvailable`: nếu lúc 0:00 máy tắt hoặc ngủ thì job chạy ngay khi máy bật lại. Mặc định tác vụ chỉ chạy khi đang đăng nhập.
Trên server thật, tạo tác vụ với quyền Administrator và chọn "Run whether user is logged on or not" bằng một tài khoản có quyền vào CSDL.
SQL Server Express không có SQL Server Agent, nên dùng Task Scheduler.

Muốn cập nhật ngay (ví dụ sau khi nhập nhiều dữ liệu) thì chạy lệnh trên bằng tay. Với 2.800 tương tác, job chạy khoảng 1 giây
khi xuất CSV và khoảng 5 giây khi ghi SQL Server (ghi từng dòng vì có trigger).

## Tích hợp giao diện

Đã làm (`Services/RecommendationService.cs`):

| Vị trí | Gọi | Ghi chú |
| :--- | :--- | :--- |
| Trang chủ, mục **"Khóa học dành riêng cho bạn"** (`HomeController.Index`) | `sp_GoiY_KhoaHocChoNguoiDung` (chỉ đọc), 3 khóa | Nút "Xem tất cả khóa học" mở trang Khóa học gợi ý |
| Banner trang chủ, nút **"Xem gợi ý cá nhân"** → trang **Khóa học gợi ý** `/Recommendation` (`RecommendationController`) | `sp_GoiY_TinhGoiYChoNguoiDung`, 12 khóa | Mỗi lần mở trang hoặc bấm **"Làm mới gợi ý"** thì tính lại cho người đang đăng nhập. Hiện "Vì bạn đã học …", số khóa đã tương tác, thời điểm tính và thời điểm ma trận cập nhật |

Thẻ khóa học dùng chung partial `Views/Shared/_RecommendedCourseCard.cshtml`. Thông tin khóa học nạp bằng EF theo đúng thứ tự
stored procedure trả về.

**Chỉ hiển thị kết quả lọc cộng tác thuần:** `RecommendationService.HienThiKhoaPhoBien = false` lọc bỏ khóa phổ biến ở bước hiển thị
(logic cold-start vẫn nằm trong stored procedure; đổi thành `true` để hiện lại, kèm nhãn "Phổ biến"). Khi không có gợi ý:

| Trường hợp | Thông báo |
| :--- | :--- |
| Chưa có tương tác nào (new user cold start) | "Bạn cần xem một số khóa học để hiển thị gợi ý." + nút Khám phá khóa học |
| Có tương tác nhưng các khóa đó chưa có khóa tương tự trong `course_similarity` (chưa có khóa nào được học cùng bởi ≥ 2 người) | "Các khóa học bạn đã xem chưa có khóa học tương tự để gợi ý." |

**Ghi lượt xem:** `Course/Details` gọi `RecommendationService.RecordView`, cộng `luotXem` trong `TuongTacKhoaHoc`
(mỗi khóa một lần mỗi phiên; bỏ qua Admin và giáo viên xem khóa của chính mình). Xem một khóa có khóa tương tự rồi mở
trang Khóa học gợi ý thì gợi ý xuất hiện ngay.

Chưa làm:
- **Khóa học tương tự** (`Course/Details/{id}`): `EXEC dbo.sp_GoiY_KhoaHocTuongTu @p0, @p1`.
- Ghi `luotClick` (xem thử / thêm giỏ / mở bài học) và `daThich` (cần thêm nút Thích) vào `TuongTacKhoaHoc`.

## Kết quả đánh giá (CSV, 5 seed, giấu 20% mỗi user, `min_common_users = 2`)

| K | Phương pháp | Precision@K | Recall@K | HitRate@K | Độ phủ danh mục |
| :-: | :--- | :-: | :-: | :-: | :-: |
| 5 | **ItemCF** | **0.0755 ± 0.0040** | **0.2366 ± 0.0128** | **0.3503 ± 0.0187** | 86.2% |
| 5 | Phổ biến (baseline) | 0.0093 ± 0.0026 | 0.0291 ± 0.0085 | 0.0463 ± 0.0128 | 3.9% |
| 5 | Ngẫu nhiên (tham khảo) | 0.0082 ± 0.0013 | 0.0246 ± 0.0021 | 0.0406 ± 0.0062 | 100% |
| 10 | **ItemCF** | **0.0741 ± 0.0030** | **0.4577 ± 0.0196** | **0.6149 ± 0.0244** | 93.8% |
| 10 | Phổ biến (baseline) | 0.0096 ± 0.0014 | 0.0580 ± 0.0118 | 0.0931 ± 0.0138 | 6.7% |
| 10 | Ngẫu nhiên (tham khảo) | 0.0080 ± 0.0009 | 0.0491 ± 0.0042 | 0.0777 ± 0.0087 | 100% |

Ảnh hưởng của `min_common_users` (K = 10, HitRate): 1 → 0.622, **2 → 0.615**, 3 → 0.552.

Nhận xét:
- ItemCF vượt baseline phổ biến khoảng 7–8 lần trên mọi chỉ số. Baseline phổ biến gần như ngang ngẫu nhiên, vì mỗi khóa chỉ có 2–29 người
  và độ phổ biến khá đều, nên "khóa đông người học" không mang nhiều thông tin.
- Precision thấp là do cách đo: mỗi user chỉ bị giấu trung bình 1,6 khóa, nên Precision@10 tối đa chỉ khoảng 0,16
  và Precision@5 tối đa khoảng 0,32. Recall và HitRate phản ánh chất lượng rõ hơn.
- **Kết quả này lạc quan.** Dữ liệu tương tác được sinh theo cụm danh mục: trung bình 76% khóa của một user thuộc cùng một
  `category_keyword`, trong khi chọn ngẫu nhiên chỉ khoảng 30%. Item-CF khai thác đúng cấu trúc này. Dữ liệu thật thường nhiễu hơn,
  nên cần đo lại khi hệ thống đã có đủ tương tác.
- `min_common_users = 2` gần như không làm giảm độ chính xác so với 1, nhưng loại được các cặp chỉ trùng một người (dễ nhiễu).
  Tăng lên 3 thì mất nhiều cặp và HitRate giảm rõ, nên giữ mức 2.
- Với dữ liệu đầy đủ, mọi user đều nhận đủ 10 gợi ý CF, nên "ItemCF + bù phổ biến" cho kết quả trùng ItemCF. Phần bù chỉ có tác dụng
  với user mới hoặc dữ liệu thưa.
- **Lưu ý dữ liệu:** trong 214 khóa có 47 tên khóa xuất hiện dưới nhiều `course_id` (ví dụ "Introduction to Psychology" có 5 mã
  C2734, C3423, C5479, C5680, C7441, cùng URL nhưng khác `category_keyword`), do bộ dữ liệu Coursera gốc liệt kê một khóa theo nhiều danh mục.
  Vì vậy danh sách gợi ý trên CSV có thể có tên trùng nhau. CSDL thật không gặp vấn đề này vì mỗi khóa có một `maKhoaHoc`.
