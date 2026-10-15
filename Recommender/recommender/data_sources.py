"""Nạp dữ liệu tương tác và metadata khóa học. Tách riêng khỏi thuật toán.

Mọi nguồn trả về cùng cấu trúc:
  load_interactions()          -> user_id, course_id, rating, views, clicks, likes, progress_percent
  load_courses()               -> course_id, course_title, level, instructor, course_url, ...
  load_recommendable_courses() -> tập course_id được phép gợi ý (None = tất cả)
  load_recommendable_users()   -> tập user_id được nhận gợi ý (None = tất cả)
  load_course_owners()         -> {user_id: {course_id do user đó dạy}} để không gợi ý khóa của chính mình
"""
import pandas as pd

from .config import COURSE_COL, CSV_COURSES, CSV_INTERACTIONS, SIGNAL_WEIGHTS, SQL_CONNECTION_STRING, USER_COL

INTERACTION_COLUMNS = [USER_COL, COURSE_COL, *SIGNAL_WEIGHTS.keys()]


def validate_interactions(df):
    """Kiểm tra đủ cột, không thiếu giá trị, không trùng cặp (user, course)."""
    missing = [c for c in INTERACTION_COLUMNS if c not in df.columns]
    if missing:
        raise ValueError(f"Dữ liệu tương tác thiếu cột: {missing}")
    if df[INTERACTION_COLUMNS].isna().any().any():
        raise ValueError("Dữ liệu tương tác có giá trị rỗng")
    duplicated = df.duplicated([USER_COL, COURSE_COL]).sum()
    if duplicated:
        raise ValueError(f"Có {duplicated} cặp (user_id, course_id) bị trùng")
    return df


class CsvDataSource:
    """Nguồn dữ liệu thử nghiệm từ file CSV."""

    def __init__(self, interactions_path=CSV_INTERACTIONS, courses_path=CSV_COURSES):
        self.interactions_path = interactions_path
        self.courses_path = courses_path

    def load_interactions(self):
        df = pd.read_csv(self.interactions_path, encoding="utf-8-sig")
        return validate_interactions(df)

    def load_courses(self):
        return pd.read_csv(self.courses_path, encoding="utf-8-sig")

    def load_recommendable_courses(self):
        return None

    def load_recommendable_users(self):
        return None

    def load_course_owners(self):
        return {}

    def load_consumed_courses(self):
        # CSV không phân biệt đăng ký / chỉ xem -> giữ mặc định: không gợi ý lại mọi khóa đã tương tác
        return None


class SqlServerDataSource:
    """Nguồn dữ liệu thật từ SQL Server (view dbo.vw_GoiY_TuongTac, migration 005)."""

    PUBLISHED_STATUS = "Đã xuất bản"  # trigger của KhoaHocKhuyenNghi chỉ chấp nhận trạng thái này

    def __init__(self, connection_string=SQL_CONNECTION_STRING):
        self.connection_string = connection_string

    def connect(self):
        import pyodbc  # chỉ cần khi dùng SQL Server
        return pyodbc.connect(self.connection_string)

    def _query(self, sql, *params):
        with self.connect() as conn:
            cursor = conn.cursor()
            cursor.execute(sql, *params)
            columns = [d[0] for d in cursor.description]
            return pd.DataFrame.from_records([tuple(r) for r in cursor.fetchall()], columns=columns)

    def load_interactions(self):
        columns = ", ".join(INTERACTION_COLUMNS)
        df = self._query(f"SELECT {columns} FROM dbo.vw_GoiY_TuongTac")
        for col in SIGNAL_WEIGHTS:
            df[col] = pd.to_numeric(df[col])
        return validate_interactions(df)

    def load_courses(self):
        return self._query("""
            SELECT kh.maKhoaHoc  AS course_id,
                   kh.tenKhoaHoc AS course_title,
                   kh.capDo      AS level,
                   nd.hoTen      AS instructor,
                   dm.tenDanhMuc AS category_keyword,
                   kh.trangThai  AS status,
                   '/Course/Details/' + kh.maKhoaHoc AS course_url
            FROM dbo.KhoaHoc kh
            JOIN dbo.NguoiDung nd ON nd.maNguoiDung = kh.maGiaoVien
            LEFT JOIN dbo.DanhMuc dm ON dm.maDanhMuc = kh.maDanhMuc""")

    def load_recommendable_courses(self):
        df = self._query("SELECT maKhoaHoc FROM dbo.KhoaHoc WHERE trangThai = ?", self.PUBLISHED_STATUS)
        return set(df["maKhoaHoc"])

    def load_recommendable_users(self):
        df = self._query("""
            SELECT maNguoiDung FROM dbo.NguoiDung
            WHERE vaiTro IN (N'HocVien', N'GiaoVien') AND ISNULL(trangThai, 1) = 1""")
        return set(df["maNguoiDung"])

    def load_course_owners(self):
        df = self._query("SELECT maGiaoVien, maKhoaHoc FROM dbo.KhoaHoc")
        return df.groupby("maGiaoVien")["maKhoaHoc"].apply(set).to_dict()

    def load_consumed_courses(self):
        """Khóa người dùng đã sở hữu / đã học (không gợi ý lại); khóa chỉ mới xem qua vẫn được gợi ý.
        Cùng quy tắc với dbo.fn_GoiY_KhoaHocDuocGoiY (migration 006)."""
        df = self._query("""
            SELECT maHocVien, maKhoaHoc FROM dbo.DangKyKhoaHoc
            UNION SELECT maHocVien, maKhoaHoc FROM dbo.TienDoHocTap
            UNION SELECT maHocVien, maKhoaHoc FROM dbo.DanhGiaKhoaHoc""")
        return df.groupby("maHocVien")["maKhoaHoc"].apply(set).to_dict()
