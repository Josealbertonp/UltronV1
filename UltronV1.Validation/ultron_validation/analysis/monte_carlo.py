"""Monte Carlo simulation on trade sequences."""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
import pandas as pd

from ultron_validation.metrics import max_drawdown, trading_metrics_from_returns


@dataclass
class MonteCarloReport:
    simulations: int
    sample_size: int
    pf: dict = field(default_factory=dict)
    max_drawdown: dict = field(default_factory=dict)
    total_return: dict = field(default_factory=dict)
    win_rate: dict = field(default_factory=dict)
    probability_profitable: float = 0.0


def _extract_returns(df: pd.DataFrame, col: str | None = None) -> np.ndarray:
    if df.empty:
        return np.array([])
    if col and col in df.columns:
        return df[col].astype(float).dropna().values
    if "rMultiple" in df.columns and df["rMultiple"].notna().any():
        return df["rMultiple"].astype(float).dropna().values
    if "profitTicks" in df.columns:
        return df["profitTicks"].astype(float).dropna().values
    if "label" in df.columns:
        return np.where(df["label"].astype(int).values == 1, 1.0, -1.0)
    return np.array([])


def run_monte_carlo(
    returns: np.ndarray,
    *,
    simulations: int = 2000,
    seed: int = 42,
) -> MonteCarloReport:
    returns = returns[np.isfinite(returns)]
    report = MonteCarloReport(simulations=simulations, sample_size=len(returns))
    if len(returns) < 5:
        return report

    rng = np.random.default_rng(seed)
    pfs, dds, totals, wrs = [], [], [], []

    for _ in range(simulations):
        sample = rng.choice(returns, size=len(returns), replace=True)
        m = trading_metrics_from_returns(sample)
        pfs.append(m["profitFactor"])
        totals.append(m["totalReturn"])
        wrs.append(m["winRate"])
        dds.append(max_drawdown(np.cumsum(sample)))

    def stats(arr):
        return {
            "p5": float(np.percentile(arr, 5)),
            "p50": float(np.percentile(arr, 50)),
            "p95": float(np.percentile(arr, 95)),
            "mean": float(np.mean(arr)),
        }

    report.pf = stats(pfs)
    report.max_drawdown = stats(dds)
    report.total_return = stats(totals)
    report.win_rate = stats(wrs)
    report.probability_profitable = float(sum(1 for t in totals if t > 0) / simulations)
    return report


def monte_carlo_comparison(
    ultron_df: pd.DataFrame,
    nr_trades: pd.DataFrame,
    simulations: int = 2000,
) -> dict:
    u_ret = _extract_returns(ultron_df[ultron_df["decision"] == "Accepted"])
    n_ret = _extract_returns(nr_trades, "profitTicks")
    return {
        "ultron": run_monte_carlo(u_ret, simulations=simulations).__dict__,
        "nightRanger": run_monte_carlo(n_ret, simulations=simulations).__dict__,
    }
