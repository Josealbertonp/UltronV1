"""Shadow dashboard tests."""

from pathlib import Path

import json

from ultronresearch.shadow.analysis import analyze_shadow, load_shadow_predictions, threshold_study
from ultronresearch.shadow.dashboard import generate_shadow_dashboard


def test_shadow_analysis_and_dashboard(tmp_path):
    sample = tmp_path / "shadow_predictions.jsonl"
    row = {
        "tradeId": "sig-001",
        "timestampUtc": "2026-06-01T14:30:00Z",
        "score": 71,
        "tier": "A",
        "direction": "Long",
        "confidence": 0.82,
        "threshold": 0.55,
        "modelVersion": "1.0.0",
        "inferenceTimeMs": 1.5,
        "decision": "PASS_THROUGH",
        "win": True,
        "pnlTicks": 32,
        "mfeTicks": 20,
        "maeTicks": 5,
        "features": [0.1, 0.2, 0.3],
    }
    sample.write_text(json.dumps(row) + "\n", encoding="utf-8")

    df = load_shadow_predictions(sample)
    assert len(df) == 1
    analysis = analyze_shadow(df)
    assert analysis["trade_count"] == 1
    assert "threshold_study" in analysis

    dash = generate_shadow_dashboard(sample, tmp_path / "out")
    assert dash.exists()
