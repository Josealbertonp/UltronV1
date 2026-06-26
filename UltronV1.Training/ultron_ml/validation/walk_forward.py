"""Walk-forward validation."""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
from sklearn.metrics import f1_score, precision_score, recall_score, roc_auc_score

from ultron_ml.data.loader import feature_matrix
from ultron_ml.metrics.calculator import trading_metrics


@dataclass
class WalkForwardWindow:
    window: int
    train_start: str
    train_end: str
    test_start: str
    test_end: str
    metrics: dict


@dataclass
class WalkForwardReport:
    windows: list[WalkForwardWindow] = field(default_factory=list)
    aggregate: dict = field(default_factory=dict)


def run_walk_forward(
    model_factory,
    df,
    train_bars: int = 200,
    test_bars: int = 50,
    step_bars: int = 50,
) -> WalkForwardReport:
    labeled = df[df["label"].notna()].copy().reset_index(drop=True)
    report = WalkForwardReport()
    if len(labeled) < train_bars + test_bars:
        return report

    X_all = feature_matrix(labeled)
    y_all = labeled["label"].astype(int).values
    times = labeled["timestampUtc"] if "timestampUtc" in labeled.columns else None

    window_idx = 0
    start = 0
    aucs, pfs = [], []

    while start + train_bars + test_bars <= len(labeled):
        train_slice = slice(start, start + train_bars)
        test_slice = slice(start + train_bars, start + train_bars + test_bars)

        model = model_factory()
        model.fit(X_all[train_slice], y_all[train_slice])
        proba = model.predict_proba(X_all[test_slice])[:, 1]
        pred = (proba >= 0.5).astype(int)
        y_test = y_all[test_slice]

        cls_metrics = {
            "auc": float(roc_auc_score(y_test, proba)) if len(np.unique(y_test)) > 1 else 0.0,
            "precision": float(precision_score(y_test, pred, zero_division=0)),
            "recall": float(recall_score(y_test, pred, zero_division=0)),
            "f1": float(f1_score(y_test, pred, zero_division=0)),
        }

        test_df = labeled.iloc[test_slice]
        trade_m = trading_metrics(test_df, pred)
        cls_metrics.update(trade_m)

        wf = WalkForwardWindow(
            window=window_idx,
            train_start=str(times.iloc[start]) if times is not None else str(start),
            train_end=str(times.iloc[start + train_bars - 1]) if times is not None else str(start + train_bars),
            test_start=str(times.iloc[start + train_bars]) if times is not None else str(start + train_bars),
            test_end=str(times.iloc[start + train_bars + test_bars - 1]) if times is not None else str(start + train_bars + test_bars),
            metrics=cls_metrics,
        )
        report.windows.append(wf)
        aucs.append(cls_metrics["auc"])
        pfs.append(cls_metrics.get("profitFactor", 0.0))
        window_idx += 1
        start += step_bars

    if aucs:
        report.aggregate = {
            "auc_mean": float(np.mean(aucs)),
            "profitFactor_mean": float(np.mean(pfs)),
            "windows_completed": len(aucs),
        }
    return report
