"""SHAP explainability."""

from __future__ import annotations

from pathlib import Path

import numpy as np
import pandas as pd


def compute_shap_report(
    model: object,
    model_name: str,
    X: pd.DataFrame,
    feature_names: list[str],
    sample_size: int = 200,
    output_dir: Path | None = None,
) -> dict:
    try:
        import shap
    except ImportError:
        return {"status": "shap_not_installed", "model": model_name}

    X_sample = X.iloc[: min(sample_size, len(X))]
    explainer, shap_values = _build_explainer(model, model_name, X_sample)

    if shap_values is None:
        return {"status": "unsupported_model", "model": model_name}

    if isinstance(shap_values, list):
        sv = shap_values[1]
    else:
        sv = shap_values

    mean_abs = np.abs(sv).mean(axis=0)
    importance = sorted(
        [{"feature": feature_names[i], "mean_abs_shap": round(float(mean_abs[i]), 6)}
         for i in range(len(feature_names))],
        key=lambda x: x["mean_abs_shap"],
        reverse=True,
    )

    report = {
        "status": "ok",
        "model": model_name,
        "global_importance": importance,
        "top_20": importance[:20],
        "bottom_10": importance[-10:],
        "local_explanation_sample": _local_sample(sv, feature_names, X_sample, n=5),
    }

    if output_dir:
        output_dir.mkdir(parents=True, exist_ok=True)
        _save_shap_plots(shap, explainer, sv, X_sample, feature_names, output_dir)

    return report


def _build_explainer(model, model_name: str, X_sample: pd.DataFrame):
    import shap

    name = model_name.lower()
    try:
        if "logistic" in name:
            explainer = shap.LinearExplainer(model, X_sample, feature_perturbation="interventional")
            return explainer, explainer.shap_values(X_sample)
        if any(k in name for k in ("forest", "xgboost", "lightgbm", "catboost")):
            explainer = shap.TreeExplainer(model)
            return explainer, explainer.shap_values(X_sample)
        explainer = shap.Explainer(model.predict_proba, X_sample)
        return explainer, explainer(X_sample).values
    except Exception:
        return None, None


def _local_sample(sv, feature_names, X_sample, n=5):
    rows = []
    for i in range(min(n, len(X_sample))):
        contrib = sorted(
            [{"feature": feature_names[j], "shap": round(float(sv[i, j]), 6)}
             for j in range(len(feature_names))],
            key=lambda x: abs(x["shap"]),
            reverse=True,
        )[:10]
        rows.append({"row_index": i, "contributions": contrib})
    return rows


def _save_shap_plots(shap, explainer, sv, X_sample, feature_names, output_dir: Path):
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    try:
        shap.summary_plot(sv, X_sample, feature_names=feature_names, show=False)
        plt.tight_layout()
        plt.savefig(output_dir / "shap_summary.png", dpi=100, facecolor="#161b22")
        plt.close()
    except Exception:
        pass

    try:
        top_feat = feature_names[int(np.argmax(np.abs(sv).mean(axis=0)))]
        shap.dependence_plot(top_feat, sv, X_sample, show=False)
        plt.tight_layout()
        plt.savefig(output_dir / "shap_dependence.png", dpi=100, facecolor="#161b22")
        plt.close()
    except Exception:
        pass
