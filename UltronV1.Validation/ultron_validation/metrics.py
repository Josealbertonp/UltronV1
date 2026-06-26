"""Trading and comparison metrics."""

from __future__ import annotations

import numpy as np
import pandas as pd


def max_drawdown(equity: np.ndarray) -> float:
    if len(equity) == 0:
        return 0.0
    peak = np.maximum.accumulate(equity)
    dd = equity - peak
    return float(dd.min())


def equity_curve(returns: np.ndarray) -> np.ndarray:
    return np.cumsum(returns)


def trading_metrics_from_returns(returns: np.ndarray) -> dict:
    if len(returns) == 0:
        return _empty()
    wins = returns[returns > 0]
    losses = returns[returns <= 0]
    gross_profit = float(wins.sum()) if len(wins) else 0.0
    gross_loss = float(abs(losses.sum())) if len(losses) else 0.0
    pf = gross_profit / gross_loss if gross_loss > 0 else (999.0 if gross_profit > 0 else 0.0)
    eq = equity_curve(returns)
    return {
        "tradeCount": int(len(returns)),
        "winRate": float(len(wins) / len(returns)),
        "profitFactor": float(pf),
        "expectancy": float(returns.mean()),
        "totalReturn": float(returns.sum()),
        "maxDrawdown": max_drawdown(eq),
        "avgWin": float(wins.mean()) if len(wins) else 0.0,
        "avgLoss": float(losses.mean()) if len(losses) else 0.0,
    }


def trading_metrics_from_df(df: pd.DataFrame, return_col: str = "rMultiple") -> dict:
    if df.empty:
        return _empty()
    if return_col in df.columns and df[return_col].notna().any():
        return trading_metrics_from_returns(df[return_col].astype(float).values)
    if "profitTicks" in df.columns:
        return trading_metrics_from_returns(df["profitTicks"].astype(float).values)
    if "label" in df.columns:
        labels = df["label"].astype(int).values
        return trading_metrics_from_returns(np.where(labels == 1, 1.0, -1.0))
    return _empty()


def _empty() -> dict:
    return {
        "tradeCount": 0,
        "winRate": 0.0,
        "profitFactor": 0.0,
        "expectancy": 0.0,
        "totalReturn": 0.0,
        "maxDrawdown": 0.0,
        "avgWin": 0.0,
        "avgLoss": 0.0,
    }


def overlap_summary(df: pd.DataFrame, match_col: str = "matchType") -> dict:
    if df.empty or match_col not in df.columns:
        return {}
    counts = df[match_col].value_counts().to_dict()
    total = sum(counts.values())
    return {
        str(k): {"count": int(v), "pct": float(v / total) if total else 0.0}
        for k, v in counts.items()
    }
