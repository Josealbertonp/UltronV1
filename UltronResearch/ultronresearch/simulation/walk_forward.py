"""Walk-forward split structure — no ML training in Phase 2."""

from __future__ import annotations

from dataclasses import dataclass

import pandas as pd

from ultronresearch.analytics.metrics import compute_group_metrics


@dataclass
class WalkForwardSplit:
    train: pd.DataFrame
    validation: pd.DataFrame
    test: pd.DataFrame
    train_ratio: float
    val_ratio: float
    test_ratio: float


def create_walk_forward_split(
    df: pd.DataFrame,
    train_ratio: float = 0.60,
    val_ratio: float = 0.20,
    test_ratio: float = 0.20,
    time_col: str = "timestamp_utc",
) -> WalkForwardSplit:
    if abs(train_ratio + val_ratio + test_ratio - 1.0) > 1e-6:
        raise ValueError("Ratios must sum to 1.0")

    data = df.copy()
    if time_col in data.columns:
        data = data.sort_values(time_col).reset_index(drop=True)
    n = len(data)
    t1 = int(n * train_ratio)
    t2 = int(n * (train_ratio + val_ratio))

    return WalkForwardSplit(
        train=data.iloc[:t1],
        validation=data.iloc[t1:t2],
        test=data.iloc[t2:],
        train_ratio=train_ratio,
        val_ratio=val_ratio,
        test_ratio=test_ratio,
    )


def analyze_walk_forward(split: WalkForwardSplit) -> dict:
    return {
        "structure": {
            "train_ratio": split.train_ratio,
            "val_ratio": split.val_ratio,
            "test_ratio": split.test_ratio,
            "train_rows": len(split.train),
            "validation_rows": len(split.validation),
            "test_rows": len(split.test),
        },
        "train_metrics": compute_group_metrics(split.train),
        "validation_metrics": compute_group_metrics(split.validation),
        "test_metrics": compute_group_metrics(split.test),
        "ml_training": {
            "status": "not_implemented",
            "phase": "RC2-Phase2",
            "note": "Splits ready for future offline ML — no model trained here.",
        },
    }
