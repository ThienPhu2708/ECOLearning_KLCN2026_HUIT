"""Chạy mẫu 4 bước Item-based CF trên CSV và in kết quả.

    python run_demo.py                       # mặc định U0001, C2392
    python run_demo.py --user U0050 --course C4343 --k 5
"""
import argparse
import sys

import numpy as np
import pandas as pd

from recommender import CsvDataSource, ItemCF
from recommender.config import MIN_COMMON_USERS, SIGNAL_WEIGHTS, TOP_K

sys.stdout.reconfigure(encoding="utf-8")
pd.set_option("display.width", 220)
pd.set_option("display.max_colwidth", 45)

SHOW_COLUMNS = ["rank", "course_id", "score", "source", "because_course_id", "course_title", "level", "instructor"]


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--user", default="U0001")
    parser.add_argument("--course", default="C2392")
    parser.add_argument("--k", type=int, default=TOP_K)
    parser.add_argument("--min-common", type=int, default=MIN_COMMON_USERS)
    args = parser.parse_args()

    source = CsvDataSource()
    interactions, courses = source.load_interactions(), source.load_courses()
    model = ItemCF(min_common_users=args.min_common).fit(interactions, courses)

    print("== Trọng số điểm tương tác:", {k: v["weight"] for k, v in SIGNAL_WEIGHTS.items()})
    print(f"\n== Bước 1: ma trận user x course {model.matrix_.shape}, "
          f"độ phủ {(model.matrix_.to_numpy() > 0).mean():.2%}")
    print(model.matrix_.loc[[args.user]].T.query(f"`{args.user}` > 0").round(3).T)

    sim = model.similarity_
    positive = sim[np.triu_indices_from(sim, 1)]
    positive = positive[positive > 0]
    print(f"\n== Bước 2: ma trận tương đồng {sim.shape}, {len(positive)} cặp > 0 "
          f"(min_common_users={args.min_common}), median {np.median(positive):.3f}, max {positive.max():.3f}")

    print(f"\n== Bước 3-4: top-{args.k} khóa tương tự {args.course} "
          f"({courses.set_index('course_id').loc[args.course, 'course_title']})")
    print(model.similar_courses(args.course, args.k)[["rank", "course_id", "score", "course_title", "level"]]
          .to_string(index=False))

    print(f"\n== Khóa học dành cho {args.user} (top-{args.k})")
    print(model.recommend_for_user(args.user, args.k)[SHOW_COLUMNS].to_string(index=False))

    print(f"\n== Cold-start: user mới 'U_MOI' (top-{args.k})")
    print(model.recommend_for_user("U_MOI", args.k)[SHOW_COLUMNS].to_string(index=False))


if __name__ == "__main__":
    main()
