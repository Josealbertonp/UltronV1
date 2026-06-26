"""Export training artifacts."""

from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


def write_json(path: Path, data: Any) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2, default=str), encoding="utf-8")
    return path


def export_artifacts(
    output_dir: Path,
    *,
    champion_name: str,
    feature_names: list[str],
    metrics: dict,
    calibration: dict,
    thresholds: dict,
    training_report: dict,
    dataset_version: str,
    onnx_verified: dict | None = None,
) -> dict[str, Path]:
    output_dir.mkdir(parents=True, exist_ok=True)
    paths: dict[str, Path] = {}

    paths["feature_list"] = write_json(output_dir / "feature_list.json", {
        "features": feature_names,
        "count": len(feature_names),
        "order": "column_order_for_inference",
    })

    paths["metadata"] = write_json(output_dir / "metadata.json", {
        "model": champion_name,
        "task": "win_probability",
        "description": "P(win) — confidence filter only, does not predict direction",
        "datasetVersion": dataset_version,
        "exportedAtUtc": datetime.now(timezone.utc).isoformat(),
        "phase": "RC2-Phase3",
        "integration": "MLModelLoader.OnnxProvider (future)",
        "onnxVerification": onnx_verified or {},
    })

    paths["version"] = write_json(output_dir / "version.json", {
        "version": "1.0.0",
        "model": champion_name,
        "schemaVersion": 1,
    })

    paths["metrics"] = write_json(output_dir / "metrics.json", metrics)
    paths["calibration"] = write_json(output_dir / "calibration.json", calibration)
    paths["thresholds"] = write_json(output_dir / "thresholds.json", thresholds)
    paths["training_report"] = write_json(output_dir / "training_report.json", training_report)

    return paths
