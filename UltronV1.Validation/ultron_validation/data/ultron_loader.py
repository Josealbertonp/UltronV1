"""Load Ultron dataset and comparison exports."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import pandas as pd

_SCHEMA_DIR = Path(__file__).resolve().parents[3] / "schema"
if str(_SCHEMA_DIR) not in sys.path:
    sys.path.insert(0, str(_SCHEMA_DIR))

import ultron_schema  # noqa: E402

ultron_schema.validate_schema_consistency()
FEATURE_NAMES = list(ultron_schema.get_feature_names())


def _iter_jsonl(path: Path):
    if not path.exists():
        return
    with path.open("r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                yield json.loads(line)


def load_ultron_sessions(dataset_root: Path) -> pd.DataFrame:
    sessions_dir = dataset_root / "sessions"
    rows: list[dict] = []
    if not sessions_dir.exists():
        return pd.DataFrame()

    for session_dir in sorted(sessions_dir.iterdir()):
        if not session_dir.is_dir():
            continue
        opp_path = session_dir / "opportunities.jsonl"
        out_path = session_dir / "outcomes.jsonl"
        outcomes = {o["signalId"]: o for o in _iter_jsonl(out_path)}

        for rec in _iter_jsonl(opp_path):
            row = _flatten_opportunity(rec)
            sid = row.get("signalId")
            if sid and sid in outcomes:
                row.update(_flatten_outcome(outcomes[sid]))
            rows.append(row)

    if not rows:
        return pd.DataFrame()

    df = pd.DataFrame(rows)
    if "timestampUtc" in df.columns:
        df["timestampUtc"] = pd.to_datetime(df["timestampUtc"], utc=True)
        df = df.sort_values("timestampUtc").reset_index(drop=True)
    return df


def _flatten_opportunity(rec: dict) -> dict:
    row = {
        "signalId": rec.get("signalId"),
        "sessionId": rec.get("sessionId"),
        "timestampUtc": rec.get("timestampUtc"),
        "barIndex": rec.get("barIndex"),
        "close": rec.get("close"),
        "archetype": rec.get("archetype"),
        "direction": rec.get("direction"),
        "score": rec.get("score"),
        "longScore": rec.get("longScore"),
        "shortScore": rec.get("shortScore"),
        "tier": rec.get("tier"),
        "regime": rec.get("regime"),
        "decision": rec.get("decision"),
        "rejectionStage": rec.get("rejectionStage"),
        "gatesLongAllowed": rec.get("gates", {}).get("longAllowed"),
        "gatesShortAllowed": rec.get("gates", {}).get("shortAllowed"),
        "orderFlowPassed": rec.get("orderFlow", {}).get("passed"),
    }
    features = rec.get("features") or []
    for i, name in enumerate(FEATURE_NAMES):
        if i < len(features):
            row[f"f_{name}"] = features[i]
    return row


def _flatten_outcome(out: dict) -> dict:
    return {
        "hasOutcome": True,
        "label": out.get("label", 1 if out.get("isWin") else 0),
        "isWin": out.get("isWin"),
        "direction": out.get("direction"),
        "entryPrice": out.get("entryPrice"),
        "exitPrice": out.get("exitPrice"),
        "stopPrice": out.get("stopPrice"),
        "targetPrice": out.get("targetPrice"),
        "mfeTicks": out.get("mfeTicks"),
        "maeTicks": out.get("maeTicks"),
        "rMultiple": out.get("rMultiple"),
        "realizedPnLTicks": out.get("realizedPnLTicks"),
        "realizedPnLUsd": out.get("realizedPnLUsd"),
        "barsInTrade": out.get("barsInTrade"),
        "secondsInTrade": out.get("secondsInTrade"),
        "exitReason": out.get("exitReason"),
        "executionMode": out.get("executionMode"),
    }


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
