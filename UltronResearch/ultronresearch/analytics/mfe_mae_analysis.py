"""MFE / MAE distribution and segmentation."""

from __future__ import annotations

import numpy as np
import pandas as pd

from ultronresearch.analytics.metrics import compute_group_metrics


def _mfe_mae(df: pd.DataFrame) -> pd.DataFrame:
    out = df.copy()
    out["mfe_ticks"] = pd.to_numeric(out.get("mfe_ticks"), errors="coerce")
    out["mae_ticks"] = pd.to_numeric(out.get("mae_ticks"), errors="coerce")
    return out.dropna(subset=["mfe_ticks", "mae_ticks"], how="any")


def analyze_mfe_mae(df: pd.DataFrame) -> dict:
    data = _mfe_mae(df)
    if data.empty:
        return {"distributions": {}, "scatter": [], "by_segment": {}}

    mfe = data["mfe_ticks"]
    mae = data["mae_ticks"]

    distributions = {
        "mfe": _histogram(mfe),
        "mae": _histogram(mae),
        "mfe_percentiles": _percentiles(mfe),
        "mae_percentiles": _percentiles(mae),
    }

    scatter = data[["trade_id", "mfe_ticks", "mae_ticks", "realized_pnl_ticks", "is_win"]].head(5000)
    scatter_records = scatter.to_dict(orient="records") if "trade_id" in scatter.columns else []

    by_segment = {}
    for dim in ("tier", "archetype", "hour"):
        if dim not in data.columns:
            continue
        segments = []
        for val, grp in data.groupby(dim, dropna=False):
            m = compute_group_metrics(grp)
            m[dim] = str(val)
            m["mfe_mean"] = round(float(grp["mfe_ticks"].mean()), 4)
            m["mae_mean"] = round(float(grp["mae_ticks"].mean()), 4)
            segments.append(m)
        by_segment[dim] = segments

    score_bins = pd.cut(pd.to_numeric(data.get("score"), errors="coerce"), bins=5)
    score_segments = []
    for val, grp in data.groupby(score_bins, observed=True):
        m = compute_group_metrics(grp)
        m["score_bin"] = str(val)
        m["mfe_mean"] = round(float(grp["mfe_ticks"].mean()), 4)
        m["mae_mean"] = round(float(grp["mae_ticks"].mean()), 4)
        score_segments.append(m)
    by_segment["score_bin"] = score_segments

    heatmap = _mfe_mae_heatmap(data)

    return {
        "distributions": distributions,
        "scatter": scatter_records,
        "by_segment": by_segment,
        "heatmap_mfe_mae": heatmap,
    }


def _histogram(series: pd.Series, bins: int = 20) -> dict:
    counts, edges = np.histogram(series.dropna(), bins=bins)
    return {"counts": counts.tolist(), "bin_edges": edges.tolist()}


def _percentiles(series: pd.Series) -> dict:
    qs = [0.1, 0.25, 0.5, 0.75, 0.9]
    return {f"p{int(q*100)}": round(float(series.quantile(q)), 4) for q in qs}


def _mfe_mae_heatmap(df: pd.DataFrame, bins: int = 10) -> dict:
    mfe_bins = pd.cut(df["mfe_ticks"], bins=bins)
    mae_bins = pd.cut(df["mae_ticks"], bins=bins)
    pivot = df.groupby([mfe_bins, mae_bins], observed=True).size().unstack(fill_value=0)
    return {
        "rows": [str(i) for i in pivot.index],
        "cols": [str(c) for c in pivot.columns],
        "values": pivot.values.tolist(),
    }
