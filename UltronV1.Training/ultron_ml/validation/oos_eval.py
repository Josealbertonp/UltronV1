"""Out-of-sample holdout evaluation."""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
from sklearn.metrics import (
    accuracy_score,
    f1_score,
    precision_score,
    recall_score,
    roc_auc_score,
)

from ultron_ml.data.loader import feature_matrix
from ultron_ml.metrics.calculator import tier_distribution, trading_metrics


@dataclass
class OOSReport:
    train_size: int
    test_size: int
    metrics: dict


def temporal_oos_split(df, oos_ratio: float = 0.2):
    labeled = df[df["label"].notna()].copy()
    if "timestampUtc" in labeled.columns:
        labeled = labeled.sort_values("timestampUtc")
    n = len(labeled)
    split = int(n * (1.0 - oos_ratio))
    return labeled.iloc[:split], labeled.iloc[split:]


def evaluate_oos(model, train_df, test_df) -> OOSReport:
    X_train = feature_matrix(train_df)
    y_train = train_df["label"].astype(int).values
    X_test = feature_matrix(test_df)
    y_test = test_df["label"].astype(int).values

    model.fit(X_train, y_train)
    proba = model.predict_proba(X_test)[:, 1]
    pred = (proba >= 0.5).astype(int)

    metrics = {
        "auc": float(roc_auc_score(y_test, proba)) if len(np.unique(y_test)) > 1 else 0.0,
        "precision": float(precision_score(y_test, pred, zero_division=0)),
        "recall": float(recall_score(y_test, pred, zero_division=0)),
        "f1": float(f1_score(y_test, pred, zero_division=0)),
        "accuracy": float(accuracy_score(y_test, pred)),
    }
    metrics.update(trading_metrics(test_df, pred))
    metrics["tierDistribution"] = tier_distribution(test_df)
    metrics["tierDistributionOOS"] = tier_distribution(test_df[pred == 1]) if pred.sum() > 0 else {}

    return OOSReport(len(train_df), len(test_df), metrics)
