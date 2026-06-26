"""Classification and trading metrics."""

from __future__ import annotations

import numpy as np
from sklearn.metrics import (
    accuracy_score,
    average_precision_score,
    brier_score_loss,
    confusion_matrix,
    f1_score,
    log_loss,
    precision_score,
    recall_score,
    roc_auc_score,
)


def compute_classification_metrics(y_true: np.ndarray, y_proba: np.ndarray, threshold: float = 0.5) -> dict:
    y_pred = (y_proba >= threshold).astype(int)
    metrics: dict = {
        "threshold": threshold,
        "accuracy": round(float(accuracy_score(y_true, y_pred)), 4),
        "precision": round(float(precision_score(y_true, y_pred, zero_division=0)), 4),
        "recall": round(float(recall_score(y_true, y_pred, zero_division=0)), 4),
        "f1": round(float(f1_score(y_true, y_pred, zero_division=0)), 4),
        "log_loss": None,
        "brier_score": round(float(brier_score_loss(y_true, y_proba)), 4),
        "roc_auc": None,
        "pr_auc": None,
        "calibration_error": round(_expected_calibration_error(y_true, y_proba), 4),
        "confusion_matrix": confusion_matrix(y_true, y_pred).tolist(),
    }

    try:
        metrics["roc_auc"] = round(float(roc_auc_score(y_true, y_proba)), 4)
    except ValueError:
        pass
    try:
        metrics["pr_auc"] = round(float(average_precision_score(y_true, y_proba)), 4)
    except ValueError:
        pass
    try:
        metrics["log_loss"] = round(float(log_loss(y_true, y_proba, labels=[0, 1])), 4)
    except ValueError:
        pass

    return metrics


def compute_trading_metrics(
    y_proba: np.ndarray,
    pnl: np.ndarray,
    threshold: float = 0.5,
) -> dict:
    mask = y_proba >= threshold
    selected_pnl = pnl[mask]
    n = int(mask.sum())
    if n == 0:
        return {
            "threshold": threshold, "trades": 0, "win_rate": 0.0,
            "profit_factor": None, "expectancy": 0.0, "max_drawdown_ticks": 0.0,
        }

    wins = selected_pnl > 0
    gross_profit = float(selected_pnl[selected_pnl > 0].sum())
    gross_loss = float(abs(selected_pnl[selected_pnl < 0].sum()))
    pf = gross_profit / gross_loss if gross_loss > 0 else None
    equity = np.cumsum(selected_pnl)
    dd = equity - np.maximum.accumulate(equity)

    return {
        "threshold": threshold,
        "trades": n,
        "win_rate": round(float(wins.mean()), 4),
        "profit_factor": round(pf, 4) if pf is not None else None,
        "expectancy": round(float(selected_pnl.mean()), 4),
        "max_drawdown_ticks": round(float(dd.min()), 4),
    }


def _expected_calibration_error(y_true: np.ndarray, y_proba: np.ndarray, n_bins: int = 10) -> float:
    bins = np.linspace(0, 1, n_bins + 1)
    ece = 0.0
    for i in range(n_bins):
        mask = (y_proba >= bins[i]) & (y_proba < bins[i + 1])
        if not mask.any():
            continue
        acc = y_true[mask].mean()
        conf = y_proba[mask].mean()
        ece += mask.mean() * abs(acc - conf)
    return float(ece)


def roc_curve_points(y_true: np.ndarray, y_proba: np.ndarray, n_points: int = 50) -> dict:
    from sklearn.metrics import roc_curve
    fpr, tpr, _ = roc_curve(y_true, y_proba)
    step = max(len(fpr) // n_points, 1)
    return {"fpr": fpr[::step].tolist(), "tpr": tpr[::step].tolist()}


def pr_curve_points(y_true: np.ndarray, y_proba: np.ndarray, n_points: int = 50) -> dict:
    from sklearn.metrics import precision_recall_curve
    prec, rec, _ = precision_recall_curve(y_true, y_proba)
    step = max(len(prec) // n_points, 1)
    return {"precision": prec[::step].tolist(), "recall": rec[::step].tolist()}
