"""Synthetic dataset fixture for tests."""

from __future__ import annotations

from pathlib import Path

import numpy as np
import pandas as pd
import pytest


def build_synthetic_parquet(path: Path, n: int = 150) -> Path:
    rng = np.random.default_rng(42)
    tiers = ["A+", "A", "B", "C"]
    rows = []
    for i in range(n):
        score = int(rng.integers(55, 85))
        win = rng.random() > (0.55 - (score - 60) * 0.01)
        pnl = rng.uniform(10, 35) if win else -rng.uniform(5, 20)
        hour = int(rng.integers(9, 16))
        rows.append({
            "trade_id": f"sig-{i:04d}",
            "timestamp_utc": pd.Timestamp("2026-01-01", tz="UTC") + pd.Timedelta(days=i // 8, hours=hour),
            "hour": hour,
            "day_of_week": (i % 5),
            "score": score,
            "tier": tiers[min(max(score // 10 - 5, 0), 3)],
            "archetype": rng.choice(["Pullback", "Breakout", "Range"]),
            "regime": "Trending",
            "order_flow_passed": True,
            "is_win": win,
            "realized_pnl_ticks": pnl,
            "mfe_ticks": abs(pnl) + rng.uniform(0, 10),
            "mae_ticks": rng.uniform(2, 15),
            **{f"feat_{j}": float(rng.random() + (0.3 if win else -0.1)) for j in range(40)},
        })

    df = pd.DataFrame(rows)
    path.parent.mkdir(parents=True, exist_ok=True)
    df.to_parquet(path, index=False)
    import json
    (path.parent / "metadata.json").write_text(
        json.dumps({"dataset_v1": {"version": "dataset_v1", "tradeCount": n}}),
        encoding="utf-8",
    )
    return path


@pytest.fixture
def datasets_dir(tmp_path):
    d = tmp_path / "datasets"
    build_synthetic_parquet(d / "dataset_v1.parquet", n=150)
    return d
