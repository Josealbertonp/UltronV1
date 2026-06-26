"""LightGBM feature importance extraction."""

from __future__ import annotations

from ultron_ml.constants import FEATURE_NAMES


def extract_feature_importance(model, importance_type: str = "gain") -> dict:
    raw = model.booster_.feature_importance(importance_type=importance_type)
    total = float(raw.sum()) or 1.0
    ranked = sorted(
        (
            {
                "feature": FEATURE_NAMES[i] if i < len(FEATURE_NAMES) else f"feature_{i}",
                "index": i,
                "importance": float(v),
                "importancePct": float(v / total),
            }
            for i, v in enumerate(raw)
        ),
        key=lambda x: x["importance"],
        reverse=True,
    )
    return {
        "importanceType": importance_type,
        "features": ranked,
        "top10": ranked[:10],
    }
