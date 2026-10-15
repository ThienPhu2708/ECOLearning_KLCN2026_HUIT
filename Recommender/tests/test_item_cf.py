"""Kiểm thử Item-based CF trên ví dụ nhỏ tính tay được.   python -m unittest discover tests"""
import unittest

import numpy as np
import pandas as pd

from recommender.item_cf import ItemCF, cosine_item_similarity, top_k_indices
from recommender.scoring import compute_interaction_score

# score = rating/5 (chỉ dùng một tín hiệu để dễ tính tay)
RATING_ONLY = {"rating": {"weight": 1.0, "max": 5}}


def make_interactions(rows):
    return pd.DataFrame(rows, columns=["user_id", "course_id", "rating"])


class CosineTest(unittest.TestCase):
    def test_cosine_and_min_common_users(self):
        # 3 user x 3 khóa; A và B có 2 user chung, A và C chỉ 1 user chung
        matrix = np.array([[1.0, 1.0, 0.0],
                           [1.0, 0.5, 0.0],
                           [0.0, 0.0, 1.0]])
        matrix[0, 2] = 0.5  # user 0 cũng học C -> A,C có 1 user chung
        sim = cosine_item_similarity(matrix, min_common_users=2)
        expected_ab = (1 * 1 + 1 * 0.5) / (np.sqrt(2) * np.sqrt(1.25))
        self.assertAlmostEqual(sim[0, 1], expected_ab)
        self.assertAlmostEqual(sim[1, 0], expected_ab)
        self.assertEqual(sim[0, 2], 0.0)  # bị loại vì < 2 user chung
        self.assertTrue(np.all(np.diag(sim) == 0))

        sim_1 = cosine_item_similarity(matrix, min_common_users=1)
        self.assertGreater(sim_1[0, 2], 0.0)

    def test_top_k_drops_zero_and_sorts_desc(self):
        self.assertEqual(top_k_indices(np.array([0.2, 0.0, 0.9, 0.5]), 5).tolist(), [2, 3, 0])


class ScoringTest(unittest.TestCase):
    def test_formula_from_spec(self):
        row = pd.DataFrame([{"rating": 3.8, "views": 34, "clicks": 53, "likes": 1, "progress_percent": 97},
                            {"rating": 1.0, "views": 100, "clicks": 63, "likes": 0, "progress_percent": 0}])
        score = compute_interaction_score(row)["score"].iloc[0]
        expected = 0.4 * 3.8 / 5 + 0.2 * 34 / 100 + 0.1 * 53 / 63 + 0.1 * 1 + 0.2 * 97 / 100
        self.assertAlmostEqual(score, expected, places=5)

    def test_weights_must_sum_to_one(self):
        with self.assertRaises(ValueError):
            compute_interaction_score(pd.DataFrame({"rating": [5]}), {"rating": {"weight": 0.5, "max": 5}})


class RecommendTest(unittest.TestCase):
    def setUp(self):
        self.model = ItemCF(min_common_users=2, signal_weights=RATING_ONLY).fit(make_interactions([
            ("u1", "A", 5), ("u1", "B", 5),
            ("u2", "A", 5), ("u2", "B", 4), ("u2", "C", 5),
            ("u3", "B", 5), ("u3", "C", 5),
            ("u4", "A", 5),
        ]))

    def test_predicted_score_is_weighted_sum(self):
        recs = self.model.recommend_for_user("u4", k=5, fill_with_popular=False, with_metadata=False)
        self.assertEqual(recs["course_id"].tolist(), ["B"])  # C chỉ có 1 user chung với A -> sim = 0
        sim_ab = self.model.similarity_frame().loc["A", "B"]
        self.assertAlmostEqual(recs["score"].iloc[0], round(1.0 * sim_ab, 6))
        self.assertEqual(recs["because_course_id"].iloc[0], "A")

    def test_excludes_seen_courses(self):
        recs = self.model.recommend_for_user("u1", k=5, with_metadata=False)
        self.assertNotIn("A", recs["course_id"].tolist())
        self.assertNotIn("B", recs["course_id"].tolist())

    def test_zero_score_interaction_is_not_recommended_again(self):
        # u5 đã đăng ký B nhưng chưa có tín hiệu nào (điểm 0) -> không được gợi ý lại B
        model = ItemCF(min_common_users=2, signal_weights=RATING_ONLY).fit(make_interactions([
            ("u1", "A", 5), ("u1", "B", 5), ("u2", "A", 5), ("u2", "B", 4), ("u5", "A", 5), ("u5", "B", 0),
        ]))
        recs = model.recommend_for_user("u5", k=5, with_metadata=False)
        self.assertNotIn("B", recs["course_id"].tolist())

    def test_seen_courses_override_allows_viewed_course(self):
        # Job thật: u1 chỉ sở hữu A (B mới xem qua) -> B vẫn được gợi ý; mặc định (đánh giá) thì không
        recs = self.model.recommend_for_user("u1", k=5, fill_with_popular=False, with_metadata=False,
                                             seen_courses={"A"})
        self.assertIn("B", recs["course_id"].tolist())
        self.assertNotIn("A", recs["course_id"].tolist())
        default = self.model.recommend_for_user("u1", k=5, fill_with_popular=False, with_metadata=False)
        self.assertNotIn("B", default["course_id"].tolist())

    def test_cold_start_returns_popular(self):
        recs = self.model.recommend_for_user("new_user", k=2, with_metadata=False)
        self.assertEqual(recs["course_id"].tolist(), ["A", "B"])  # cùng 3 user, A có rating TB cao hơn B
        self.assertTrue((recs["source"] == "popular").all())

    def test_allowed_and_excluded_courses(self):
        recs = self.model.recommend_for_user("u4", k=5, fill_with_popular=False, allowed_courses={"A", "C"},
                                             with_metadata=False)
        self.assertTrue(recs.empty)


if __name__ == "__main__":
    unittest.main()
