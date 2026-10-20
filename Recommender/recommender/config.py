"""Cấu hình hệ thống gợi ý: trọng số tín hiệu tương tác, tham số thuật toán, nguồn dữ liệu."""
import os
from pathlib import Path

USER_COL = "user_id"
COURSE_COL = "course_id"
SCORE_COL = "score"

# Điểm tương tác của một ô (user, course) = tổng weight * (giá trị / max), kết quả trong [0, 1].
#   max = số cố định  -> chia cho số đó (rating /5, progress /100, likes 0/1)
#   max = None        -> chia cho max thực tế trong dữ liệu (views, clicks)
# Tổng trọng số phải bằng 1.
#
# Thêm bình luận sau này: thêm cột "comments" vào nguồn dữ liệu và một dòng
#   "comments": {"weight": 0.1, "max": None},
# rồi giảm trọng số các tín hiệu khác để tổng vẫn bằng 1. Không phải sửa code thuật toán.
SIGNAL_WEIGHTS = {
    "rating":           {"weight": 0.4, "max": 5},
    "views":            {"weight": 0.2, "max": None},
    "clicks":           {"weight": 0.1, "max": None},
    "likes":            {"weight": 0.1, "max": 1},
    "progress_percent": {"weight": 0.2, "max": 100},
}

# Cặp khóa học có ít hơn số người dùng chung này thì độ tương đồng = 0
MIN_COMMON_USERS = 2

TOP_K = 10
# Số khóa tương tự lưu cho mỗi khóa trong course_similarity (None = lưu mọi cặp > 0)
SIMILARITY_TOP_N = 50
# Số gợi ý lọc cộng tác lưu cho mỗi người dùng trong KhoaHocKhuyenNghi
USER_RECS_TOP_N = 20

DATA_DIR = Path(__file__).resolve().parent.parent / "data"
OUTPUT_DIR = Path(__file__).resolve().parent.parent / "output"
CSV_INTERACTIONS = DATA_DIR / "user_interactions_clean.csv"
CSV_COURSES = DATA_DIR / "courses_metadata.csv"

# Chuỗi kết nối SQL Server (ghi đè bằng biến môi trường ECO_SQL_CONN cho từng máy)
SQL_CONNECTION_STRING = os.environ.get(
    "ECO_SQL_CONN",
    "DRIVER={ODBC Driver 17 for SQL Server};SERVER=.\\SQLEXPRESS;"
    "DATABASE=QL_HETHONG_CUNGCAP_KHOAHOC;Trusted_Connection=yes;TrustServerCertificate=yes;",
)
