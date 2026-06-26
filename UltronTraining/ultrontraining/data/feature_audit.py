"""Offline feature audit — RC2 Phase 5."""

from __future__ import annotations

import numpy as np
import pandas as pd
from sklearn.feature_selection import mutual_info_classif
from sklearn.inspection import permutation_importance

from ultrontraining.constants import LABEL_COLUMN
from ultrontraining.data.features import build_feature_matrix, select_feature_columns
from ultrontraining.models.base import get_trainer_by_name


def audit_features(df: pd.DataFrame, y: pd.Series | None = None) -> dict:
    """Full feature audit: types, variance, NaN, MI, correlation, constants."""
    if y is None:
        y = df[LABEL_COLUMN].astype(int)

    feature_cols = select_feature_columns(df)
    X = df[feature_cols].copy()

    for col in X.columns:
        if X[col].dtype == bool or X[col].dtype == object or str(X[col].dtype) == "string":
            X[col] = pd.to_numeric(
                X[col].astype(str).where(X[col].notna(), other=np.nan),
                errors="coerce",
            )
    X = X.apply(pd.to_numeric, errors="coerce").fillna(0)

    rows: list[dict] = []
    mi_vals = mutual_info_classif(X.values, y, random_state=42)
    mi_map = {c: float(mi_vals[i]) for i, c in enumerate(X.columns)}

    target_corr = {}
    for c in X.columns:
        try:
            target_corr[c] = round(float(X[c].corr(y)), 6)
        except (TypeError, ValueError):
            target_corr[c] = None

    corr_matrix = X.corr(numeric_only=True)
    high_corr_pairs: list[dict] = []
    cols = list(corr_matrix.columns)
    for i, a in enumerate(cols):
        for b in cols[i + 1 :]:
            v = corr_matrix.loc[a, b]
            if pd.notna(v) and abs(v) >= 0.95:
                high_corr_pairs.append({"feature_a": a, "feature_b": b, "correlation": round(float(v), 6)})

    constants = [c for c in X.columns if X[c].nunique(dropna=True) <= 1]

    for c in X.columns:
        rows.append({
            "feature": c,
            "dtype": str(X[c].dtype),
            "variance": round(float(X[c].var(ddof=0)), 8),
            "std": round(float(X[c].std(ddof=0)), 8),
            "missing_pct": round(float(X[c].isna().mean()), 6),
            "unique": int(X[c].nunique(dropna=True)),
            "mutual_information": round(mi_map.get(c, 0.0), 6),
            "target_correlation": target_corr.get(c),
            "is_constant": c in constants,
        })

    rows.sort(key=lambda r: r["mutual_information"], reverse=True)

    return {
        "feature_count": len(rows),
        "row_count": len(df),
        "constants": constants,
        "high_correlation_pairs": high_corr_pairs,
        "features": rows,
        "mutual_information_ranking": [
            {"feature": r["feature"], "mutual_information": r["mutual_information"]} for r in rows
        ],
    }


def permutation_audit(model, X: pd.DataFrame, y: pd.Series, n_repeats: int = 5) -> list[dict]:
    result = permutation_importance(
        model, X.values, y.values, n_repeats=n_repeats, random_state=42, scoring="roc_auc"
    )
    ranked = sorted(
        [
            {"feature": c, "permutation_importance_mean": round(float(result.importances_mean[i]), 6)}
            for i, c in enumerate(X.columns)
        ],
        key=lambda r: r["permutation_importance_mean"],
        reverse=True,
    )
    return ranked


def quick_permutation_audit(X: pd.DataFrame, y: pd.Series) -> list[dict]:
    trainer = get_trainer_by_name("CatBoost")
    model = trainer.fit(X.values, y.values)
    return permutation_audit(model, X, y)


def classify_usefulness(audit: dict, mi_threshold: float = 0.01) -> dict:
    useful = [f for f in audit["features"] if f["mutual_information"] >= mi_threshold and not f["is_constant"]]
    useless = [f for f in audit["features"] if f["is_constant"] or f["mutual_information"] < mi_threshold * 0.25]
    return {
        "useful": [f["feature"] for f in useful],
        "low_value": [f["feature"] for f in useless],
        "mi_threshold": mi_threshold,
    }
