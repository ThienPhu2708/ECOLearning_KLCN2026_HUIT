"""Đánh giá offline Precision/Recall/HitRate@K, so sánh với baseline phổ biến.

    python evaluate.py                          # K = 5, 10; 5 seed; min_common_users = 2
    python evaluate.py --min-common 1 2 3       # so sánh nhiều ngưỡng user chung
"""
import argparse
import sys

import pandas as pd

from recommender import CsvDataSource
from recommender.config import MIN_COMMON_USERS, OUTPUT_DIR
from recommender.evaluation import evaluate

sys.stdout.reconfigure(encoding="utf-8")
pd.set_option("display.width", 200)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--k", type=int, nargs="+", default=[5, 10])
    parser.add_argument("--seeds", type=int, nargs="+", default=[1, 2, 3, 4, 5])
    parser.add_argument("--test-ratio", type=float, default=0.2)
    parser.add_argument("--min-common", type=int, nargs="+", default=[MIN_COMMON_USERS])
    args = parser.parse_args()

    interactions = CsvDataSource().load_interactions()
    OUTPUT_DIR.mkdir(exist_ok=True)

    for min_common in args.min_common:
        summary, runs = evaluate(interactions, tuple(args.k), tuple(args.seeds), args.test_ratio, min_common)
        print(f"\n===== min_common_users = {min_common} | {len(args.seeds)} seed | test_ratio = {args.test_ratio}")
        for k, table in summary.groupby("k", sort=False):
            print(f"\n--- K = {k}")
            view = table.assign(
                **{
                    f"Precision@{k}": table.precision.map("{:.4f}".format) + " ± " + table.precision_std.map("{:.4f}".format),
                    f"Recall@{k}": table.recall.map("{:.4f}".format) + " ± " + table.recall_std.map("{:.4f}".format),
                    f"HitRate@{k}": table.hit_rate.map("{:.4f}".format) + " ± " + table.hit_rate_std.map("{:.4f}".format),
                    "Độ dài TB": table.avg_list_len.map("{:.2f}".format),
                    "Độ phủ danh mục": table.coverage.map("{:.1%}".format),
                })
            print(view[["method", f"Precision@{k}", f"Recall@{k}", f"HitRate@{k}", "Độ dài TB", "Độ phủ danh mục"]]
                  .to_string(index=False))
        summary.to_csv(OUTPUT_DIR / f"danh_gia_min_common_{min_common}.csv", index=False, encoding="utf-8-sig")
        runs.to_csv(OUTPUT_DIR / f"danh_gia_chi_tiet_min_common_{min_common}.csv", index=False, encoding="utf-8-sig")
    print(f"\nĐã lưu kết quả vào {OUTPUT_DIR}")


if __name__ == "__main__":
    main()
