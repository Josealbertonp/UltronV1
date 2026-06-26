"""End-to-end reproducible training pipeline."""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

import lightgbm as lgb
import numpy as np
import pandas as pd

from ultron_ml.data.feature_validation import validate_features
from ultron_ml.data.loader import feature_matrix, load_sessions, load_synthetic
from ultron_ml.explain.feature_importance import extract_feature_importance
from ultron_ml.explain.shap_explainer import compute_shap_summary
from ultron_ml.export.manifest_builder import (
    build_feature_schema,
    build_inference_config,
    build_training_config,
    build_training_manifest,
    write_json,
)
from ultron_ml.export.onnx_export import export_lightgbm_onnx
from ultron_ml.metrics.calculator import full_metrics
from ultron_ml.registry.model_registry import ModelRegistry
from ultron_ml.validation.oos_eval import evaluate_oos, temporal_oos_split
from ultron_ml.validation.temporal_cv import run_temporal_cv
from ultron_ml.validation.walk_forward import run_walk_forward


DEFAULT_LGBM_PARAMS = {
    "n_estimators": 200,
    "learning_rate": 0.05,
    "num_leaves": 31,
    "max_depth": -1,
    "min_child_samples": 20,
    "subsample": 0.8,
    "colsample_bytree": 0.8,
    "reg_alpha": 0.1,
    "reg_lambda": 0.1,
    "random_state": 42,
    "verbose": -1,
}


@dataclass
class TrainingResult:
    model_id: str
    model_dir: Path
    metrics: dict
    manifest: dict
    registered: dict = field(default_factory=dict)


def _model_factory(params: dict | None = None):
    p = {**DEFAULT_LGBM_PARAMS, **(params or {})}

    def factory():
        return lgb.LGBMClassifier(**p)

    return factory


class UltronTrainer:
    """Full ML platform trainer — metrics before export, no auto-promotion."""

    def __init__(
        self,
        registry_root: Path,
        lgbm_params: dict | None = None,
        random_seed: int = 42,
    ):
        self.registry_root = registry_root
        self.models_dir = registry_root / "models"
        self.models_dir.mkdir(parents=True, exist_ok=True)
        self.lgbm_params = {**DEFAULT_LGBM_PARAMS, **(lgbm_params or {})}
        self.lgbm_params["random_state"] = random_seed
        self.random_seed = random_seed
        self.registry = ModelRegistry(registry_root)

    def load_data(self, dataset_root: Path | None, use_synthetic: bool) -> pd.DataFrame:
        if use_synthetic or dataset_root is None:
            return load_synthetic(800, seed=self.random_seed)
        df = load_sessions(dataset_root)
        if df.empty:
            return load_synthetic(800, seed=self.random_seed)
        return df

    def train(
        self,
        df: pd.DataFrame,
        *,
        dataset_source: str = "unknown",
        oos_ratio: float = 0.2,
    ) -> TrainingResult:
        if "timestampUtc" in df.columns:
            df = df.sort_values("timestampUtc").reset_index(drop=True)

        fv = validate_features(df)
        if not fv.passed:
            raise ValueError(f"Feature validation failed: {fv.errors}")

        labeled = df[df["label"].notna()].copy()
        if len(labeled) < 80:
            raise ValueError(f"Insufficient labeled rows: {len(labeled)} (min 80)")

        train_df, test_df = temporal_oos_split(labeled, oos_ratio=oos_ratio)
        factory = _model_factory(self.lgbm_params)

        cv_report = run_temporal_cv(factory, labeled, n_splits=5)
        wf_report = run_walk_forward(factory, labeled, train_bars=200, test_bars=50, step_bars=50)
        oos_report = evaluate_oos(factory(), train_df, test_df)

        model = factory()
        X_train = feature_matrix(train_df)
        y_train = train_df["label"].astype(int).values
        X_test = feature_matrix(test_df)
        y_test = test_df["label"].astype(int).values

        model.fit(X_train, y_train)
        proba = model.predict_proba(X_test)[:, 1]
        pred = (proba >= 0.5).astype(int)

        metrics = full_metrics(y_test, proba, pred, test_df)
        metrics["temporalCv"] = cv_report.aggregate
        metrics["walkForward"] = wf_report.aggregate
        metrics["oosSplit"] = {"trainSize": len(train_df), "testSize": len(test_df)}

        importance = extract_feature_importance(model)
        shap_summary = compute_shap_summary(model, train_df)

        ts = datetime.now(timezone.utc).strftime("%Y%m%d_%H%M%S")
        model_id = f"ultron_mnq_v1_{ts}"
        model_dir = self.models_dir / model_id

        training_config = build_training_config(self.lgbm_params)
        manifest = build_training_manifest(
            model_id,
            dataset_info={
                "source": dataset_source,
                "totalRows": len(df),
                "labeledRows": len(labeled),
                "trainRows": len(train_df),
                "oosRows": len(test_df),
            },
            feature_validation=fv.to_dict(),
            temporal_cv={
                "aggregate": cv_report.aggregate,
                "folds": [{"fold": f.fold, "metrics": f.metrics} for f in cv_report.folds],
            },
            walk_forward={
                "aggregate": wf_report.aggregate,
                "windows": [
                    {"window": w.window, "metrics": w.metrics} for w in wf_report.windows
                ],
            },
            oos_report={
                "trainSize": oos_report.train_size,
                "testSize": oos_report.test_size,
                "metrics": oos_report.metrics,
            },
            metrics=metrics,
            training_config=training_config,
            reproducibility={
                "randomSeed": self.random_seed,
                "lightgbmParams": self.lgbm_params,
                "oosRatio": oos_ratio,
                "pythonPackages": "see requirements.txt",
            },
            shap_summary=shap_summary,
            feature_importance=importance,
        )

        inference_config = build_inference_config(model_id, training_config)

        write_json(model_dir / "metrics.json", metrics)
        write_json(model_dir / "config.json", inference_config)
        write_json(model_dir / "feature_schema.json", build_feature_schema())
        write_json(model_dir / "training_manifest.json", manifest)
        write_json(model_dir / "feature_importance.json", importance)
        export_lightgbm_onnx(model, model_dir / "model.onnx")

        registered = self.registry.register_model(model_dir, metrics)

        return TrainingResult(
            model_id=model_id,
            model_dir=model_dir,
            metrics=metrics,
            manifest=manifest,
            registered=registered,
        )
