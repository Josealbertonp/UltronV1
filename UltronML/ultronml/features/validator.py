"""Pre-export dataset validation."""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
import pandas as pd

from ultronml.constants import FEATURE_COUNT, LABEL_COLUMNS
from ultronml.dataset.builder import DatasetBuilder


@dataclass
class ValidationReport:
    passed: bool
    row_count: int
    duplicate_trade_ids: int = 0
    nan_counts: dict[str, int] = field(default_factory=dict)
    inf_counts: dict[str, int] = field(default_factory=dict)
    absurd_values: list[str] = field(default_factory=list)
    empty_fields: list[str] = field(default_factory=list)
    errors: list[str] = field(default_factory=list)
    warnings: list[str] = field(default_factory=list)

    def to_dict(self) -> dict:
        return {
            "passed": self.passed,
            "rowCount": self.row_count,
            "duplicateTradeIds": self.duplicate_trade_ids,
            "nanCounts": self.nan_counts,
            "infCounts": self.inf_counts,
            "absurdValues": self.absurd_values,
            "emptyFields": self.empty_fields,
            "errors": self.errors,
            "warnings": self.warnings,
        }

    @classmethod
    def skipped(cls, row_count: int) -> ValidationReport:
        return cls(passed=True, row_count=row_count, warnings=["validation skipped"])


class FeatureValidator:
    ABSURD_ABS_THRESHOLD = 1e12
    MAX_NAN_RATIO = 0.25

    def validate(self, df: pd.DataFrame) -> ValidationReport:
        report = ValidationReport(passed=True, row_count=len(df))

        if df.empty:
            report.passed = False
            report.errors.append("Dataset is empty")
            return report

        if "trade_id" in df.columns:
            dup = int(df["trade_id"].duplicated().sum())
            report.duplicate_trade_ids = dup
            if dup > 0:
                report.passed = False
                report.errors.append(f"Duplicate trade_id count: {dup}")

        feat_cols = DatasetBuilder.feature_columns(df)
        if len(feat_cols) != DatasetBuilder.expected_feature_count():
            report.passed = False
            report.errors.append(
                f"Feature column count {len(feat_cols)} != expected {FEATURE_COUNT}"
            )

        for col in feat_cols:
            series = df[col]
            nan_n = int(series.isna().sum())
            if nan_n:
                report.nan_counts[col] = nan_n
            numeric = pd.to_numeric(series, errors="coerce")
            inf_n = int(np.isinf(numeric).sum())
            if inf_n:
                report.inf_counts[col] = inf_n
                report.passed = False
                report.errors.append(f"Infinity in {col}: {inf_n}")

            if numeric.notna().any() and numeric.abs().max() > self.ABSURD_ABS_THRESHOLD:
                report.absurd_values.append(col)
                report.warnings.append(f"Absurd magnitude in {col}")

        if report.nan_counts:
            total_cells = len(df) * max(len(feat_cols), 1)
            nan_total = sum(report.nan_counts.values())
            if nan_total / total_cells > self.MAX_NAN_RATIO:
                report.passed = False
                report.errors.append(f"NaN ratio too high: {nan_total}/{total_cells}")

        for col in ("trade_id", "instrument", "direction"):
            if col in df.columns and df[col].isna().all():
                report.empty_fields.append(col)
                report.passed = False
                report.errors.append(f"Required field empty: {col}")

        label_in_features = [c for c in feat_cols if c.replace("feat_", "") in LABEL_COLUMNS]
        if label_in_features:
            report.passed = False
            report.errors.append(f"Label leakage into features: {label_in_features[:3]}")

        if "timestamp_utc" in df.columns and not df["timestamp_utc"].is_monotonic_increasing:
            report.warnings.append("timestamps not monotonic — sorted at export recommended")

        return report
