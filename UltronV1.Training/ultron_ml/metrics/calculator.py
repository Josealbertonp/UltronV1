"""Classification and trading metrics."""

from __future__ import annotations

import numpy as np
import pandas as pd
from sklearn.metrics import (
    accuracy_score,
    f1_score,
    precision_score,
    recall_score,
    roc_auc_score,
)


def tier_distribution(df: pd.DataFrame) -> dict:
    if "tier" not in df.columns or df.empty:
        return {}
    counts = df["tier"].value_counts().to_dict()
    total = sum(counts.values())
    return {str(k): {"count": int(v), "pct": float(v / total)} for k, v in counts.items()}


def trading_metrics(df: pd.DataFrame, predictions: np.ndarray | None = None) -> dict:
    """Compute PF, expectancy, win rate from labeled rows with rMultiple."""
    work = df.copy()
    if predictions is not None:
        work = work.iloc[: len(predictions)].copy()
        work = work[predictions == 1]

    if "rMultiple" not in work.columns or work.empty:
        labeled = work[work["label"].notna()] if "label" in work.columns else work
        if labeled.empty:
            return _empty_trading_metrics()
        wins = labeled[labeled["label"] == 1]
        losses = labeled[labeled["label"] == 0]
        return _from_labels(wins, losses, len(labeled))

    r = work["rMultiple"].astype(float).values
    wins = r[r > 0]
    losses = r[r <= 0]
    n = len(r)
    if n == 0:
        return _empty_trading_metrics()

    gross_profit = float(wins.sum()) if len(wins) else 0.0
    gross_loss = float(abs(losses.sum())) if len(losses) else 0.0
    pf = gross_profit / gross_loss if gross_loss > 0 else (999.0 if gross_profit > 0 else 0.0)

    return {
        "tradeCount": n,
        "winRate": float(len(wins) / n),
        "profitFactor": float(pf),
        "expectancy": float(r.mean()),
        "avgWinR": float(wins.mean()) if len(wins) else 0.0,
        "avgLossR": float(losses.mean()) if len(losses) else 0.0,
    }


def _from_labels(wins, losses, n):
    gw = len(wins)
    gl = len(losses)
    pf = gw / gl if gl > 0 else (999.0 if gw > 0 else 0.0)
    return {
        "tradeCount": n,
        "winRate": float(gw / n) if n else 0.0,
        "profitFactor": float(pf),
        "expectancy": float((gw - gl) / n) if n else 0.0,
        "avgWinR": 1.0,
        "avgLossR": -1.0,
    }


def _empty_trading_metrics():
    return {
        "tradeCount": 0,
        "winRate": 0.0,
        "profitFactor": 0.0,
        "expectancy": 0.0,
        "avgWinR": 0.0,
        "avgLossR": 0.0,
    }


def full_metrics(y_true, y_proba, y_pred, df: pd.DataFrame) -> dict:
    metrics = {
        "auc": float(roc_auc_score(y_true, y_proba)) if len(np.unique(y_true)) > 1 else 0.0,
        "precision": float(precision_score(y_true, y_pred, zero_division=0)),
        "recall": float(recall_score(y_true, y_pred, zero_division=0)),
        "f1": float(f1_score(y_true, y_pred, zero_division=0)),
        "accuracy": float(accuracy_score(y_true, y_pred)),
    }
    metrics.update(trading_metrics(df))
    metrics["tierDistribution"] = tier_distribution(df)
    return metrics
