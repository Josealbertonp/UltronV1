"""Monte Carlo trade-sequence simulation."""

from __future__ import annotations

import numpy as np
import pandas as pd

from ultronresearch.analytics.metrics import _pnl_series


def run_monte_carlo(
    df: pd.DataFrame,
    n_simulations: int = 5000,
    seed: int = 42,
) -> dict:
    pnl = _pnl_series(df).values
    if len(pnl) == 0:
        return {"simulations": 0, "note": "No trades"}

    rng = np.random.default_rng(seed)
    n = len(pnl)
    final_equity = np.zeros(n_simulations)
    max_drawdowns = np.zeros(n_simulations)
    sample_curves: list[list[float]] = []

    for i in range(n_simulations):
        shuffled = rng.permutation(pnl)
        equity = np.cumsum(shuffled)
        peak = np.maximum.accumulate(equity)
        dd = equity - peak
        final_equity[i] = equity[-1]
        max_drawdowns[i] = dd.min()
        if i < 50:
            sample_curves.append(equity.tolist())

    return {
        "simulations": n_simulations,
        "trade_count": n,
        "final_equity": {
            "mean": round(float(final_equity.mean()), 4),
            "median": round(float(np.median(final_equity)), 4),
            "p5": round(float(np.percentile(final_equity, 5)), 4),
            "p95": round(float(np.percentile(final_equity, 95)), 4),
            "best": round(float(final_equity.max()), 4),
            "worst": round(float(final_equity.min()), 4),
        },
        "max_drawdown": {
            "mean": round(float(max_drawdowns.mean()), 4),
            "median": round(float(np.median(max_drawdowns)), 4),
            "p5": round(float(np.percentile(max_drawdowns, 5)), 4),
            "p95": round(float(np.percentile(max_drawdowns, 95)), 4),
            "worst": round(float(max_drawdowns.min()), 4),
        },
        "confidence_interval_95": [
            round(float(np.percentile(final_equity, 2.5)), 4),
            round(float(np.percentile(final_equity, 97.5)), 4),
        ],
        "sample_curves": sample_curves,
        "average_curve": np.cumsum(pnl).tolist(),
    }
