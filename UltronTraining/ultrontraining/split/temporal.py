"""Temporal data splits — no random shuffle."""

from __future__ import annotations

from dataclasses import dataclass

import pandas as pd

from ultrontraining.data.features import FeatureMatrix


@dataclass
class TemporalSplit:
    train: FeatureMatrix
    validation: FeatureMatrix
    test: FeatureMatrix
    train_indices: pd.Index
    val_indices: pd.Index
    test_indices: pd.Index


def temporal_split(
    fm: FeatureMatrix,
    train_ratio: float = 0.60,
    val_ratio: float = 0.20,
    test_ratio: float = 0.20,
) -> TemporalSplit:
    if abs(train_ratio + val_ratio + test_ratio - 1.0) > 1e-6:
        raise ValueError("Ratios must sum to 1.0")

    n = len(fm.X)
    t1 = int(n * train_ratio)
    t2 = int(n * (train_ratio + val_ratio))

    def _slice(start: int, end: int) -> FeatureMatrix:
        idx = fm.X.index[start:end]
        return FeatureMatrix(
            X=fm.X.loc[idx],
            y=fm.y.loc[idx],
            feature_names=fm.feature_names,
            pnl=fm.pnl.loc[idx] if fm.pnl is not None else None,
        )

    return TemporalSplit(
        train=_slice(0, t1),
        validation=_slice(t1, t2),
        test=_slice(t2, n),
        train_indices=fm.X.index[:t1],
        val_indices=fm.X.index[t1:t2],
        test_indices=fm.X.index[t2:n],
    )
