"""Threshold optimization — research only, does not modify Core."""

from __future__ import annotations

import pandas as pd

from ultronresearch.analytics.metrics import compute_group_metrics
from ultronresearch.constants import TIER_ORDER


def optimize_score_threshold(
    df: pd.DataFrame,
    min_trades: int = 10,
) -> dict:
    if df.empty or "score" not in df.columns:
        return {"candidates": [], "best": None}

    scores = sorted(pd.to_numeric(df["score"], errors="coerce").dropna().unique())
    candidates = []
    for s in scores:
        grp = df[pd.to_numeric(df["score"], errors="coerce") >= s]
        m = compute_group_metrics(grp)
        if m["trades"] < min_trades:
            continue
        m["min_score"] = int(s)
        candidates.append(m)

    best_pf = max(
        (c for c in candidates if c.get("profit_factor") is not None),
        key=lambda c: c["profit_factor"],
        default=None,
    )
    best_exp = max(candidates, key=lambda c: c["expectancy"], default=None) if candidates else None

    return {
        "candidates": candidates,
        "best_by_profit_factor": best_pf,
        "best_by_expectancy": best_exp,
    }


def optimize_tier_threshold(df: pd.DataFrame, min_trades: int = 10) -> dict:
    if df.empty or "tier" not in df.columns:
        return {"candidates": [], "best": None}

    tiers_present = [t for t in TIER_ORDER if t in set(df["tier"].astype(str))]
    candidates = []
    for i, min_tier in enumerate(tiers_present):
        allowed = set(tiers_present[: i + 1])
        grp = df[df["tier"].astype(str).isin(allowed)]
        m = compute_group_metrics(grp)
        if m["trades"] < min_trades:
            continue
        m["min_tier"] = min_tier
        m["allowed_tiers"] = list(allowed)
        candidates.append(m)

    best = max(
        (c for c in candidates if c.get("profit_factor") is not None),
        key=lambda c: c["profit_factor"],
        default=None,
    )
    return {"candidates": candidates, "best_by_profit_factor": best}


def optimize_confidence_placeholder(df: pd.DataFrame) -> dict:
    """Placeholder for future ML confidence threshold — no model in Phase 2."""
    return {
        "status": "placeholder",
        "note": "ML confidence optimization requires trained model from UltronML Phase 3.",
        "suggested_thresholds": [0.50, 0.55, 0.60, 0.65, 0.70, 0.75, 0.80],
        "trade_count": len(df),
    }


def run_threshold_optimizer(df: pd.DataFrame, min_trades: int = 10) -> dict:
    return {
        "score": optimize_score_threshold(df, min_trades),
        "tier": optimize_tier_threshold(df, min_trades),
        "confidence": optimize_confidence_placeholder(df),
    }
