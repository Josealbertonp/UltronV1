"""ML Filter analysis — Phase 4B dashboard."""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
import pandas as pd

from ultronresearch.shadow.analysis import (
    _brier_score,
    _calibration_curve,
    _histogram,
    compute_trading_metrics,
    load_shadow_predictions,
)


def analyze_ml_filter(df: pd.DataFrame) -> dict:
    if df.empty:
        return {"status": "empty"}

    df = df.copy()
    df["confidence"] = pd.to_numeric(df.get("confidence"), errors="coerce")
    df["pnlTicks"] = pd.to_numeric(df.get("pnlTicks"), errors="coerce").fillna(0)
    df["win"] = df.get("win", df["pnlTicks"] > 0)
    if df["win"].dtype != bool:
        df["win"] = df["win"].astype(bool)

    passed = df[df.get("entryAllowed", True) == True]  # noqa: E712
    blocked = df[df.get("entryAllowed", True) == False]  # noqa: E712
    if "decision" in df.columns:
        blocked = df[df["decision"] == "BLOCK"]
        passed = df[df["decision"].isin(["PASS", "PASS_THROUGH"])]

    shadow_would_block = df[df.get("wouldBlockInActive", False) == True]  # noqa: E712

    return {
        "trade_count": len(df),
        "passed_count": len(passed),
        "blocked_count": len(blocked),
        "passed_metrics": _segment_metrics(passed),
        "blocked_metrics": _segment_metrics(blocked),
        "shadow_comparison": {
            "shadow_would_block": len(shadow_would_block),
            "all_metrics": _segment_metrics(df),
            "passed_metrics": _segment_metrics(passed),
            "hypothetical_blocked_metrics": _segment_metrics(blocked),
        },
        "false_blocks": _count_false_blocks(blocked),
        "true_blocks": _count_true_blocks(blocked),
        "filter_precision": _filter_precision(blocked),
        "confidence_distribution": _histogram(df["confidence"]),
        "calibration": _calibration_curve(df),
        "brier_score": _brier_score(df),
        "threshold_study": _threshold_study(df),
        "improvement": _improvement_vs_all(df, passed),
    }


def _segment_metrics(df: pd.DataFrame) -> dict:
    if df.empty:
        return {"trades": 0, "win_rate": 0, "profit_factor": None, "expectancy": 0}
    pnl = pd.to_numeric(df["pnlTicks"], errors="coerce").fillna(0)
    wins = pnl > 0
    gp = float(pnl[pnl > 0].sum())
    gl = float(abs(pnl[pnl < 0].sum()))
    pf = gp / gl if gl > 0 else None
    eq = np.cumsum(pnl.values)
    dd = eq - np.maximum.accumulate(eq) if len(eq) else np.array([0])
    return {
        "trades": len(df),
        "win_rate": round(float(wins.mean()), 4),
        "profit_factor": round(pf, 4) if pf is not None else None,
        "expectancy": round(float(pnl.mean()), 4),
        "max_drawdown_ticks": round(float(dd.min()), 4) if len(dd) else 0,
        "avg_mfe": round(float(pd.to_numeric(df.get("mfeTicks"), errors="coerce").mean()), 4),
        "avg_mae": round(float(pd.to_numeric(df.get("maeTicks"), errors="coerce").mean()), 4),
    }


def _count_false_blocks(blocked: pd.DataFrame) -> int:
    if blocked.empty or "win" not in blocked.columns:
        return 0
    return int((blocked["win"] == True).sum())  # noqa: E712


def _count_true_blocks(blocked: pd.DataFrame) -> int:
    if blocked.empty or "win" not in blocked.columns:
        return 0
    return int((blocked["win"] == False).sum())  # noqa: E712


def _filter_precision(blocked: pd.DataFrame) -> float | None:
    if blocked.empty:
        return None
    true_b = _count_true_blocks(blocked)
    false_b = _count_false_blocks(blocked)
    total = true_b + false_b
    return round(true_b / total, 4) if total else None


def _threshold_study(df: pd.DataFrame) -> dict:
    conf = pd.to_numeric(df["confidence"], errors="coerce").fillna(0).values
    pnl = pd.to_numeric(df["pnlTicks"], errors="coerce").fillna(0).values
    grid = [round(x * 0.05, 2) for x in range(8, 20)]
    return {"candidates": [compute_trading_metrics(conf, pnl, t) for t in grid]}


def _improvement_vs_all(df: pd.DataFrame, passed: pd.DataFrame) -> dict:
    all_m = _segment_metrics(df)
    pass_m = _segment_metrics(passed)
    return {
        "profit_factor_delta": _delta(all_m.get("profit_factor"), pass_m.get("profit_factor")),
        "win_rate_delta": _delta(all_m.get("win_rate"), pass_m.get("win_rate")),
        "expectancy_delta": _delta(all_m.get("expectancy"), pass_m.get("expectancy")),
        "drawdown_improvement_ticks": _delta(all_m.get("max_drawdown_ticks"), pass_m.get("max_drawdown_ticks")),
        "trades_removed": all_m["trades"] - pass_m["trades"],
    }


def _delta(before, after):
    if before is None or after is None:
        return None
    return round(float(after) - float(before), 4)


def generate_filter_dashboard(shadow_path: Path, output_dir: Path) -> Path:
    from ultronresearch.filter.dashboard import render_filter_dashboard

    df = load_shadow_predictions(shadow_path)
    analysis = analyze_ml_filter(df)
    return render_filter_dashboard(analysis, output_dir)
