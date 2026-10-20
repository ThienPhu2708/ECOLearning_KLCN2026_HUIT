"""Job tính định kỳ: dựng ma trận tương đồng và gợi ý, lưu vào CSDL (hoặc file CSV để xem).

    python build_recommendations.py --source csv          # đọc CSV, ghi output/*.csv
    python build_recommendations.py --source sql          # đọc SQL Server, ghi GoiY_TrongSo + course_similarity
                                                          #   + KhoaHocKhuyenNghi
    python build_recommendations.py --source sql --dry-run  # đọc SQL Server, chỉ ghi CSV để kiểm tra
"""
import argparse
import sys
import time

from recommender import CsvDataSource, ItemCF, SqlServerDataSource
from recommender.config import MIN_COMMON_USERS, OUTPUT_DIR, SIMILARITY_TOP_N, USER_RECS_TOP_N
from recommender.sql_store import save_similarity_model, save_user_recommendations

sys.stdout.reconfigure(encoding="utf-8")


def build(source, min_common_users, similarity_top_n, user_top_n):
    """Nạp dữ liệu, tính ma trận, trả về (model, bảng tương đồng, bảng gợi ý theo user)."""
    interactions = source.load_interactions()
    model = ItemCF(min_common_users=min_common_users).fit(interactions, source.load_courses())
    similarity = model.similarity_table(top_n=similarity_top_n)
    recommendations = model.recommend_all_users(
        user_top_n,
        allowed_courses=source.load_recommendable_courses(),
        allowed_users=source.load_recommendable_users(),
        exclude_by_user=source.load_course_owners(),
        seen_by_user=source.load_consumed_courses(),
    )
    return model, similarity, recommendations


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", choices=["csv", "sql"], default="csv")
    parser.add_argument("--dry-run", action="store_true", help="Không ghi CSDL, chỉ xuất CSV")
    parser.add_argument("--min-common", type=int, default=MIN_COMMON_USERS)
    parser.add_argument("--similarity-top-n", type=int, default=SIMILARITY_TOP_N)
    parser.add_argument("--user-top-n", type=int, default=USER_RECS_TOP_N)
    args = parser.parse_args()

    started = time.perf_counter()
    source = SqlServerDataSource() if args.source == "sql" else CsvDataSource()
    model, similarity, recommendations = build(source, args.min_common, args.similarity_top_n, args.user_top_n)

    print(f"Tương tác: {len(model.interactions_)} | user: {len(model.user_ids_)} | khóa học: {len(model.course_ids_)}")
    print(f"course_similarity: {len(similarity)} dòng | gợi ý theo user: {len(recommendations)} dòng "
          f"cho {recommendations['user_id'].nunique()} user")

    if args.source == "sql" and not args.dry_run:
        with source.connect() as conn:
            n_sim = save_similarity_model(conn, similarity, model.signal_weights, model.signal_max_)
            n_rec = save_user_recommendations(conn, recommendations)
        print(f"Đã ghi CSDL: GoiY_TrongSo = {len(model.signal_weights)} dòng, course_similarity = {n_sim} dòng, "
              f"KhoaHocKhuyenNghi = {n_rec} dòng")
    else:
        OUTPUT_DIR.mkdir(exist_ok=True)
        similarity.to_csv(OUTPUT_DIR / "course_similarity.csv", index=False, encoding="utf-8-sig")
        recommendations.to_csv(OUTPUT_DIR / "user_recommendations.csv", index=False, encoding="utf-8-sig")
        print(f"Đã ghi {OUTPUT_DIR / 'course_similarity.csv'} và {OUTPUT_DIR / 'user_recommendations.csv'}")
    print(f"Thời gian: {time.perf_counter() - started:.2f}s")


if __name__ == "__main__":
    main()
