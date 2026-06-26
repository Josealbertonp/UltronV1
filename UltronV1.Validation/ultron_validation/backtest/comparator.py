"""Backtest comparator — Ultron vs NightRanger."""

from __future__ import annotations

from dataclasses import dataclass, field

import numpy as np
import pandas as pd

from ultron_validation.constants import TIMESTAMP_TOLERANCE_MIN
from ultron_validation.metrics import overlap_summary, trading_metrics_from_df, trading_metrics_from_returns


@dataclass
class ComparatorReport:
    total_bars: int = 0
    overlap: dict = field(default_factory=dict)
    timing: dict = field(default_factory=dict)
    ultron_metrics: dict = field(default_factory=dict)
    nr_metrics: dict = field(default_factory=dict)
    both_metrics: dict = field(default_factory=dict)
    ultron_only_metrics: dict = field(default_factory=dict)
    nr_only_metrics: dict = field(default_factory=dict)
    score_delta_when_both: dict = field(default_factory=dict)
    joined_rows: int = 0
    comparison_df: pd.DataFrame | None = None


def _norm_dir(val) -> str:
    v = str(val).strip()
    if v in ("Long", "1", "BUY", "Buy"):
        return "Long"
    if v in ("Short", "-1", "SELL", "Sell"):
        return "Short"
    return "None"


def build_signal_frame(ultron_df: pd.DataFrame) -> pd.DataFrame:
    df = ultron_df.copy()
    df["ultronDir"] = df.apply(_ultron_direction, axis=1)
    df["ultronAccepted"] = df["decision"] == "Accepted"
    return df


def _ultron_direction(row) -> str:
    if row.get("decision") != "Accepted":
        return "None"
    return _norm_dir(row.get("direction", "None"))


def join_ultron_nr(
    ultron_df: pd.DataFrame,
    nr_trades: pd.DataFrame,
    tolerance_min: int = TIMESTAMP_TOLERANCE_MIN,
) -> pd.DataFrame:
    """Temporal join: each Ultron accepted signal ↔ nearest NR trade."""
    signals = build_signal_frame(ultron_df)
    accepted = signals[signals["ultronAccepted"]].copy()
    if accepted.empty or nr_trades.empty:
        return _build_from_comparison_only(signals, pd.DataFrame())

    nr = nr_trades.copy()
    nr["nrDir"] = nr["direction"].apply(_norm_dir)

    rows = []
    tol = pd.Timedelta(minutes=tolerance_min)
    for _, sig in accepted.iterrows():
        ts = sig["timestampUtc"]
        window = nr[(nr["timestampUtc"] >= ts - tol) & (nr["timestampUtc"] <= ts + tol)]
        if window.empty:
            rows.append({**sig.to_dict(), "nrDir": "None", "matchType": "UltronOnly", "nrScore": np.nan, "profitTicks": np.nan, "deltaMinutes": np.nan})
            continue

        window = window.copy()
        window["deltaMinutes"] = (window["timestampUtc"] - ts).dt.total_seconds() / 60.0
        best = window.iloc[window["deltaMinutes"].abs().argsort().iloc[0]]
        u_dir = sig["ultronDir"]
        n_dir = best["nrDir"]
        if u_dir == n_dir:
            match = "Both"
        elif n_dir == "None":
            match = "UltronOnly"
        else:
            match = "Conflict"

        rows.append({
            **sig.to_dict(),
            "nrDir": n_dir,
            "matchType": match,
            "nrScore": best.get("score"),
            "profitTicks": best.get("profitTicks"),
            "deltaMinutes": best["deltaMinutes"],
            "nrEntryTime": best.get("timestampUtc"),
        })

    joined = pd.DataFrame(rows)

    # NR-only: trades without matching Ultron signal
    matched_nr_times = set(joined["nrEntryTime"].dropna().astype(str).tolist()) if "nrEntryTime" in joined.columns else set()
    for _, trade in nr.iterrows():
        ts_str = str(trade["timestampUtc"])
        if ts_str not in matched_nr_times:
            near = accepted[
                (accepted["timestampUtc"] >= trade["timestampUtc"] - tol)
                & (accepted["timestampUtc"] <= trade["timestampUtc"] + tol)
            ]
            if near.empty:
                rows.append({
                    "timestampUtc": trade["timestampUtc"],
                    "ultronDir": "None",
                    "nrDir": trade["nrDir"],
                    "matchType": "NROnly",
                    "score": np.nan,
                    "nrScore": trade.get("score"),
                    "profitTicks": trade.get("profitTicks"),
                    "deltaMinutes": np.nan,
                })

    return pd.DataFrame(rows) if rows else joined


def _build_from_comparison_only(signals: pd.DataFrame, comparison_csv: pd.DataFrame) -> pd.DataFrame:
    if comparison_csv.empty:
        accepted = signals[signals["ultronAccepted"]]
        return accepted.assign(matchType="UltronOnly", nrDir="None")

    comp = comparison_csv.copy()
    if "UltronDir" in comp.columns:
        comp["ultronDir"] = comp["UltronDir"].apply(_norm_dir)
    if "NRDir" in comp.columns:
        comp["nrDir"] = comp["NRDir"].apply(_norm_dir)
    if "matchType" not in comp.columns and "MatchType" in comp.columns:
        comp["matchType"] = comp["MatchType"]
    return comp


def run_comparator(
    ultron_df: pd.DataFrame,
    nr_trades: pd.DataFrame,
    comparison_csv: pd.DataFrame | None = None,
) -> ComparatorReport:
    report = ComparatorReport(total_bars=len(ultron_df))

    if comparison_csv is not None and not comparison_csv.empty:
        comp_df = _build_from_comparison_only(build_signal_frame(ultron_df), comparison_csv)
    else:
        comp_df = join_ultron_nr(ultron_df, nr_trades)

    report.comparison_df = comp_df
    report.joined_rows = len(comp_df)
    report.overlap = overlap_summary(comp_df)

    both = comp_df[comp_df["matchType"] == "Both"]
    u_only = comp_df[comp_df["matchType"] == "UltronOnly"]
    n_only = comp_df[comp_df["matchType"] == "NROnly"]

    report.ultron_metrics = trading_metrics_from_df(
        comp_df[comp_df["ultronDir"] != "None"].assign(rMultiple=comp_df.get("rMultiple"))
    )
    report.nr_metrics = trading_metrics_from_df(nr_trades, "profitTicks")
    report.both_metrics = trading_metrics_from_df(both, "profitTicks") if "profitTicks" in both.columns else trading_metrics_from_df(both)
    report.ultron_only_metrics = trading_metrics_from_df(u_only)
    report.nr_only_metrics = trading_metrics_from_df(n_only, "profitTicks") if "profitTicks" in n_only.columns else trading_metrics_from_df(n_only)

    if not both.empty and "score" in both.columns and "nrScore" in both.columns:
        delta = both["score"].astype(float) - both["nrScore"].astype(float)
        report.score_delta_when_both = {
            "mean": float(delta.mean()),
            "median": float(delta.median()),
            "std": float(delta.std()) if len(delta) > 1 else 0.0,
        }

    if "deltaMinutes" in comp_df.columns:
        dm = comp_df["deltaMinutes"].dropna()
        if not dm.empty:
            report.timing = {
                "ultronBeforeNrPct": float((dm < 0).mean()),
                "sameBarPct": float((dm.abs() < 0.1).mean()),
                "nrBeforeUltronPct": float((dm > 0).mean()),
                "meanDeltaMinutes": float(dm.mean()),
                "medianDeltaMinutes": float(dm.median()),
            }

    return report
