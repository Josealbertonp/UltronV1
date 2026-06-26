"""Walk-forward validation reports for Ultron vs NR."""

from __future__ import annotations

from dataclasses import dataclass, field

import pandas as pd

from ultron_validation.backtest.comparator import run_comparator
from ultron_validation.metrics import trading_metrics_from_df


@dataclass
class WalkForwardWindow:
    window: int
    train_start: str
    train_end: str
    test_start: str
    test_end: str
    ultron_metrics: dict
    nr_metrics: dict
    overlap: dict
    pf_delta: float  # ultron - nr


@dataclass
class WalkForwardReport:
    windows: list[WalkForwardWindow] = field(default_factory=list)
    aggregate: dict = field(default_factory=dict)


def run_walk_forward_report(
    ultron_df: pd.DataFrame,
    nr_trades: pd.DataFrame,
    *,
    train_days: int = 5,
    test_days: int = 2,
    step_days: int = 2,
) -> WalkForwardReport:
    if "timestampUtc" not in ultron_df.columns or ultron_df.empty:
        return WalkForwardReport()

    df = ultron_df.sort_values("timestampUtc").copy()
    start = df["timestampUtc"].min()
    end = df["timestampUtc"].max()
    report = WalkForwardReport()
    pf_deltas = []

    window_idx = 0
    cursor = start
    while cursor + pd.Timedelta(days=train_days + test_days) <= end:
        train_end = cursor + pd.Timedelta(days=train_days)
        test_end = train_end + pd.Timedelta(days=test_days)

        test_ultron = df[(df["timestampUtc"] >= train_end) & (df["timestampUtc"] < test_end)]
        test_nr = nr_trades[
            (nr_trades["timestampUtc"] >= train_end) & (nr_trades["timestampUtc"] < test_end)
        ] if not nr_trades.empty else pd.DataFrame()

        if len(test_ultron) < 10:
            cursor += pd.Timedelta(days=step_days)
            continue

        comp = run_comparator(test_ultron, test_nr)
        u_acc = test_ultron[test_ultron["decision"] == "Accepted"]
        u_met = trading_metrics_from_df(u_acc)
        n_met = trading_metrics_from_df(test_nr, "profitTicks")
        pf_delta = u_met.get("profitFactor", 0) - n_met.get("profitFactor", 0)
        pf_deltas.append(pf_delta)

        report.windows.append(WalkForwardWindow(
            window=window_idx,
            train_start=str(cursor),
            train_end=str(train_end),
            test_start=str(train_end),
            test_end=str(test_end),
            ultron_metrics=u_met,
            nr_metrics=n_met,
            overlap=comp.overlap,
            pf_delta=pf_delta,
        ))
        window_idx += 1
        cursor += pd.Timedelta(days=step_days)

    if pf_deltas:
        import numpy as np
        report.aggregate = {
            "pfDeltaMean": float(np.mean(pf_deltas)),
            "pfDeltaStd": float(np.std(pf_deltas)),
            "ultronWinsPct": float(sum(1 for d in pf_deltas if d > 0) / len(pf_deltas)),
            "windowsCompleted": len(pf_deltas),
        }
    return report
