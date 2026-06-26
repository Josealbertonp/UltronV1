"""Time-of-day analysis."""

from __future__ import annotations

import pandas as pd

from ultronresearch.analytics.metrics import compute_group_metrics
from ultronresearch.constants import DAY_NAMES


def analyze_hours(df: pd.DataFrame, min_trades: int = 5) -> dict:
    if df.empty or "hour" not in df.columns:
        return {"by_hour": [], "best_hour": None, "worst_hour": None, "heatmaps": {}}

    rows = []
    for hour, grp in df.groupby("hour", dropna=True):
        m = compute_group_metrics(grp)
        if m["trades"] < min_trades:
            continue
        m["hour"] = int(hour)
        rows.append(m)

    by_hour = sorted(rows, key=lambda r: r["hour"])
    best = max(by_hour, key=lambda r: r.get("expectancy") or -1e9) if by_hour else None
    worst = min(by_hour, key=lambda r: r.get("expectancy") or 1e9) if by_hour else None

    heatmaps = {
        "hour_x_win_rate": {r["hour"]: r["win_rate"] for r in by_hour},
        "hour_x_profit_factor": {r["hour"]: r["profit_factor"] for r in by_hour},
        "hour_x_expectancy": {r["hour"]: r["expectancy"] for r in by_hour},
    }

    return {
        "by_hour": by_hour,
        "best_hour": best,
        "worst_hour": worst,
        "heatmaps": heatmaps,
    }


def analyze_days(df: pd.DataFrame, min_trades: int = 5) -> dict:
    if df.empty or "day_of_week" not in df.columns:
        return {"by_day": []}

    rows = []
    for dow, grp in df.groupby("day_of_week", dropna=True):
        m = compute_group_metrics(grp)
        if m["trades"] < min_trades:
            continue
        name = DAY_NAMES[int(dow)] if 0 <= int(dow) < len(DAY_NAMES) else str(dow)
        m["day_of_week"] = int(dow)
        m["day_name"] = name
        rows.append(m)

    return {"by_day": sorted(rows, key=lambda r: r["day_of_week"])}
