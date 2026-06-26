"""Load Ultron V1 JSONL datasets (opportunities + outcomes)."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Iterable

import numpy as np
import pandas as pd

from ultron_ml.constants import FEATURE_COUNT, FEATURE_NAMES


def _iter_jsonl(path: Path) -> Iterable[dict]:
    if not path.exists():
        return
    with path.open("r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if line:
                yield json.loads(line)


def load_sessions(dataset_root: Path) -> pd.DataFrame:
    """Load all opportunity records from dataset/sessions/*/opportunities.jsonl."""
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
            row = _flatten_record(rec)
            oid = row.get("signalId")
            if oid and oid in outcomes:
                row.update(_flatten_outcome(outcomes[oid]))
            rows.append(row)

    if not rows:
        return pd.DataFrame()

    df = pd.DataFrame(rows)
    if "timestampUtc" in df.columns:
        df["timestampUtc"] = pd.to_datetime(df["timestampUtc"], utc=True)
        df = df.sort_values("timestampUtc").reset_index(drop=True)
    return df


def _flatten_record(rec: dict) -> dict:
    row: dict = {
        "signalId": rec.get("signalId"),
        "sessionId": rec.get("sessionId"),
        "timestampUtc": rec.get("timestampUtc"),
        "barIndex": rec.get("barIndex"),
        "instrument": rec.get("instrument"),
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
        "rejectionReason": rec.get("rejectionReason"),
        "gatesLongAllowed": rec.get("gates", {}).get("longAllowed"),
        "gatesShortAllowed": rec.get("gates", {}).get("shortAllowed"),
        "orderFlowPassed": rec.get("orderFlow", {}).get("passed"),
        "featureSchemaVersion": rec.get("versions", {}).get("featureSchema"),
        "scoreVersion": rec.get("versions", {}).get("score"),
        "ultronVersion": rec.get("versions", {}).get("ultron"),
        "configVersion": rec.get("versions", {}).get("config"),
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


def feature_matrix(df: pd.DataFrame) -> np.ndarray:
    cols = [f"f_{n}" for n in FEATURE_NAMES]
    missing = [c for c in cols if c not in df.columns]
    if missing:
        raise ValueError(f"Missing feature columns: {missing[:5]}...")
    return df[cols].astype(float).values


def load_synthetic(n: int = 500, seed: int = 42) -> pd.DataFrame:
    """Generate synthetic labeled data for pipeline testing when no NT8 dataset exists."""
    rng = np.random.default_rng(seed)
    rows = []
    base_time = pd.Timestamp("2026-06-01", tz="UTC")

    for i in range(n):
        feats = rng.normal(0, 1, FEATURE_COUNT)
        score = int(np.clip(40 + feats[0] * 10 + feats[8] * 5 + rng.normal(0, 5), 20, 95))
        tier = "A" if score >= 65 else "B" if score >= 55 else "C"
        accepted = score >= 55 and rng.random() > 0.3
        label = 1 if accepted and (score + rng.normal(0, 10)) > 58 else 0
        r_mult = rng.normal(0.8, 1.2) if label else rng.normal(-0.6, 0.8)

        row = {
            "signalId": f"synth-{i}",
            "timestampUtc": base_time + pd.Timedelta(minutes=5 * i),
            "score": score,
            "tier": tier,
            "decision": "Accepted" if accepted else "Rejected",
            "label": label,
            "hasOutcome": True,
            "rMultiple": float(r_mult),
            "isWin": bool(label),
        }
        for j, name in enumerate(FEATURE_NAMES):
            row[f"f_{name}"] = float(feats[j])
        rows.append(row)

    return pd.DataFrame(rows)
