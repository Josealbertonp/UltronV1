"""Offline training pipeline skeleton — no training in RC2 Phase 1."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

import pandas as pd

from ultronml.dataset.builder import DatasetBuilder
from ultronml.dataset.versioning import DatasetVersionManager, DatasetExportResult
from ultronml.features.metadata import FeatureMetadataRegistry
from ultronml.features.validator import FeatureValidator


@dataclass
class TrainingPipelineConfig:
    dataset_root: Path
    output_dir: Path
    models_dir: Path
    accepted_only: bool = True
    with_outcomes_only: bool = True
    min_rows: int = 50


@dataclass
class TrainingPipelineResult:
    export: DatasetExportResult | None = None
    stages_completed: list[str] = field(default_factory=list)
    skipped_reason: str | None = None


class TrainingPipeline:
    """
    RC2 Phase 1: orchestrates load → validate → export.
    Training step is a placeholder for future XGBoost/LightGBM/ONNX.
    """

    def __init__(self, config: TrainingPipelineConfig):
        self._config = config

    def run(self) -> TrainingPipelineResult:
        result = TrainingPipelineResult()
        cfg = self._config

        builder = DatasetBuilder(cfg.dataset_root)
        df = builder.build(
            accepted_only=cfg.accepted_only,
            with_outcomes_only=cfg.with_outcomes_only,
        )
        result.stages_completed.append("load")

        if len(df) < cfg.min_rows:
            result.skipped_reason = f"Insufficient rows: {len(df)} < {cfg.min_rows}"
            return result

        report = FeatureValidator().validate(df)
        result.stages_completed.append("validate")
        if not report.passed:
            result.skipped_reason = f"Validation failed: {report.errors}"
            return result

        meta_reg = FeatureMetadataRegistry()
        meta_path = cfg.output_dir / "feature_metadata.json"
        cfg.output_dir.mkdir(parents=True, exist_ok=True)
        meta_reg.save(meta_path)
        result.stages_completed.append("feature_metadata")

        exporter = DatasetVersionManager(cfg.output_dir)
        export = exporter.export(df, validate=False)
        result.export = export
        result.stages_completed.append("export_parquet")

        # Placeholder — no model training in Phase 1
        result.stages_completed.append("train_skipped")
        _ensure_model_placeholders(cfg.models_dir)

        return result


def _ensure_model_placeholders(models_dir: Path) -> None:
    models_dir.mkdir(parents=True, exist_ok=True)
    for name, content in _MODEL_PLACEHOLDERS.items():
        path = models_dir / name
        if not path.exists():
            path.write_text(content, encoding="utf-8")


_MODEL_PLACEHOLDERS = {
    "version.json": '{\n  "status": "placeholder",\n  "phase": "RC2-Phase1",\n  "model": null\n}\n',
    "metadata.json": '{\n  "status": "placeholder",\n  "description": "Train model to populate"\n}\n',
    "feature_list.json": '{\n  "features": [],\n  "status": "placeholder"\n}\n',
}
