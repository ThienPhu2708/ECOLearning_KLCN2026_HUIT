"""Tính điểm tương tác tổng hợp cho từng cặp (user, course)."""
import numpy as np
import pandas as pd

from .config import SCORE_COL, SIGNAL_WEIGHTS


def validate_signal_weights(signal_weights):
    """Kiểm tra tổng trọng số bằng 1 để điểm nằm trong [0, 1]."""
    total = sum(cfg["weight"] for cfg in signal_weights.values())
    if not np.isclose(total, 1.0):
        raise ValueError(f"Tổng trọng số tín hiệu phải bằng 1, hiện là {total:.4f}")


def resolve_signal_max(interactions, signal_weights=SIGNAL_WEIGHTS):
    """Trả về giá trị chuẩn hóa của từng tín hiệu; max=None thì lấy max thực tế trong dữ liệu."""
    resolved = {}
    for col, cfg in signal_weights.items():
        if col not in interactions.columns:
            raise ValueError(f"Thiếu cột tín hiệu '{col}' trong dữ liệu tương tác")
        max_value = cfg["max"] if cfg["max"] is not None else interactions[col].max()
        resolved[col] = float(max_value) if max_value and max_value > 0 else 1.0
    return resolved


def compute_interaction_score(interactions, signal_weights=SIGNAL_WEIGHTS, max_values=None):
    """Thêm cột 'score' = tổng weight * (giá trị / max), mỗi số hạng được kẹp trong [0, 1]."""
    validate_signal_weights(signal_weights)
    if max_values is None:
        max_values = resolve_signal_max(interactions, signal_weights)

    score = pd.Series(0.0, index=interactions.index)
    for col, cfg in signal_weights.items():
        normalized = (interactions[col].astype(float) / max_values[col]).clip(0, 1)
        score += cfg["weight"] * normalized

    result = interactions.copy()
    result[SCORE_COL] = score.round(6)
    return result
