"""End-to-end offline training pipeline."""

from __future__ import annotations

import time
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

import numpy as np

from ultrontraining.calibration.calibrator import (
    apply_calibration,
    fit_calibrators,
    select_best_calibration,
)
from ultrontraining.comparison.selector import build_comparison_table, select_champion
from ultrontraining.config import TrainingConfig
from ultrontraining.dashboard.generator import TrainingDashboard
from ultrontraining.data.features import build_feature_matrix, validate_features
from ultrontraining.data.loader import DatasetLoader
from ultrontraining.explain.shap_analysis import compute_shap_report
from ultrontraining.export.artifacts import export_artifacts
from ultrontraining.export.onnx_export import export_to_onnx, verify_onnx
from ultrontraining.metrics.classification import (
    compute_classification_metrics,
    pr_curve_points,
    roc_curve_points,
)
from ultrontraining.models.adapter import sklearn_adapter
from ultrontraining.models.base import get_all_trainers
from ultrontraining.optimization.threshold import optimize_thresholds
from ultrontraining.split.temporal import temporal_split
from ultrontraining.split.walk_forward import walk_forward_expanding, walk_forward_rolling


@dataclass
class PipelineResult:
    champion: dict
    comparison: list[dict]
    output_dir: Path
    artifact_paths: dict[str, Path] = field(default_factory=dict)
    elapsed_ms: float = 0.0
    stages: list[str] = field(default_factory=list)


class TrainingPipeline:
    """RC2 Phase 3 — full offline training. No Core integration."""

    def __init__(self, config: TrainingConfig):
        self._config = config

    def run(self) -> PipelineResult:
        t0 = time.perf_counter()
        cfg = self._config
        out = cfg.resolve_output()
        stages: list[str] = []

        bundle = DatasetLoader(cfg.datasets_dir).load(cfg.dataset_version)
        stages.append("load_dataset")

        fm = build_feature_matrix(bundle.df)
        feat_report = validate_features(fm, cfg.min_rows)
        stages.append("feature_validation")
        if not feat_report.passed:
            raise ValueError(f"Feature validation failed: {feat_report.errors}")

        split = temporal_split(fm, cfg.train_ratio, cfg.val_ratio, cfg.test_ratio)
        stages.append("temporal_split")

        trainers = get_all_trainers(cfg.random_seed)
        if cfg.model_names:
            trainers = [t for t in trainers if t.name in cfg.model_names]

        model_results: list[dict] = []
        for trainer in trainers:
            result = trainer.train_and_benchmark(
                split.train.X.values,
                split.train.y.values,
                split.validation.X.values,
                split.test.X.values,
            )

            val_proba = result.val_proba[:, 1]
            platt, isotonic = fit_calibrators(split.validation.y.values, val_proba)
            best_cal = select_best_calibration(platt, isotonic)

            test_proba = apply_calibration(best_cal, result.test_proba[:, 1])
            val_proba_cal = apply_calibration(best_cal, val_proba)

            val_metrics = compute_classification_metrics(split.validation.y.values, val_proba_cal)
            test_metrics = compute_classification_metrics(split.test.y.values, test_proba)

            pnl = split.test.pnl.values if split.test.pnl is not None else np.zeros(len(split.test.y))
            thresholds = optimize_thresholds(test_proba, pnl, cfg.threshold_grid)

            wf_expanding = wf_rolling = None
            if cfg.run_walk_forward:
                adapter = sklearn_adapter(trainer)

                def factory():
                    return sklearn_adapter(trainer)

                wf_expanding = walk_forward_expanding(
                    fm, factory,
                    n_folds=min(cfg.walk_forward_folds, 3),
                    min_train=max(cfg.min_rows // 2, 30),
                )
                window = cfg.rolling_window_size or max(len(fm.X) // 3, 40)
                wf_rolling = walk_forward_rolling(
                    fm, factory,
                    window_size=window,
                    n_folds=min(cfg.walk_forward_folds, 3),
                    min_train=max(cfg.min_rows // 2, 30),
                )

            shap_report = {}
            if cfg.run_shap:
                shap_report = compute_shap_report(
                    result.model, trainer.name, split.test.X, fm.feature_names,
                    sample_size=cfg.shap_sample_size,
                    output_dir=out / "shap" / trainer.name,
                )

            model_results.append({
                "name": trainer.name,
                "model": result.model,
                "train_time_ms": result.train_time_ms,
                "infer_time_ms": result.infer_time_ms,
                "model_size_bytes": result.model_size_bytes,
                "val_metrics": val_metrics,
                "test_metrics": test_metrics,
                "calibration": best_cal.val_metrics,
                "calibration_method": best_cal.method,
                "calibrator": best_cal,
                "thresholds": thresholds,
                "threshold_metrics": thresholds.get("recommended") or {},
                "roc_curve": roc_curve_points(split.test.y.values, test_proba),
                "pr_curve": pr_curve_points(split.test.y.values, test_proba),
                "walk_forward_expanding": _wf_to_dict(wf_expanding),
                "walk_forward_rolling": _wf_to_dict(wf_rolling),
                "shap": shap_report,
                "feature_count": len(fm.feature_names),
            })

        stages.append("train_all_models")

        comparison = build_comparison_table(model_results)
        champion = select_champion(model_results)
        stages.append("model_selection")

        champion_dir = out / "champion"
        onnx_path = champion_dir / "model.onnx"
        export_to_onnx(
            champion["model"],
            champion["name"],
            champion["feature_count"],
            onnx_path,
        )
        onnx_verified = verify_onnx(onnx_path, champion["feature_count"])
        stages.append("onnx_export")

        training_report = {
            "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
            "datasetVersion": bundle.version,
            "rowCount": len(bundle.df),
            "featureValidation": feat_report.to_dict(),
            "split": {
                "train": len(split.train.X),
                "validation": len(split.validation.X),
                "test": len(split.test.X),
            },
            "comparison": comparison,
            "champion": champion["name"],
            "task": "win_probability",
            "note": "Model predicts P(win) only — direction remains Ultron engine",
        }

        artifact_paths = export_artifacts(
            champion_dir,
            champion_name=champion["name"],
            feature_names=fm.feature_names,
            metrics={
                "validation": champion.get("val_metrics"),
                "test": champion.get("test_metrics"),
                "all_models": comparison,
            },
            calibration={
                "method": champion.get("calibration_method"),
                "metrics": champion.get("calibration"),
            },
            thresholds=champion.get("thresholds", {}),
            training_report=training_report,
            dataset_version=bundle.version,
            onnx_verified=onnx_verified,
        )
        artifact_paths["model_onnx"] = onnx_path
        stages.append("export_artifacts")

        report = {
            "dataset_version": bundle.version,
            "comparison": comparison,
            "champion": champion,
            "thresholds": champion.get("thresholds"),
        }
        TrainingDashboard(out).generate(report)
        stages.append("dashboard")

        elapsed = (time.perf_counter() - t0) * 1000
        return PipelineResult(
            champion=champion,
            comparison=comparison,
            output_dir=out,
            artifact_paths=artifact_paths,
            elapsed_ms=elapsed,
            stages=stages,
        )


def _wf_to_dict(wf) -> dict | None:
    if wf is None:
        return None
    return {
        "mode": wf.mode,
        "aggregate": wf.aggregate,
        "folds": [{"fold": f.fold, "metrics": f.metrics} for f in wf.folds],
    }
