"""Build reproducible training manifests and artifact bundle."""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

from ultron_ml.constants import (
    FEATURE_COUNT,
    FEATURE_NAMES,
    FEATURE_SCHEMA_VERSION,
    SCORE_VERSION,
    ULTRON_VERSION,
)


def build_feature_schema() -> dict:
    return {
        "schemaVersion": FEATURE_SCHEMA_VERSION,
        "instrument": "MNQ 09-26",
        "featureCount": FEATURE_COUNT,
        "features": FEATURE_NAMES,
    }


def build_training_config(params: dict) -> dict:
    return {
        "algorithm": "lightgbm",
        "objective": "binary",
        "params": params,
        "promotionPolicy": "manual_only",
        "autoPromote": False,
    }


def build_training_manifest(
    model_id: str,
    *,
    dataset_info: dict,
    feature_validation: dict,
    temporal_cv: dict,
    walk_forward: dict,
    oos_report: dict,
    metrics: dict,
    training_config: dict,
    reproducibility: dict,
    shap_summary: dict,
    feature_importance: dict,
) -> dict:
    return {
        "manifestVersion": 1,
        "modelId": model_id,
        "ultronVersion": ULTRON_VERSION,
        "featureSchemaVersion": FEATURE_SCHEMA_VERSION,
        "scoreVersion": SCORE_VERSION,
        "createdUtc": datetime.now(timezone.utc).isoformat(),
        "instrument": "MNQ 09-26",
        "status": "trained_not_promoted",
        "promotionPolicy": "manual_only",
        "autoPromote": False,
        "artifacts": [
            "model.onnx",
            "metrics.json",
            "config.json",
            "feature_schema.json",
            "training_manifest.json",
            "feature_importance.json",
        ],
        "dataset": dataset_info,
        "validation": {
            "featureValidation": feature_validation,
            "temporalCrossValidation": temporal_cv,
            "walkForwardValidation": walk_forward,
            "outOfSampleEvaluation": oos_report,
        },
        "metrics": metrics,
        "trainingConfig": training_config,
        "reproducibility": reproducibility,
        "shapExplanation": shap_summary,
        "featureImportanceSummary": feature_importance.get("top10", []),
    }


def build_inference_config(model_id: str, training_config: dict) -> dict:
    return {
        "modelId": model_id,
        "ultronVersion": ULTRON_VERSION,
        "featureSchemaVersion": FEATURE_SCHEMA_VERSION,
        "advisoryOnly": True,
        "neverVeto": True,
        "maxBoostPoints": 8,
        "maxPenaltyPoints": -6,
        "boostThreshold": 0.62,
        "penaltyThreshold": 0.45,
        "neutralZone": [0.45, 0.62],
        "trainingParams": training_config.get("params", {}),
    }


def write_json(path: Path, payload: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as f:
        json.dump(payload, f, indent=2, default=_json_default)


def _json_default(obj: Any):
    if hasattr(obj, "to_dict"):
        return obj.to_dict()
    if hasattr(obj, "__dict__"):
        return obj.__dict__
    return str(obj)
