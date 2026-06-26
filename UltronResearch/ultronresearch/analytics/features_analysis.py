"""Feature statistical analysis."""

from __future__ import annotations

import numpy as np
import pandas as pd
from sklearn.feature_selection import mutual_info_classif

from ultronresearch.constants import LABEL_COLUMNS


def feature_columns(df: pd.DataFrame) -> list[str]:
    return [c for c in df.columns if c.startswith("feat_")]


def analyze_features(df: pd.DataFrame, target_col: str = "is_win") -> dict:
    cols = feature_columns(df)
    if not cols or df.empty:
        return {"features": [], "high_correlation_pairs": [], "near_constant": []}

    X = df[cols].apply(pd.to_numeric, errors="coerce")
    y = df[target_col].astype(int) if target_col in df.columns else (df["realized_pnl_ticks"] > 0).astype(int)

    variances = X.var(skipna=True)
    mi = mutual_info_classif(X.fillna(0), y, random_state=42, discrete_features=False)

    corr = X.corr()
    high_pairs = []
    for i, a in enumerate(cols):
        for b in cols[i + 1:]:
            val = corr.loc[a, b]
            if pd.notna(val) and abs(val) >= 0.85:
                high_pairs.append({"feature_a": a, "feature_b": b, "correlation": round(float(val), 4)})

    near_constant = [c for c in cols if variances.get(c, 0) < 1e-8]

    duplicate_groups = _find_duplicate_columns(X, cols)

    rows = []
    for i, col in enumerate(cols):
        series = X[col]
        rows.append({
            "feature": col,
            "variance": round(float(variances.get(col, 0)), 6),
            "mutual_information": round(float(mi[i]), 6),
            "mean": round(float(series.mean()), 4) if series.notna().any() else None,
            "std": round(float(series.std()), 4) if series.notna().any() else None,
            "min": round(float(series.min()), 4) if series.notna().any() else None,
            "max": round(float(series.max()), 4) if series.notna().any() else None,
            "nan_ratio": round(float(series.isna().mean()), 4),
            "near_constant": col in near_constant,
        })

    rows.sort(key=lambda r: r["mutual_information"], reverse=True)

    return {
        "features": rows,
        "correlation_matrix": corr.round(4).to_dict(),
        "high_correlation_pairs": high_pairs,
        "near_constant": near_constant,
        "duplicate_groups": duplicate_groups,
    }


def _find_duplicate_columns(X: pd.DataFrame, cols: list[str]) -> list[list[str]]:
    groups: list[list[str]] = []
    seen: set[str] = set()
    for i, a in enumerate(cols):
        if a in seen:
            continue
        group = [a]
        for b in cols[i + 1:]:
            if (X[a].fillna(-9999).equals(X[b].fillna(-9999))):
                group.append(b)
                seen.add(b)
        if len(group) > 1:
            groups.append(group)
            seen.add(a)
    return groups
