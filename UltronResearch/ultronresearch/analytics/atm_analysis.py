"""ATM profile comparison."""

from __future__ import annotations

import pandas as pd

from ultronresearch.analytics.metrics import compute_group_metrics


def detect_atm_column(df: pd.DataFrame, candidates: list[str] | None = None) -> str | None:
    candidates = candidates or ["atm_template", "atm_profile", "atm_name", "atm_id"]
    for col in candidates:
        if col in df.columns and df[col].notna().any():
            return col
    for col in df.columns:
        if col.lower().startswith("atm_") and df[col].notna().any():
            return col
    return None


def analyze_atm(df: pd.DataFrame, atm_columns: list[str] | None = None) -> dict:
    col = detect_atm_column(df, atm_columns)
    if col is None:
        return {
            "by_atm": [],
            "note": (
                "No ATM column in dataset. Export atm_template/atm_profile from sessions "
                "or label profiles ATM-A / ATM-B / ATM-C for comparison."
            ),
        }

    rows = []
    for atm, grp in df.groupby(col, dropna=False):
        m = compute_group_metrics(grp)
        m["atm"] = str(atm)
        rows.append(m)

    rows.sort(key=lambda r: r.get("profit_factor") or 0, reverse=True)
    return {"atm_column": col, "by_atm": rows}
