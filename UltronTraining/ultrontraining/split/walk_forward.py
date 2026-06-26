"""Walk-forward validation — rolling and expanding windows."""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
import pandas as pd

from ultrontraining.data.features import FeatureMatrix
from ultrontraining.metrics.classification import compute_classification_metrics


@dataclass
class WalkForwardFold:
    fold: int
    train_start: int
    train_end: int
    test_start: int
    test_end: int
    metrics: dict


@dataclass
class WalkForwardResult:
    mode: str
    folds: list[WalkForwardFold] = field(default_factory=list)
    aggregate: dict = field(default_factory=dict)


def walk_forward_expanding(
    fm: FeatureMatrix,
    model_factory,
    n_folds: int = 3,
    min_train: int = 40,
) -> WalkForwardResult:
    return _run_walk_forward(fm, model_factory, n_folds, min_train, mode="expanding", rolling_size=None)


def walk_forward_rolling(
    fm: FeatureMatrix,
    model_factory,
    window_size: int,
    n_folds: int = 3,
    min_train: int = 40,
) -> WalkForwardResult:
    return _run_walk_forward(
        fm, model_factory, n_folds, min_train, mode="rolling", rolling_size=window_size
    )


def _run_walk_forward(
    fm: FeatureMatrix,
    model_factory,
    n_folds: int,
    min_train: int,
    mode: str,
    rolling_size: int | None,
) -> WalkForwardResult:
    n = len(fm.X)
    test_size = max(int((n - min_train) / max(n_folds, 1)), 10)
    folds: list[WalkForwardFold] = []
    metrics_list: list[dict] = []

    for fold in range(n_folds):
        test_end = min_train + (fold + 1) * test_size
        test_start = test_end - test_size
        if test_end > n:
            break

        if mode == "expanding":
            train_start = 0
        else:
            train_start = max(0, test_start - (rolling_size or min_train))

        train_end = test_start
        if train_end - train_start < min_train // 2:
            continue

        tr_idx = fm.X.index[train_start:train_end]
        te_idx = fm.X.index[test_start:test_end]

        X_tr, y_tr = fm.X.loc[tr_idx], fm.y.loc[tr_idx]
        X_te, y_te = fm.X.loc[te_idx], fm.y.loc[te_idx]

        model = model_factory()
        model.fit(X_tr.values, y_tr.values)
        proba = model.predict_proba(X_te.values)[:, 1]
        m = compute_classification_metrics(y_te.values, proba)
        folds.append(WalkForwardFold(
            fold=fold,
            train_start=train_start,
            train_end=train_end,
            test_start=test_start,
            test_end=test_end,
            metrics=m,
        ))
        metrics_list.append(m)

    aggregate = {}
    if metrics_list:
        for key in metrics_list[0]:
            vals = [m[key] for m in metrics_list if m.get(key) is not None]
            if vals and isinstance(vals[0], (int, float)):
                aggregate[key] = round(float(np.mean(vals)), 4)

    return WalkForwardResult(mode=mode, folds=folds, aggregate=aggregate)
