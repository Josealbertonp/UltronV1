"""Offline feature selection — low variance, duplicates, correlation, MI ranking."""

from __future__ import annotations

import numpy as np
import pandas as pd
from sklearn.feature_selection import mutual_info_classif
from sklearn.inspection import permutation_importance


def remove_low_variance(X: pd.DataFrame, min_std: float = 1e-6) -> tuple[pd.DataFrame, list[str]]:
    dropped = [c for c in X.columns if X[c].std(ddof=0) <= min_std]
    return X.drop(columns=dropped, errors="ignore"), dropped


def remove_duplicate_columns(X: pd.DataFrame) -> tuple[pd.DataFrame, list[str]]:
    keep = ~X.T.duplicated()
    dropped = [c for c in X.columns if c not in keep[keep].index]
    return X.loc[:, keep], dropped


def remove_high_correlation(X: pd.DataFrame, threshold: float = 0.95) -> tuple[pd.DataFrame, list[str]]:
    corr = X.corr(numeric_only=True).abs()
    upper = corr.where(np.triu(np.ones(corr.shape), k=1).astype(bool))
    drop = [col for col in upper.columns if any(upper[col] >= threshold)]
    return X.drop(columns=drop, errors="ignore"), drop


def mutual_information_ranking(X: pd.DataFrame, y: pd.Series) -> list[dict]:
    mi = mutual_info_classif(X.fillna(0), y, random_state=42)
    ranked = sorted(
        [{"feature": c, "mutual_information": round(float(mi[i]), 6)} for i, c in enumerate(X.columns)],
        key=lambda r: r["mutual_information"],
        reverse=True,
    )
    return ranked


def permutation_importance_ranking(model, X: pd.DataFrame, y: pd.Series, n_repeats: int = 5) -> list[dict]:
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


def select_features(X: pd.DataFrame, y: pd.Series) -> tuple[pd.DataFrame, dict]:
    report: dict = {"steps": []}
    cur = X.copy()
    cur, dropped = remove_low_variance(cur)
    report["steps"].append({"low_variance_removed": dropped})
    cur, dropped = remove_duplicate_columns(cur)
    report["steps"].append({"duplicates_removed": dropped})
    cur, dropped = remove_high_correlation(cur)
    report["steps"].append({"high_correlation_removed": dropped})
    report["mutual_information"] = mutual_information_ranking(cur, y)
    report["final_feature_count"] = len(cur.columns)
    return cur, report
