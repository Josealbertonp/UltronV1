"""Build test fixture parquet from UltronML export or synthetic data."""

from __future__ import annotations

from pathlib import Path

import numpy as np
import pandas as pd
import pytest


def build_synthetic_fixture(path: Path, n: int = 120) -> Path:
    rng = np.random.default_rng(42)
    tiers = ["A+", "A", "B", "C"]
    arches = ["TrendContinuation", "Pullback", "Breakout", "Range", "FakeBreakout"]
    hours = list(range(9, 17))

    rows = []
    for i in range(n):
        win = rng.random() > 0.45
        pnl = rng.uniform(8, 40) if win else -rng.uniform(5, 25)
        score = int(rng.integers(55, 85))
        hour = int(rng.choice(hours))
        rows.append({
            "trade_id": f"sig-{i:04d}",
            "timestamp_utc": pd.Timestamp("2026-06-01", tz="UTC") + pd.Timedelta(hours=hour, minutes=i % 60),
            "hour": hour,
            "day_of_week": i % 5,
            "instrument": "MNQ 09-26",
            "direction": "Long" if rng.random() > 0.5 else "Short",
            "score": score,
            "tier": tiers[min(score // 10 - 5, 3)],
            "archetype": rng.choice(arches),
            "regime": "Trending",
            "decision": "Accepted",
            "realized_pnl_ticks": pnl,
            "is_win": win,
            "is_loss": not win,
            "mfe_ticks": abs(pnl) + rng.uniform(0, 15),
            "mae_ticks": rng.uniform(2, 20),
            "bars_in_trade": int(rng.integers(1, 8)),
            "gate_Session_long": True,
            "gate_Session_short": True,
            "gate_Rvol_long": score > 60,
            **{f"feat_{j}": float(rng.random()) for j in range(40)},
        })

    df = pd.DataFrame(rows)
    path.parent.mkdir(parents=True, exist_ok=True)
    df.to_parquet(path, index=False)
    meta = {
        "dataset_v1": {
            "version": "dataset_v1",
            "tradeCount": n,
            "validationPassed": True,
        }
    }
    import json
    (path.parent / "metadata.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    return path


@pytest.fixture
def build_fixture(tmp_path):
    d = tmp_path / "datasets"
    build_synthetic_fixture(d / "dataset_v1.parquet")
    return d
