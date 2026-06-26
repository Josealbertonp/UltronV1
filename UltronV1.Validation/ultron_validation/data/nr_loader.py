"""Load NightRanger trade and blocked logs."""

from __future__ import annotations

import json
import re
from pathlib import Path

import pandas as pd


def _iter_jsonl(path: Path):
    with path.open("r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                try:
                    yield json.loads(line)
                except json.JSONDecodeError:
                    continue


def find_nr_files(log_root: Path, pattern: str) -> list[Path]:
    if not log_root.exists():
        return []
    return sorted(log_root.rglob(pattern))


def load_nr_trades(log_root: Path) -> pd.DataFrame:
    files = find_nr_files(log_root, "NightRanger_trades_*.jsonl")
    rows = []
    for f in files:
        for rec in _iter_jsonl(f):
            rows.append(_normalize_trade(rec))
    if not rows:
        return pd.DataFrame()
    df = pd.DataFrame(rows)
    df["timestampUtc"] = pd.to_datetime(df["entryTime"], utc=True, errors="coerce")
    return df.sort_values("timestampUtc").reset_index(drop=True)


def load_nr_blocked(log_root: Path) -> pd.DataFrame:
    files = find_nr_files(log_root, "NightRanger_blocked_*.jsonl")
    rows = []
    for f in files:
        for rec in _iter_jsonl(f):
            rows.append({
                "timestampUtc": pd.to_datetime(rec.get("Timestamp"), utc=True, errors="coerce"),
                "direction": _norm_dir(rec.get("SignalDir", "")),
                "score": rec.get("Score"),
                "blockReason": rec.get("BlockReason"),
                "adx": rec.get("ADX"),
                "er": rec.get("ER"),
            })
    if not rows:
        return pd.DataFrame()
    return pd.DataFrame(rows).sort_values("timestampUtc").reset_index(drop=True)


def load_nr_trades_csv(log_root: Path) -> pd.DataFrame:
    files = find_nr_files(log_root, "NightRanger_trades_*.csv")
    rows = []
    for f in files:
        try:
            df = pd.read_csv(f)
            for _, r in df.iterrows():
                rows.append({
                    "entryTime": r.get("EntryTime"),
                    "exitTime": r.get("ExitTime"),
                    "direction": _norm_dir(str(r.get("Direction", ""))),
                    "profitTicks": r.get("ProfitTicks"),
                    "score": r.get("Score"),
                    "mfeTicks": r.get("MFETicks"),
                    "maeTicks": r.get("MAETicks"),
                })
        except Exception:
            continue
    if not rows:
        return pd.DataFrame()
    df = pd.DataFrame(rows)
    df["timestampUtc"] = pd.to_datetime(df["entryTime"], utc=True, errors="coerce")
    return df.sort_values("timestampUtc").reset_index(drop=True)


def _normalize_trade(rec: dict) -> dict:
    return {
        "entryTime": rec.get("EntryTime") or rec.get("entryTime"),
        "exitTime": rec.get("ExitTime") or rec.get("exitTime"),
        "direction": _norm_dir(rec.get("Direction", "")),
        "profitTicks": rec.get("ProfitTicks") or rec.get("profitTicks"),
        "score": rec.get("Score") or rec.get("score"),
        "mfeTicks": rec.get("MFETicks"),
        "maeTicks": rec.get("MAETicks"),
        "exitReason": rec.get("ExitReason"),
        "adx": rec.get("ADX"),
        "rsi": rec.get("RSI"),
    }


def _norm_dir(val: str) -> str:
    v = str(val).strip().upper()
    if v in ("LONG", "BUY", "1"):
        return "Long"
    if v in ("SHORT", "SELL", "-1"):
        return "Short"
    return "None"


def load_synthetic_nr_trades(ultron_df: pd.DataFrame, seed: int = 42) -> pd.DataFrame:
    """Generate NR-like trades aligned loosely with Ultron for demo mode."""
    import numpy as np

    rng = np.random.default_rng(seed)
    accepted = ultron_df[ultron_df["decision"] == "Accepted"].copy()
    if accepted.empty:
        accepted = ultron_df.iloc[::3].copy()

    rows = []
    for _, row in accepted.iterrows():
        if rng.random() < 0.15:
            continue  # UltronOnly
        ts = row["timestampUtc"]
        if rng.random() < 0.1:
            ts = ts + pd.Timedelta(minutes=int(rng.integers(5, 15)))  # NR later
        profit = float(row.get("rMultiple", 0) * 30) if pd.notna(row.get("rMultiple")) else float(rng.normal(5, 20))
        rows.append({
            "entryTime": ts.isoformat(),
            "timestampUtc": ts,
            "direction": row.get("direction", "Long"),
            "profitTicks": profit,
            "score": row.get("score", 55) + int(rng.integers(-5, 5)),
        })

    # NR-only extras
    for i in range(max(20, len(accepted) // 5)):
        ts = ultron_df["timestampUtc"].iloc[int(rng.integers(0, len(ultron_df)))]
        rows.append({
            "entryTime": ts.isoformat(),
            "timestampUtc": ts + pd.Timedelta(minutes=10),
            "direction": "Long" if rng.random() > 0.5 else "Short",
            "profitTicks": float(rng.normal(0, 15)),
            "score": int(rng.integers(50, 75)),
        })

    return pd.DataFrame(rows).sort_values("timestampUtc").reset_index(drop=True)
