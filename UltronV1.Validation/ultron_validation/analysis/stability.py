"""Stability and overfitting detection."""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
import pandas as pd

from ultron_validation.metrics import trading_metrics_from_df
from ultron_validation.sweep.parameter_sweep import evaluate_params


@dataclass
class StabilityReport:
    temporal_stability: dict = field(default_factory=dict)
    parameter_sensitivity: dict = field(default_factory=dict)
    overfitting: dict = field(default_factory=dict)
    verdict: str = "insufficient_data"


def run_stability_tests(
    ultron_df: pd.DataFrame,
    sweep_report,
) -> StabilityReport:
    report = StabilityReport()
    if ultron_df.empty or "timestampUtc" not in ultron_df.columns:
        return report

    df = ultron_df.sort_values("timestampUtc")
    n = len(df)
    mid = n // 2
    first = df.iloc[:mid]
    second = df.iloc[mid:]

    m1 = trading_metrics_from_df(first[first["decision"] == "Accepted"])
    m2 = trading_metrics_from_df(second[second["decision"] == "Accepted"])
    pf1 = m1.get("profitFactor", 0)
    pf2 = m2.get("profitFactor", 0)
    report.temporal_stability = {
        "firstHalfPF": pf1,
        "secondHalfPF": pf2,
        "pfRatio": float(pf2 / pf1) if pf1 > 0 else 0.0,
        "stable": bool(0.5 <= (pf2 / pf1 if pf1 > 0 else 0) <= 2.0),
    }

    # Parameter sensitivity: perturb baseline ±1 step
    baseline = {"score_min": 55, "rvol_min": 0.44, "delta_min": 0, "adx_min": 20.0}
    base_m = evaluate_params(df, **baseline).metrics
    perturbations = []
    for key, delta in [("score_min", 5), ("rvol_min", 0.05), ("adx_min", 3)]:
        p = dict(baseline)
        p[key] = p[key] + delta
        pm = evaluate_params(df, **p).metrics
        base_pf = base_m.get("profitFactor", 0)
        new_pf = pm.get("profitFactor", 0)
        perturbations.append(abs(new_pf - base_pf) / base_pf if base_pf > 0 else 0)

    report.parameter_sensitivity = {
        "meanPfChangePct": float(np.mean(perturbations)) if perturbations else 0.0,
        "maxPfChangePct": float(np.max(perturbations)) if perturbations else 0.0,
        "robust": bool(np.mean(perturbations) < 0.3) if perturbations else False,
    }

    # Overfitting: in-sample best vs OOS temporal split
    if sweep_report and sweep_report.best_by_pf:
        best_params = sweep_report.best_by_pf.params
        in_sample = evaluate_params(df, **best_params).metrics
        oos = evaluate_params(second, **best_params).metrics
        is_pf = in_sample.get("profitFactor", 0)
        oos_pf = oos.get("profitFactor", 0)
        gap = is_pf - oos_pf
        report.overfitting = {
            "inSamplePF": is_pf,
            "outOfSamplePF": oos_pf,
            "pfGap": float(gap),
            "overfitRisk": "high" if gap > 0.5 and oos_pf < is_pf * 0.6 else "moderate" if gap > 0.3 else "low",
            "robustnessScore": sweep_report.robustness_score,
        }

    report.verdict = _verdict(report)
    return report


def _verdict(report: StabilityReport) -> str:
    ts = report.temporal_stability
    ov = report.overfitting
    if not ts:
        return "insufficient_data"
    if ov.get("overfitRisk") == "high":
        return "overfitting_detected"
    if ts.get("stable") and ov.get("overfitRisk") == "low":
        return "stable_evolution"
    if ts.get("secondHalfPF", 0) > ts.get("firstHalfPF", 0):
        return "improving"
    return "needs_more_data"
