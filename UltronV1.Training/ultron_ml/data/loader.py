"""Load Ultron V1 JSONL datasets (opportunities + outcomes)."""

from __future__ import annotations

from pathlib import Path

import numpy as np
import pandas as pd

from ultron_data.constants import FEATURE_COUNT, FEATURE_NAMES
from ultron_data.loader import feature_matrix, load_ultron_sessions

load_sessions = load_ultron_sessions


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


__all__ = ["feature_matrix", "load_sessions", "load_synthetic"]
