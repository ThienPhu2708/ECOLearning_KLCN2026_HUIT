"""Item-based Collaborative Filtering (memory-based, không huấn luyện mô hình).

Bước 1: ma trận tương tác user x course (ô = điểm tương tác, chưa tương tác = 0)
Bước 2: ma trận độ tương đồng course x course bằng cosine similarity
Bước 3: sắp xếp độ tương đồng / điểm dự đoán từ lớn đến bé
Bước 4: lấy top-K
"""
import numpy as np
import pandas as pd

from .config import COURSE_COL, MIN_COMMON_USERS, SCORE_COL, SIGNAL_WEIGHTS, USER_COL
from .scoring import compute_interaction_score, resolve_signal_max

SOURCE_CF = "cf"
SOURCE_POPULAR = "popular"


# --------------------------------------------------------------------------- #
# Các hàm thuần (không phụ thuộc trạng thái), dễ kiểm thử riêng
# --------------------------------------------------------------------------- #
def build_interaction_matrix(scored_interactions):
    """Bước 1: pivot thành ma trận user x course, ô chưa tương tác = 0."""
    return scored_interactions.pivot_table(
        index=USER_COL, columns=COURSE_COL, values=SCORE_COL, aggfunc="max", fill_value=0.0
    ).sort_index().sort_index(axis=1)


def cosine_item_similarity(matrix, min_common_users=MIN_COMMON_USERS):
    """Bước 2: cosine giữa các cột (khóa học). Đường chéo = 0; cặp có < min_common_users user chung = 0."""
    dot = matrix.T @ matrix
    norms = np.sqrt(np.diag(dot))
    denom = np.outer(norms, norms)
    similarity = np.divide(dot, denom, out=np.zeros_like(dot), where=denom > 0)

    binary = (matrix > 0).astype(float)
    common_users = binary.T @ binary
    similarity[common_users < min_common_users] = 0.0
    np.fill_diagonal(similarity, 0.0)
    return similarity


def top_k_indices(values, k):
    """Bước 3 + 4: chỉ số các phần tử > 0 sắp giảm dần, lấy k phần tử đầu (hòa thì theo thứ tự cột)."""
    order = np.argsort(-values, kind="stable")
    order = order[values[order] > 0]
    return order[:k]


# --------------------------------------------------------------------------- #
# Mô hình
# --------------------------------------------------------------------------- #
class ItemCF:
    """Gợi ý khóa học bằng Item-based CF. fit() chỉ tính ma trận, không có quá trình huấn luyện."""

    def __init__(self, min_common_users=MIN_COMMON_USERS, signal_weights=SIGNAL_WEIGHTS):
        self.min_common_users = min_common_users
        self.signal_weights = signal_weights

    # ---------------- Tính toán (chạy định kỳ) ----------------
    def fit(self, interactions, courses=None):
        """Tính điểm tương tác, ma trận user x course, ma trận tương đồng và bảng độ phổ biến."""
        self.signal_max_ = resolve_signal_max(interactions, self.signal_weights)
        scored = compute_interaction_score(interactions, self.signal_weights, self.signal_max_)
        self.interactions_ = scored
        self.matrix_ = build_interaction_matrix(scored)
        self._matrix_values = self.matrix_.to_numpy()
        self.user_ids_ = self.matrix_.index.to_numpy()
        self.course_ids_ = self.matrix_.columns.to_numpy()
        self._user_pos = {u: i for i, u in enumerate(self.user_ids_)}
        self._course_pos = {c: j for j, c in enumerate(self.course_ids_)}
        self.similarity_ = cosine_item_similarity(self._matrix_values, self.min_common_users)
        # Mọi khóa đã tương tác, kể cả ô có điểm 0 (vd. mới đăng ký, chưa học) -> không gợi ý lại
        self._seen_by_user = scored.groupby(USER_COL)[COURSE_COL].agg(set).to_dict()
        self.courses_ = courses.set_index(COURSE_COL) if courses is not None else None
        self.popularity_ = self._compute_popularity(scored, self.courses_)
        return self

    @staticmethod
    def _compute_popularity(scored, courses=None):
        """Độ phổ biến: số người tương tác giảm dần, hòa thì rating trung bình (bỏ ô chưa đánh giá) giảm dần.

        Có metadata thì gồm cả khóa chưa ai tương tác (0 người) để khóa mới vẫn được bù vào danh sách.
        """
        rated = scored.assign(rating=scored["rating"].where(scored["rating"] > 0))
        stats = rated.groupby(COURSE_COL).agg(n_users=(USER_COL, "nunique"), avg_rating=("rating", "mean"))
        if courses is not None:
            stats = stats.reindex(stats.index.union(courses.index))
        stats = stats.fillna({"n_users": 0, "avg_rating": 0}).sort_index()
        return stats.sort_values(["n_users", "avg_rating"], ascending=[False, False], kind="stable")

    def similarity_frame(self):
        """Ma trận tương đồng dạng DataFrame (course x course) để xem / xuất file."""
        return pd.DataFrame(self.similarity_, index=self.course_ids_, columns=self.course_ids_)

    def similarity_table(self, top_n=None):
        """Bảng dài (course_id_a, course_id_b, similarity) chỉ gồm cặp > 0, sắp giảm dần theo từng khóa a."""
        rows = []
        for a, course_a in enumerate(self.course_ids_):
            for b in top_k_indices(self.similarity_[a], top_n or len(self.course_ids_)):
                rows.append((course_a, self.course_ids_[b], round(float(self.similarity_[a, b]), 6)))
        return pd.DataFrame(rows, columns=["course_id_a", "course_id_b", "similarity"])

    # ---------------- Truy vấn ----------------
    def similar_courses(self, course_id, k=10, allowed_courses=None, with_metadata=True):
        """Top-K khóa học tương tự một khóa (mục "Khóa học tương tự")."""
        if course_id not in self._course_pos:
            return self._to_frame([], [], [], [], with_metadata)
        values = self._mask_not_allowed(self.similarity_[self._course_pos[course_id]].copy(), allowed_courses)
        idx = top_k_indices(values, k)
        ids = self.course_ids_[idx].tolist()
        return self._to_frame(ids, values[idx].tolist(), [SOURCE_CF] * len(ids), [None] * len(ids), with_metadata)

    def recommend_for_user(self, user_id, k=10, fill_with_popular=True, allowed_courses=None,
                           exclude_courses=(), with_metadata=True, seen_courses=None):
        """Top-K khóa học cho user (mục "Khóa học dành cho bạn").

        Điểm dự đoán khóa j = tổng sim(i, j) * score(user, i) trên các khóa i user đã tương tác.
        Bỏ khóa đã học và khóa có điểm 0. User chưa có tương tác (cold-start) hoặc không đủ K
        khóa thì bù bằng khóa phổ biến nhất (nếu fill_with_popular=True).

        seen_courses: tập khóa không gợi ý lại. Mặc định (None) = mọi khóa đã tương tác, dùng khi đánh giá.
        Job chạy thật truyền vào khóa đã sở hữu / đã học (đăng ký, tiến độ, đánh giá) để khóa chỉ mới
        xem qua vẫn được gợi ý lại, giống stored procedure (fn_GoiY_KhoaHocDuocGoiY, migration 006).
        """
        ids, scores, sources, because = [], [], [], []
        if seen_courses is None:
            seen_courses = self._seen_by_user.get(user_id, set())
        seen = set(exclude_courses) | set(seen_courses)

        if user_id in self._user_pos:
            user_scores = self._matrix_values[self._user_pos[user_id]]
            interacted = np.flatnonzero(user_scores > 0)

            # contributions[i, j] = score(user, i) * sim(i, j)
            contributions = user_scores[interacted, None] * self.similarity_[interacted]
            predicted = contributions.sum(axis=0)
            predicted = self._mask_not_allowed(predicted, allowed_courses, seen)

            for j in top_k_indices(predicted, k):
                ids.append(self.course_ids_[j])
                scores.append(round(float(predicted[j]), 6))
                sources.append(SOURCE_CF)
                because.append(self.course_ids_[interacted[np.argmax(contributions[:, j])]])

        if fill_with_popular and len(ids) < k:
            for course_id in self.popular_courses(k, exclude=seen | set(ids), allowed_courses=allowed_courses,
                                                  with_metadata=False)[COURSE_COL]:
                if len(ids) >= k:
                    break
                ids.append(course_id)
                scores.append(0.0)
                sources.append(SOURCE_POPULAR)
                because.append(None)

        return self._to_frame(ids, scores, sources, because, with_metadata)

    def popular_courses(self, k=10, exclude=(), allowed_courses=None, with_metadata=True):
        """Top-K khóa phổ biến nhất (cold-start, baseline). Cột score = số người đã tương tác."""
        exclude = set(exclude)
        ids = [c for c in self.popularity_.index
               if c not in exclude and (allowed_courses is None or c in allowed_courses)][:k]
        counts = self.popularity_.loc[ids, "n_users"].astype(float).tolist()
        return self._to_frame(ids, counts, [SOURCE_POPULAR] * len(ids), [None] * len(ids), with_metadata)

    def recommend_all_users(self, k, allowed_courses=None, allowed_users=None, exclude_by_user=None,
                            seen_by_user=None):
        """Gợi ý lọc cộng tác (không bù phổ biến) cho mọi user, dùng cho job ghi vào CSDL.

        seen_by_user: {user: tập khóa không gợi ý lại}; None = mọi khóa đã tương tác (xem recommend_for_user).
        """
        exclude_by_user = exclude_by_user or {}
        frames = []
        for user_id in self.user_ids_:
            if allowed_users is not None and user_id not in allowed_users:
                continue
            seen = None if seen_by_user is None else seen_by_user.get(user_id, set())
            recs = self.recommend_for_user(user_id, k, fill_with_popular=False, allowed_courses=allowed_courses,
                                           exclude_courses=exclude_by_user.get(user_id, ()), with_metadata=False,
                                           seen_courses=seen)
            if not recs.empty:
                frames.append(recs.assign(**{USER_COL: user_id}))
        if not frames:
            return pd.DataFrame(columns=[USER_COL, "rank", COURSE_COL, "score", "source", "because_course_id"])
        return pd.concat(frames, ignore_index=True)[[USER_COL, "rank", COURSE_COL, "score", "source",
                                                     "because_course_id"]]

    # ---------------- Hỗ trợ ----------------
    def _mask_not_allowed(self, values, allowed_courses=None, exclude_courses=()):
        """Đặt điểm = 0 cho khóa không được phép gợi ý (chưa xuất bản, của chính giáo viên...)."""
        if allowed_courses is not None:
            values[~np.isin(self.course_ids_, list(allowed_courses))] = 0.0
        for course_id in exclude_courses:
            if course_id in self._course_pos:
                values[self._course_pos[course_id]] = 0.0
        return values

    def _to_frame(self, ids, scores, sources, because, with_metadata):
        frame = pd.DataFrame({
            "rank": range(1, len(ids) + 1),
            COURSE_COL: ids,
            "score": scores,
            "source": sources,
            "because_course_id": pd.Series(because, dtype=object),
        })
        if with_metadata and self.courses_ is not None:
            frame = frame.join(self.courses_, on=COURSE_COL)
        return frame
