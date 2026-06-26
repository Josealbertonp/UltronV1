"""Feature validation against Ultron schema before training."""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
import pandas as pd

from ultron_ml.constants import FEATURE_COUNT, FEATURE_NAMES, FEATURE_SCHEMA_VERSION


@dataclass
class FeatureValidationReport:
    passed: bool
    schema_version: int
    expected_feature_count: int
    actual_feature_count: int
    row_count: int
    labeled_count: int
    nan_ratio_by_feature: dict[str, float] = field(default_factory=dict)
    constant_features: list[str] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    def to_dict(self) -> dict:
        return {
            "passed": self.passed,
            "schemaVersion": self.schema_version,
            "expectedFeatureCount": self.expected_feature_count,
            "actualFeatureCount": self.actual_feature_count,
            "rowCount": self.row_count,
            "labeledCount": self.labeled_count,
            "nanRatioByFeature": self.nan_ratio_by_feature,
            "constantFeatures": self.constant_features,
            "errors": self.errors,
            "warnings": self.warnings,
        }


def validate_features(df: pd.DataFrame, min_labeled: int = 50) -> FeatureValidationReport:
    cols = [f"f_{n}" for n in FEATURE_NAMES]
    report = FeatureValidationReport(
        passed=True,
        schema_version=FEATURE_SCHEMA_VERSION,
        expected_feature_count=FEATURE_COUNT,
        actual_feature_count=len([c for c in cols if c in df.columns]),
        row_count=len(df),
        labeled_count=int(df["label"].notna().sum()) if "label" in df.columns else 0,
    )

    missing = [c for c in cols if c not in df.columns]
    if missing:
        report.passed = False
        report.errors.append(f"Missing {len(missing)} feature columns")

    if report.actual_feature_count != FEATURE_COUNT:
        report.passed = False
        report.errors.append(
            f"Feature count mismatch: expected {FEATURE_COUNT}, got {report.actual_feature_count}"
        )

    if "timestampUtc" in df.columns and not df["timestampUtc"].is_monotonic_increasing:
        report.warnings.append("timestamps not monotonic — will sort before split")

    if report.labeled_count < min_labeled:
        report.warnings.append(f"Only {report.labeled_count} labeled rows (min recommended {min_labeled})")

    for col in cols:
        if col not in df.columns:
            continue
        series = df[col]
        nan_ratio = float(series.isna().mean())
        if nan_ratio > 0:
            report.nan_ratio_by_feature[col] = nan_ratio
        if series.nunique(dropna=True) <= 1:
            report.constant_features.append(col)

    if report.constant_features:
        report.warnings.append(f"{len(report.constant_features)} constant features detected")

    if df.isnull().values.any():
        nan_pct = float(np.isnan(df[cols].values.astype(float)).mean()) if cols else 0.0
        if nan_pct > 0.05:
            report.warnings.append(f"Global NaN ratio {nan_pct:.2%}")

    return report
