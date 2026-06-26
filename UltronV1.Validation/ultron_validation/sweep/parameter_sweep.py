"""Counterfactual parameter sweep on recorded opportunities."""

from __future__ import annotations

from dataclasses import dataclass, field
from itertools import product

import numpy as np
import pandas as pd

from ultron_validation.constants import (
    DEFAULT_ADX_MIN,
    DEFAULT_DELTA_MIN,
    DEFAULT_RVOL_MIN,
    DEFAULT_SCORE_MIN,
    SWEEP_ADX_RANGE,
    SWEEP_DELTA_RANGE,
    SWEEP_RVOL_RANGE,
    SWEEP_SCORE_RANGE,
)
from ultron_validation.metrics import trading_metrics_from_df


@dataclass
class SweepResult:
    params: dict
    accepted_count: int
    metrics: dict


@dataclass
class ParameterSweepReport:
    baseline: SweepResult
    results: list[SweepResult] = field(default_factory=list)
    grid_df: pd.DataFrame = field(default_factory=pd.DataFrame)
    best_by_pf: SweepResult | None = None
    robustness_score: float = 0.0


def _range(start, stop, step):
    vals = []
    v = start
    while v < stop + 1e-9:
        vals.append(round(v, 4) if isinstance(step, float) else int(v))
        v += step
    return vals


def apply_thresholds(
    df: pd.DataFrame,
    score_min: float,
    rvol_min: float,
    delta_min: float,
    adx_min: float,
) -> pd.DataFrame:
    """Counterfactual accept filter — does not re-run engine."""
    work = df.copy()
    score = work[["longScore", "shortScore"]].max(axis=1) if "longScore" in work.columns else work.get("score", 0)
    work["_effScore"] = score

    mask = pd.Series(True, index=work.index)
    if "gatesLongAllowed" in work.columns:
        mask &= work["gatesLongAllowed"] | work["gatesShortAllowed"]
    mask &= work["_effScore"] >= score_min

    if "f_Rvol" in work.columns:
        mask &= work["f_Rvol"].astype(float) >= rvol_min
    if "f_Delta" in work.columns:
        mask &= work["f_Delta"].astype(float).abs() >= delta_min
    if "f_Adx" in work.columns:
        mask &= work["f_Adx"].astype(float) >= adx_min

    work["counterfactualAccepted"] = mask
    return work[mask]


def evaluate_params(df: pd.DataFrame, **params) -> SweepResult:
    accepted = apply_thresholds(df, **params)
    metrics = trading_metrics_from_df(accepted)
    metrics["acceptRate"] = float(len(accepted) / len(df)) if len(df) else 0.0
    return SweepResult(params=params, accepted_count=len(accepted), metrics=metrics)


def run_parameter_sweep(
    df: pd.DataFrame,
    *,
    score_range=SWEEP_SCORE_RANGE,
    rvol_range=SWEEP_RVOL_RANGE,
    delta_range=SWEEP_DELTA_RANGE,
    adx_range=SWEEP_ADX_RANGE,
    max_combos: int = 500,
) -> ParameterSweepReport:
    baseline_params = {
        "score_min": DEFAULT_SCORE_MIN,
        "rvol_min": DEFAULT_RVOL_MIN,
        "delta_min": DEFAULT_DELTA_MIN,
        "adx_min": DEFAULT_ADX_MIN,
    }
    baseline = evaluate_params(df, **baseline_params)

    scores = _range(*score_range)
    rvols = _range(*rvol_range)
    deltas = _range(*delta_range)
    adxs = _range(*adx_range)

    grid = list(product(scores, rvols, deltas, adxs))
    if len(grid) > max_combos:
        rng = np.random.default_rng(42)
        idx = rng.choice(len(grid), max_combos, replace=False)
        grid = [grid[i] for i in sorted(idx)]

    results = []
    rows = []
    for s, r, d, a in grid:
        params = {"score_min": s, "rvol_min": r, "delta_min": d, "adx_min": a}
        res = evaluate_params(df, **params)
        results.append(res)
        rows.append({**params, **{f"m_{k}": v for k, v in res.metrics.items()}, "accepted": res.accepted_count})

    grid_df = pd.DataFrame(rows)
    best = max(results, key=lambda x: x.metrics.get("profitFactor", 0)) if results else None

    # Robustness: % of grid with PF within 80% of best
    if best and results:
        best_pf = best.metrics.get("profitFactor", 0)
        threshold = best_pf * 0.8 if best_pf > 0 else 0
        robust = sum(1 for r in results if r.metrics.get("profitFactor", 0) >= threshold)
        robustness = robust / len(results) if results else 0.0
    else:
        robustness = 0.0

    return ParameterSweepReport(
        baseline=baseline,
        results=results,
        grid_df=grid_df,
        best_by_pf=best,
        robustness_score=float(robustness),
    )
