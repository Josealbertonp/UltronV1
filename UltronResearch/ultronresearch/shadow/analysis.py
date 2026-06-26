"""Shadow ML prediction analysis for UltronResearch."""

from __future__ import annotations

import json
from pathlib import Path

import numpy as np
import pandas as pd


def compute_trading_metrics(conf: np.ndarray, pnl: np.ndarray, threshold: float = 0.5) -> dict:
    mask = conf >= threshold
    selected = pnl[mask]
    n = int(mask.sum())
    if n == 0:
        return {
            "threshold": threshold, "trades": 0, "win_rate": 0.0,
            "profit_factor": None, "expectancy": 0.0, "max_drawdown_ticks": 0.0,
        }
    wins = selected > 0
    gp = float(selected[selected > 0].sum())
    gl = float(abs(selected[selected < 0].sum()))
    pf = gp / gl if gl > 0 else None
    eq = np.cumsum(selected)
    dd = eq - np.maximum.accumulate(eq)
    return {
        "threshold": threshold,
        "trades": n,
        "win_rate": round(float(wins.mean()), 4),
        "profit_factor": round(pf, 4) if pf is not None else None,
        "expectancy": round(float(selected.mean()), 4),
        "max_drawdown_ticks": round(float(dd.min()), 4),
    }


def load_shadow_predictions(path: Path | str) -> pd.DataFrame:
    path = Path(path)
    if not path.exists():
        return pd.DataFrame()

    rows = []
    for line in path.read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line:
            rows.append(json.loads(line))

    if not rows:
        return pd.DataFrame()

    df = pd.DataFrame(rows)
    if "features" in df.columns:
        lists = df["features"].apply(lambda x: x if isinstance(x, list) else [])
        if lists.map(len).max() > 0:
            feat_df = pd.DataFrame(lists.tolist())
            feat_df.columns = [f"shadow_feat_{i}" for i in feat_df.columns]
            df = pd.concat([df.drop(columns=["features"]), feat_df], axis=1)
    return df


def analyze_shadow(df: pd.DataFrame) -> dict:
    if df.empty:
        return {"status": "empty"}

    df = df.copy()
    df["confidence"] = pd.to_numeric(df.get("confidence"), errors="coerce")
    df["pnlTicks"] = pd.to_numeric(df.get("pnlTicks"), errors="coerce").fillna(0)
    df["win"] = df.get("win", df["pnlTicks"] > 0)
    if df["win"].dtype != bool:
        df["win"] = df["win"].astype(bool)

    latency = pd.to_numeric(df.get("inferenceTimeMs"), errors="coerce").dropna()

    return {
        "trade_count": len(df),
        "confidence_distribution": _histogram(df["confidence"]),
        "confidence_x_win_rate": _bucket_metric(df, "win_rate"),
        "confidence_x_pf": _bucket_pf(df),
        "confidence_x_expectancy": _bucket_metric(df, "expectancy"),
        "confidence_x_mfe": _bucket_metric(df, "mfe", col="mfeTicks"),
        "confidence_x_mae": _bucket_metric(df, "mae", col="maeTicks"),
        "calibration": _calibration_curve(df),
        "reliability": _reliability_diagram(df),
        "brier_score": _brier_score(df),
        "latency": _latency_stats(latency),
        "threshold_study": threshold_study(df),
    }


def threshold_study(df: pd.DataFrame, grid: list[float] | None = None) -> dict:
    grid = grid or [round(x * 0.05, 2) for x in range(8, 20)]
    conf = pd.to_numeric(df["confidence"], errors="coerce").fillna(0).values
    pnl = pd.to_numeric(df["pnlTicks"], errors="coerce").fillna(0).values
    return {"candidates": [compute_trading_metrics(conf, pnl, t) for t in grid]}


def _histogram(series: pd.Series, bins: int = 20) -> dict:
    counts, edges = np.histogram(series.dropna(), bins=bins, range=(0, 1))
    return {"counts": counts.tolist(), "bin_edges": edges.tolist()}


def _bucket_metric(df, metric, col=None, bins: int = 10):
    conf = pd.to_numeric(df["confidence"], errors="coerce")
    cats = pd.cut(conf, bins=bins)
    rows = []
    for cat, grp in df.groupby(cats, observed=True):
        if metric == "win_rate":
            val = float(grp["win"].mean()) if len(grp) else 0
        elif metric == "expectancy":
            val = float(pd.to_numeric(grp["pnlTicks"], errors="coerce").mean())
        else:
            target = col or metric
            val = float(pd.to_numeric(grp.get(target), errors="coerce").mean())
        rows.append({"bin": str(cat), metric: round(val, 4), "trades": len(grp)})
    return rows


def _bucket_pf(df, bins: int = 10) -> list[dict]:
    conf = pd.to_numeric(df["confidence"], errors="coerce")
    cats = pd.cut(conf, bins=bins)
    rows = []
    for cat, grp in df.groupby(cats, observed=True):
        pnl = pd.to_numeric(grp["pnlTicks"], errors="coerce").fillna(0)
        gp = float(pnl[pnl > 0].sum())
        gl = float(abs(pnl[pnl < 0].sum()))
        pf = gp / gl if gl > 0 else None
        rows.append({"bin": str(cat), "profit_factor": pf, "trades": len(grp)})
    return rows


def _calibration_curve(df: pd.DataFrame, bins: int = 10) -> list[dict]:
    conf = pd.to_numeric(df["confidence"], errors="coerce").fillna(0)
    cats = pd.cut(conf, bins=bins)
    rows = []
    for cat, grp in df.groupby(cats, observed=True):
        rows.append({
            "bin": str(cat),
            "mean_confidence": round(float(grp["confidence"].mean()), 4),
            "observed_win_rate": round(float(grp["win"].mean()), 4),
            "count": len(grp),
        })
    return rows


def _reliability_diagram(df: pd.DataFrame) -> dict:
    return {"points": _calibration_curve(df), "ece": round(_ece(df), 4)}


def _ece(df: pd.DataFrame, n_bins: int = 10) -> float:
    conf = pd.to_numeric(df["confidence"], errors="coerce").fillna(0).values
    y = df["win"].astype(int).values
    bins = np.linspace(0, 1, n_bins + 1)
    ece = 0.0
    for i in range(n_bins):
        mask = (conf >= bins[i]) & (conf < bins[i + 1])
        if not mask.any():
            continue
        ece += mask.mean() * abs(y[mask].mean() - conf[mask].mean())
    return float(ece)


def _brier_score(df: pd.DataFrame) -> float:
    conf = pd.to_numeric(df["confidence"], errors="coerce").fillna(0).values
    y = df["win"].astype(int).values
    return round(float(np.mean((conf - y) ** 2)), 4)


def _latency_stats(latency: pd.Series) -> dict:
    if latency.empty:
        return {}
    return {
        "mean_ms": round(float(latency.mean()), 3),
        "max_ms": round(float(latency.max()), 3),
        "p95_ms": round(float(latency.quantile(0.95)), 3),
        "p99_ms": round(float(latency.quantile(0.99)), 3),
        "count": int(len(latency)),
    }
