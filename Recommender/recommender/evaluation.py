"""Đánh giá offline: giấu ~20% khóa của mỗi user, so sánh Item-based CF với baseline phổ biến."""
import numpy as np
import pandas as pd

from .config import COURSE_COL, USER_COL
from .item_cf import ItemCF


def split_per_user(interactions, test_ratio=0.2, seed=42):
    """Với mỗi user, giấu round(test_ratio * số khóa) khóa (ít nhất 1) làm tập kiểm tra."""
    rng = np.random.default_rng(seed)
    test_index = []
    for _, group in interactions.groupby(USER_COL):
        if len(group) < 2:
            continue  # cần giữ lại ít nhất 1 khóa để có lịch sử
        n_test = min(max(1, round(test_ratio * len(group))), len(group) - 1)
        test_index.extend(rng.choice(group.index.to_numpy(), size=n_test, replace=False))
    test = interactions.loc[test_index]
    train = interactions.drop(index=test_index)
    return train, test


def metrics_at_k(recommended, relevant, k):
    """Precision@K, Recall@K, HitRate@K cho một user."""
    hits = len(set(recommended[:k]) & relevant)
    return hits / k, hits / len(relevant), float(hits > 0)


def _recommenders(model, rng):
    """Các phương pháp cần so sánh: hàm (user_id, k) -> danh sách course_id."""
    all_courses = model.course_ids_

    def cf_only(user_id, k):
        return model.recommend_for_user(user_id, k, fill_with_popular=False, with_metadata=False)[COURSE_COL].tolist()

    def cf_fill(user_id, k):
        return model.recommend_for_user(user_id, k, fill_with_popular=True, with_metadata=False)[COURSE_COL].tolist()

    def popular(user_id, k):
        seen = model.interactions_.loc[model.interactions_[USER_COL] == user_id, COURSE_COL]
        return model.popular_courses(k, exclude=set(seen), with_metadata=False)[COURSE_COL].tolist()

    def random(user_id, k):
        seen = set(model.interactions_.loc[model.interactions_[USER_COL] == user_id, COURSE_COL])
        candidates = [c for c in all_courses if c not in seen]
        return rng.choice(candidates, size=min(k, len(candidates)), replace=False).tolist()

    return {
        "ItemCF": cf_only,
        "ItemCF + bù phổ biến": cf_fill,
        "Phổ biến (baseline)": popular,
        "Ngẫu nhiên (tham khảo)": random,
    }


def evaluate_once(interactions, ks=(5, 10), seed=42, test_ratio=0.2, min_common_users=2):
    """Một lần chia train/test: trả về DataFrame (method, k, precision, recall, hit_rate, ...)."""
    train, test = split_per_user(interactions, test_ratio, seed)
    model = ItemCF(min_common_users=min_common_users).fit(train)
    relevant_by_user = test.groupby(USER_COL)[COURSE_COL].apply(set)
    rng = np.random.default_rng(seed)

    rows = []
    for method, recommend in _recommenders(model, rng).items():
        max_k = max(ks)
        recs_by_user = {u: recommend(u, max_k) for u in relevant_by_user.index}
        for k in ks:
            per_user = np.array([metrics_at_k(recs_by_user[u], rel, k) for u, rel in relevant_by_user.items()])
            rows.append({
                "method": method, "k": k,
                "precision": per_user[:, 0].mean(),
                "recall": per_user[:, 1].mean(),
                "hit_rate": per_user[:, 2].mean(),
                "avg_list_len": np.mean([min(k, len(r)) for r in recs_by_user.values()]),
                "coverage": len({c for r in recs_by_user.values() for c in r[:k]}) / len(model.course_ids_),
            })
    return pd.DataFrame(rows)


def evaluate(interactions, ks=(5, 10), seeds=(1, 2, 3, 4, 5), test_ratio=0.2, min_common_users=2):
    """Lặp nhiều seed, trả về trung bình và độ lệch chuẩn của từng chỉ số."""
    runs = pd.concat([evaluate_once(interactions, ks, s, test_ratio, min_common_users).assign(seed=s)
                      for s in seeds], ignore_index=True)
    summary = runs.groupby(["k", "method"], sort=False).agg(
        precision=("precision", "mean"), precision_std=("precision", "std"),
        recall=("recall", "mean"), recall_std=("recall", "std"),
        hit_rate=("hit_rate", "mean"), hit_rate_std=("hit_rate", "std"),
        avg_list_len=("avg_list_len", "mean"), coverage=("coverage", "mean"),
    ).reset_index()
    return summary, runs
