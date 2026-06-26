"""Feature matrix preparation and validation."""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np
import pandas as pd
from sklearn.preprocessing import LabelEncoder

from ultrontraining.constants import EXCLUDE_FROM_FEATURES, LABEL_COLUMN


@dataclass
class FeatureMatrix:
    X: pd.DataFrame
    y: pd.Series
    feature_names: list[str]
    pnl: pd.Series | None = None


def select_feature_columns(df: pd.DataFrame) -> list[str]:
    cols: list[str] = []
    for c in df.columns:
        if c in EXCLUDE_FROM_FEATURES:
            continue
        if c.startswith("score_contrib_") or c.startswith("score_applied_"):
            continue
        if c.startswith("feature_flag_"):
            continue
        if c.startswith("gate_"):
            cols.append(c)
        elif c.startswith("feat_"):
            cols.append(c)
        elif c.startswith("der_"):
            cols.append(c)
        elif c in ("score", "long_score", "short_score", "tier", "archetype", "regime",
                    "hour", "minute", "day_of_week", "order_flow_passed",
                    "gates_long_allowed", "gates_short_allowed"):
            cols.append(c)
    return sorted(set(cols))


def build_feature_matrix(df: pd.DataFrame) -> FeatureMatrix:
    feature_cols = select_feature_columns(df)
    X = df[feature_cols].copy()

    for col in X.columns:
        if X[col].dtype == bool or X[col].dtype == object:
            if X[col].dtype == object and X[col].nunique(dropna=True) <= 20:
                X[col] = LabelEncoder().fit_transform(X[col].astype(str))
            else:
                X[col] = pd.to_numeric(X[col], errors="coerce")

    X = X.apply(pd.to_numeric, errors="coerce")
    medians = X.median(numeric_only=True)
    X = X.fillna(medians).fillna(0.0)

    y = df[LABEL_COLUMN].astype(int)
    pnl = pd.to_numeric(df.get("realized_pnl_ticks"), errors="coerce")

    return FeatureMatrix(X=X, y=y, feature_names=list(X.columns), pnl=pnl)


@dataclass
class FeatureValidationReport:
    passed: bool
    row_count: int
    feature_count: int
    nan_ratio: float
    constant_features: list[str]
    errors: list[str]

    def to_dict(self) -> dict:
        return {
            "passed": self.passed,
            "rowCount": self.row_count,
            "featureCount": self.feature_count,
            "nanRatio": self.nan_ratio,
            "constantFeatures": self.constant_features,
            "errors": self.errors,
        }


def validate_features(fm: FeatureMatrix, min_rows: int = 50) -> FeatureValidationReport:
    errors: list[str] = []
    if len(fm.X) < min_rows:
        errors.append(f"Insufficient rows: {len(fm.X)} < {min_rows}")

    nan_ratio = float(fm.X.isna().sum().sum() / max(fm.X.size, 1))
    if nan_ratio > 0.25:
        errors.append(f"NaN ratio too high: {nan_ratio:.2%}")

    constant = [c for c in fm.feature_names if fm.X[c].nunique() <= 1]
    if len(constant) == len(fm.feature_names):
        errors.append("All features are constant")

    if fm.y.nunique() < 2:
        errors.append("Target has single class")

    return FeatureValidationReport(
        passed=len(errors) == 0,
        row_count=len(fm.X),
        feature_count=len(fm.feature_names),
        nan_ratio=round(nan_ratio, 4),
        constant_features=constant,
        errors=errors,
    )
