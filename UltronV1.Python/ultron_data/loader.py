"""Load Ultron V1 JSONL datasets (opportunities + outcomes)."""

from __future__ import annotations

from pathlib import Path

import numpy as np
import pandas as pd

from ultron_data.constants import FEATURE_NAMES
from ultron_data.jsonl import iter_jsonl


def load_ultron_sessions(dataset_root: Path) -> pd.DataFrame:
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

        outcomes = {o["signalId"]: o for o in iter_jsonl(out_path)}

        for rec in iter_jsonl(opp_path):
            row = flatten_opportunity(rec)
            oid = row.get("signalId")
            if oid and oid in outcomes:
                row.update(flatten_outcome(outcomes[oid]))
            rows.append(row)

    if not rows:
        return pd.DataFrame()

    df = pd.DataFrame(rows)
    if "timestampUtc" in df.columns:
        df["timestampUtc"] = pd.to_datetime(df["timestampUtc"], utc=True)
        df = df.sort_values("timestampUtc").reset_index(drop=True)
    return df


def flatten_opportunity(rec: dict) -> dict:
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


def flatten_outcome(out: dict) -> dict:
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
