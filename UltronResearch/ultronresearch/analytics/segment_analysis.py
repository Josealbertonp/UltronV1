"""Archetype, score, and tier analysis."""

from __future__ import annotations

import pandas as pd

from ultronresearch.analytics.metrics import compute_group_metrics
from ultronresearch.constants import TIER_ORDER


def analyze_archetypes(df: pd.DataFrame, min_trades: int = 3) -> dict:
    col = "archetype"
    if df.empty or col not in df.columns:
        return {"by_archetype": []}

    rows = []
    for arch, grp in df.groupby(col, dropna=False):
        m = compute_group_metrics(grp)
        if m["trades"] < min_trades:
            continue
        m["archetype"] = str(arch)
        rows.append(m)

    return {"by_archetype": sorted(rows, key=lambda r: r.get("expectancy") or 0, reverse=True)}


def analyze_tiers(df: pd.DataFrame, min_trades: int = 3) -> dict:
    col = "tier"
    if df.empty or col not in df.columns:
        return {"by_tier": []}

    rows = []
    for tier, grp in df.groupby(col, dropna=False):
        m = compute_group_metrics(grp)
        if m["trades"] < min_trades:
            continue
        m["tier"] = str(tier)
        rows.append(m)

    order = {t: i for i, t in enumerate(TIER_ORDER)}
    rows.sort(key=lambda r: order.get(r["tier"], 99))
    return {"by_tier": rows}


def analyze_score(df: pd.DataFrame, step: int = 1, min_trades: int = 5) -> dict:
    if df.empty or "score" not in df.columns:
        return {"by_score": [], "optimal_min_score": None, "curves": {}}

    scores = pd.to_numeric(df["score"], errors="coerce").dropna().astype(int)
    if scores.empty:
        return {"by_score": [], "optimal_min_score": None, "curves": {}}

    lo, hi = int(scores.min()), int(scores.max())
    rows = []
    for s in range(lo, hi + 1, max(1, step)):
        grp = df[pd.to_numeric(df["score"], errors="coerce") >= s]
        m = compute_group_metrics(grp)
        if m["trades"] < min_trades:
            continue
        m["min_score"] = s
        rows.append(m)

    optimal = None
    if rows:
        valid = [r for r in rows if r.get("profit_factor") is not None]
        if valid:
            optimal = max(valid, key=lambda r: r["profit_factor"])

    curves = {
        "score_x_win_rate": {r["min_score"]: r["win_rate"] for r in rows},
        "score_x_profit_factor": {r["min_score"]: r["profit_factor"] for r in rows},
        "score_x_expectancy": {r["min_score"]: r["expectancy"] for r in rows},
        "score_x_mfe": {r["min_score"]: r["average_mfe"] for r in rows},
        "score_x_mae": {r["min_score"]: r["average_mae"] for r in rows},
    }

    by_score_point = []
    for s, grp in df.groupby(pd.to_numeric(df["score"], errors="coerce").astype("Int64")):
        if pd.isna(s):
            continue
        m = compute_group_metrics(grp)
        m["score"] = int(s)
        by_score_point.append(m)

    return {
        "by_score": sorted(by_score_point, key=lambda r: r["score"]),
        "by_min_score_threshold": rows,
        "optimal_min_score": optimal,
        "curves": curves,
    }
