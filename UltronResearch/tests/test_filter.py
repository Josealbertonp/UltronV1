"""Filter dashboard tests."""

import json
from pathlib import Path

from ultronresearch.filter.analysis import analyze_ml_filter
from ultronresearch.filter.dashboard import render_filter_dashboard
from ultronresearch.shadow.analysis import load_shadow_predictions


def test_filter_dashboard(tmp_path):
    sample = tmp_path / "shadow_predictions.jsonl"
    rows = [
        {
            "tradeId": "t1", "confidence": 0.85, "threshold": 0.70,
            "decision": "PASS", "entryAllowed": True, "win": True, "pnlTicks": 30,
            "mfeTicks": 20, "maeTicks": 5, "wouldBlockInActive": False,
        },
        {
            "tradeId": "t2", "confidence": 0.45, "threshold": 0.70,
            "decision": "BLOCK", "entryAllowed": False, "win": False, "pnlTicks": -15,
            "mfeTicks": 5, "maeTicks": 18, "wouldBlockInActive": True,
        },
    ]
    sample.write_text("\n".join(json.dumps(r) for r in rows) + "\n", encoding="utf-8")

    df = load_shadow_predictions(sample)
    analysis = analyze_ml_filter(df)
    assert analysis["passed_count"] >= 1
    assert analysis["blocked_count"] >= 1

    path = render_filter_dashboard(analysis, tmp_path / "out")
    assert path.exists()
