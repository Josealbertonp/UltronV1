"""Core performance metrics."""

from __future__ import annotations

import numpy as np
import pandas as pd

from ultronresearch.constants import PNL_COLUMN, WIN_COLUMN


def _pnl_series(df: pd.DataFrame) -> pd.Series:
    if PNL_COLUMN in df.columns:
        return pd.to_numeric(df[PNL_COLUMN], errors="coerce").fillna(0.0)
    return pd.Series(0.0, index=df.index)


def _wins(df: pd.DataFrame) -> pd.Series:
    if WIN_COLUMN in df.columns:
        return df[WIN_COLUMN].astype(bool)
    return _pnl_series(df) > 0


def compute_group_metrics(df: pd.DataFrame) -> dict:
    """Aggregate metrics for a trade subset."""
    if df.empty:
        return _empty_metrics()

    pnl = _pnl_series(df)
    wins = _wins(df)
    n = len(df)
    win_n = int(wins.sum())
    loss_n = n - win_n
    gross_profit = float(pnl[pnl > 0].sum())
    gross_loss = float(abs(pnl[pnl < 0].sum()))
    avg_win = float(pnl[wins].mean()) if win_n else 0.0
    avg_loss = float(pnl[~wins].mean()) if loss_n else 0.0
    pf = gross_profit / gross_loss if gross_loss > 0 else (float("inf") if gross_profit > 0 else 0.0)
    wr = win_n / n if n else 0.0
    expectancy = float(pnl.mean()) if n else 0.0

    equity = pnl.cumsum()
    peak = equity.cummax()
    dd = equity - peak
    max_dd = float(dd.min()) if len(dd) else 0.0
    total_pnl = float(pnl.sum())
    recovery = total_pnl / abs(max_dd) if max_dd < 0 else (float("inf") if total_pnl > 0 else 0.0)

    mfe = pd.to_numeric(df.get("mfe_ticks"), errors="coerce")
    mae = pd.to_numeric(df.get("mae_ticks"), errors="coerce")

    sharpe = _sharpe_simple(pnl)
    sortino = _sortino_simple(pnl)

    payoff = avg_win / abs(avg_loss) if avg_loss != 0 else (float("inf") if avg_win > 0 else 0.0)

    return {
        "trades": n,
        "wins": win_n,
        "losses": loss_n,
        "win_rate": round(wr, 4),
        "profit_factor": round(pf, 4) if pf != float("inf") else None,
        "expectancy": round(expectancy, 4),
        "average_win": round(avg_win, 4),
        "average_loss": round(avg_loss, 4),
        "payoff": round(payoff, 4) if payoff != float("inf") else None,
        "average_mfe": round(float(mfe.mean()), 4) if mfe.notna().any() else None,
        "average_mae": round(float(mae.mean()), 4) if mae.notna().any() else None,
        "total_pnl_ticks": round(total_pnl, 4),
        "max_drawdown_ticks": round(max_dd, 4),
        "recovery_factor": round(recovery, 4) if recovery != float("inf") else None,
        "sharpe_simple": round(sharpe, 4),
        "sortino_simple": round(sortino, 4),
    }


def compute_general_stats(df: pd.DataFrame) -> dict:
    return compute_group_metrics(df)


def _sharpe_simple(pnl: pd.Series) -> float:
    if len(pnl) < 2 or pnl.std() == 0:
        return 0.0
    return float(pnl.mean() / pnl.std() * np.sqrt(len(pnl)))


def _sortino_simple(pnl: pd.Series) -> float:
    downside = pnl[pnl < 0]
    if len(pnl) < 2 or len(downside) == 0 or downside.std() == 0:
        return 0.0
    return float(pnl.mean() / downside.std() * np.sqrt(len(pnl)))


def _empty_metrics() -> dict:
    return {
        "trades": 0, "wins": 0, "losses": 0, "win_rate": 0.0,
        "profit_factor": None, "expectancy": 0.0, "average_win": 0.0,
        "average_loss": 0.0, "payoff": None, "average_mfe": None,
        "average_mae": None, "total_pnl_ticks": 0.0,
        "max_drawdown_ticks": 0.0, "recovery_factor": None,
        "sharpe_simple": 0.0, "sortino_simple": 0.0,
    }


def equity_curve(df: pd.DataFrame) -> pd.DataFrame:
    pnl = _pnl_series(df)
    eq = pnl.cumsum()
    peak = eq.cummax()
    return pd.DataFrame({
        "trade_index": range(len(eq)),
        "pnl": pnl.values,
        "equity": eq.values,
        "drawdown": (eq - peak).values,
    })
