"""Model comparison and champion selection."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass
class ModelScorecard:
    name: str
    roc_auc: float | None
    pr_auc: float | None
    profit_factor: float | None
    expectancy: float | None
    calibration_error: float | None
    train_time_ms: float
    infer_time_ms: float
    model_size_bytes: int
    composite_score: float


def build_comparison_table(results: list[dict]) -> list[dict]:
    table = []
    for r in results:
        val = r.get("val_metrics", {})
        trade = r.get("threshold_metrics", {})
        table.append({
            "model": r["name"],
            "roc_auc": val.get("roc_auc"),
            "pr_auc": val.get("pr_auc"),
            "profit_factor": trade.get("profit_factor"),
            "expectancy": trade.get("expectancy"),
            "calibration_error": r.get("calibration", {}).get("calibration_error"),
            "calibration_method": r.get("calibration_method"),
            "train_time_ms": r.get("train_time_ms"),
            "infer_time_ms": r.get("infer_time_ms"),
            "model_size_kb": round(r.get("model_size_bytes", 0) / 1024, 2),
        })
    return table


def select_champion(results: list[dict]) -> dict:
    if not results:
        raise ValueError("No models to compare")

    scored: list[tuple[float, dict]] = []
    for r in results:
        val = r.get("val_metrics", {})
        roc = val.get("roc_auc") or 0.0
        ece = r.get("calibration", {}).get("calibration_error") or 0.5
        pf = r.get("threshold_metrics", {}).get("profit_factor") or 0.0
        composite = roc * 0.5 + min(pf, 5.0) / 5.0 * 0.3 + (1.0 - min(ece, 1.0)) * 0.2
        scored.append((composite, r))

    scored.sort(key=lambda x: x[0], reverse=True)
    champion = scored[0][1]
    champion["composite_score"] = round(scored[0][0], 4)
    champion["selection_reason"] = "Highest composite: 50% ROC-AUC + 30% PF + 20% calibration"
    return champion
