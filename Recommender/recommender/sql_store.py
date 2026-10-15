"""Ghi kết quả tính định kỳ vào SQL Server: GoiY_TrongSo, course_similarity và KhoaHocKhuyenNghi."""
from datetime import datetime

from .config import COURSE_COL, USER_COL


def _replace_all(cursor, table, columns, rows):
    """Xóa toàn bộ bảng rồi ghi lại các dòng mới (gọi trong transaction của hàm cha)."""
    cursor.execute(f"DELETE FROM dbo.{table}")
    if rows:
        placeholders = ", ".join("?" * len(columns))
        cursor.executemany(f"INSERT INTO dbo.{table} ({', '.join(columns)}) VALUES ({placeholders})", rows)


def save_similarity_model(conn, similarity_table, signal_weights, signal_max):
    """Ghi ma trận tương đồng và bộ trọng số đã dùng để tính nó trong cùng một transaction.

    sp_GoiY_TinhGoiYChoNguoiDung đọc GoiY_TrongSo để tính điểm tương tác giống hệt lúc dựng ma trận.
    """
    now = datetime.now()
    weight_rows = [(signal, float(cfg["weight"]), float(signal_max[signal]), now)
                   for signal, cfg in signal_weights.items()]
    similarity_rows = [(r.course_id_a, r.course_id_b, float(r.similarity), now)
                       for r in similarity_table.itertuples(index=False)]
    cursor = conn.cursor()
    cursor.fast_executemany = True
    try:
        _replace_all(cursor, "GoiY_TrongSo", ["tinHieu", "trongSo", "giaTriChuanHoa", "capNhatLuc"], weight_rows)
        _replace_all(cursor, "course_similarity", ["course_id_a", "course_id_b", "similarity", "computed_at"],
                     similarity_rows)
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    return len(similarity_rows)


def save_user_recommendations(conn, recommendations):
    """Thay toàn bộ bảng KhoaHocKhuyenNghi (chỉ gợi ý lọc cộng tác; khóa phổ biến bù lúc đọc)."""
    now = datetime.now()
    rows = [(f"GY{getattr(r, USER_COL)}_{r.rank}", float(r.score), int(r.rank), now,
             int(getattr(r, USER_COL)), getattr(r, COURSE_COL))
            for r in recommendations.itertuples(index=False)]
    cursor = conn.cursor()
    # Không dùng fast_executemany: trigger kiểm tra dữ liệu chạy theo từng câu INSERT
    try:
        _replace_all(cursor, "KhoaHocKhuyenNghi",
                     ["maGoiY", "diemDuDoan", "thuTuUuTien", "ngayGoiY", "maHocVien", "maKhoaHoc"], rows)
        conn.commit()
    except Exception:
        conn.rollback()
        raise
    return len(rows)
