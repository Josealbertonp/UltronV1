"""SHAP explanations for trained LightGBM models."""

from __future__ import annotations

import numpy as np

from ultron_ml.constants import FEATURE_NAMES
from ultron_ml.data.loader import feature_matrix


def compute_shap_summary(model, df, max_samples: int = 200) -> dict:
    try:
        import shap
    except ImportError:
        return {"available": False, "error": "shap not installed"}

    labeled = df[df["label"].notna()].copy()
    if labeled.empty:
        return {"available": False, "error": "no labeled rows"}

    sample = labeled.iloc[: min(len(labeled), max_samples)]
    X = feature_matrix(sample)

    explainer = shap.TreeExplainer(model)
    shap_values = explainer.shap_values(X)
    if isinstance(shap_values, list):
        shap_values = shap_values[1] if len(shap_values) > 1 else shap_values[0]

    mean_abs = np.abs(shap_values).mean(axis=0)
    total = float(mean_abs.sum()) or 1.0
    ranked = sorted(
        (
            {
                "feature": FEATURE_NAMES[i] if i < len(FEATURE_NAMES) else f"feature_{i}",
                "index": i,
                "meanAbsShap": float(mean_abs[i]),
                "meanAbsShapPct": float(mean_abs[i] / total),
            }
            for i in range(len(mean_abs))
        ),
        key=lambda x: x["meanAbsShap"],
        reverse=True,
    )

    return {
        "available": True,
        "sampleSize": int(len(sample)),
        "globalImportance": ranked,
        "top10": ranked[:10],
    }
