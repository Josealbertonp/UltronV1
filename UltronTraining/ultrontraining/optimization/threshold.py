"""Confidence threshold optimization."""

from __future__ import annotations

import numpy as np

from ultrontraining.metrics.classification import compute_trading_metrics


def optimize_thresholds(
    y_proba: np.ndarray,
    pnl: np.ndarray,
    grid: list[float] | None = None,
) -> dict:
    grid = grid or [round(x * 0.05, 2) for x in range(10, 20)]
    candidates = []
    for t in grid:
        m = compute_trading_metrics(y_proba, pnl, threshold=t)
        candidates.append(m)

    valid_pf = [c for c in candidates if c.get("profit_factor") is not None and c["trades"] >= 5]
    best_pf = max(valid_pf, key=lambda c: c["profit_factor"], default=None)
    best_exp = max(
        [c for c in candidates if c["trades"] >= 5],
        key=lambda c: c["expectancy"],
        default=None,
    )

    return {
        "candidates": candidates,
        "best_by_profit_factor": best_pf,
        "best_by_expectancy": best_exp,
        "recommended": best_pf or best_exp,
    }
