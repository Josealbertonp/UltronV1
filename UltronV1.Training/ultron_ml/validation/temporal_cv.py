"""Temporal cross-validation (no shuffle)."""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
import pandas as pd
from sklearn.metrics import (
    accuracy_score,
    f1_score,
    precision_score,
    recall_score,
    roc_auc_score,
)

from ultron_ml.data.loader import feature_matrix


@dataclass
class FoldResult:
    fold: int
    train_size: int
    test_size: int
    metrics: dict


@dataclass
class TemporalCVReport:
    n_splits: int
    folds: list[FoldResult] = field(default_factory=list)
    aggregate: dict = field(default_factory=dict)


def temporal_train_test_splits(df: pd.DataFrame, n_splits: int = 5, test_ratio: float = 0.15):
    """Expanding window temporal splits."""
    n = len(df)
    if n < n_splits + 10:
        n_splits = max(2, n // 20)

    min_train = int(n * 0.4)
    test_size = max(int(n * test_ratio), 10)

    for fold in range(n_splits):
        train_end = min_train + fold * (test_size // max(n_splits, 1))
        test_start = train_end
        test_end = min(test_start + test_size, n)
        if test_end - test_start < 5 or train_end < 20:
            continue
        train_idx = np.arange(0, train_end)
        test_idx = np.arange(test_start, test_end)
        yield fold, train_idx, test_idx


def run_temporal_cv(model_factory, df: pd.DataFrame, n_splits: int = 5) -> TemporalCVReport:
    labeled = df[df["label"].notna()].copy()
    if labeled.empty:
        return TemporalCVReport(n_splits=n_splits)

    X_all = feature_matrix(labeled)
    y_all = labeled["label"].astype(int).values

    report = TemporalCVReport(n_splits=n_splits)
    aucs, f1s, precisions, recalls = [], [], [], []

    for fold, train_idx, test_idx in temporal_train_test_splits(labeled, n_splits):
        model = model_factory()
        model.fit(X_all[train_idx], y_all[train_idx])
        proba = model.predict_proba(X_all[test_idx])[:, 1]
        pred = (proba >= 0.5).astype(int)
        y_test = y_all[test_idx]

        fold_metrics = {
            "auc": float(roc_auc_score(y_test, proba)) if len(np.unique(y_test)) > 1 else 0.0,
            "precision": float(precision_score(y_test, pred, zero_division=0)),
            "recall": float(recall_score(y_test, pred, zero_division=0)),
            "f1": float(f1_score(y_test, pred, zero_division=0)),
            "accuracy": float(accuracy_score(y_test, pred)),
        }
        report.folds.append(FoldResult(fold, len(train_idx), len(test_idx), fold_metrics))
        aucs.append(fold_metrics["auc"])
        f1s.append(fold_metrics["f1"])
        precisions.append(fold_metrics["precision"])
        recalls.append(fold_metrics["recall"])

    if aucs:
        report.aggregate = {
            "auc_mean": float(np.mean(aucs)),
            "auc_std": float(np.std(aucs)),
            "f1_mean": float(np.mean(f1s)),
            "precision_mean": float(np.mean(precisions)),
            "recall_mean": float(np.mean(recalls)),
            "folds_completed": len(aucs),
        }
    return report
