"""Load Ultron dataset and comparison exports."""

from __future__ import annotations

from pathlib import Path

import pandas as pd

from ultron_data.loader import load_ultron_sessions

__all__ = [
    "load_ultron_sessions",
    "load_comparison_csv",
    "load_synthetic_ultron",
]


def load_comparison_csv(path: Path) -> pd.DataFrame:
    if not path.exists():
        return pd.DataFrame()
    df = pd.read_csv(path)
    if "BarTime" in df.columns:
        df["timestampUtc"] = pd.to_datetime(df["BarTime"], utc=True)
    if "MatchType" in df.columns:
        df = df.rename(columns={"MatchType": "matchType"})
    return df


def load_synthetic_ultron(n: int = 600, seed: int = 42) -> pd.DataFrame:
    """Synthetic opportunities for pipeline demo when no NT8 data exists."""
    rng = __import__("numpy").random.default_rng(seed)
    base = pd.Timestamp("2026-06-01", tz="UTC")
    rows = []
    for i in range(n):
        score = int(rng.integers(40, 85))
        rvol = float(rng.uniform(0.2, 1.5))
        delta = float(rng.integers(-200, 400))
        adx = float(rng.uniform(12, 35))
        accepted = score >= 55 and rvol >= 0.44 and adx >= 20 and rng.random() > 0.35
        label = 1 if accepted and rng.random() > 0.42 else 0
        r_mult = float(rng.normal(0.7, 1.1) if label else rng.normal(-0.5, 0.7))

        rows.append({
            "signalId": f"synth-{i}",
            "timestampUtc": base + pd.Timedelta(minutes=5 * i),
            "barIndex": i,
            "score": score,
            "longScore": score,
            "shortScore": max(20, score - 10),
            "tier": "A" if score >= 65 else "B" if score >= 55 else "C",
            "decision": "Accepted" if accepted else "Rejected",
            "direction": "Long" if rng.random() > 0.5 else "Short",
            "gatesLongAllowed": adx >= 20,
            "gatesShortAllowed": adx >= 20,
            "f_Rvol": rvol,
            "f_Delta": delta,
            "f_Adx": adx,
            "label": label,
            "rMultiple": r_mult,
            "hasOutcome": True,
        })
    return pd.DataFrame(rows)
