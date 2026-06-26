"""Tests for Validation backtest comparator."""

from __future__ import annotations

import pandas as pd

from ultron_validation.backtest.comparator import join_ultron_nr


def _ultron_row(signal_id: str, ts: str, direction: str = "Long") -> dict:
    return {
        "signalId": signal_id,
        "timestampUtc": pd.Timestamp(ts, tz="UTC"),
        "decision": "Accepted",
        "direction": direction,
        "score": 60,
        "rMultiple": 1.0,
    }


def _nr_row(ts: str, direction: str = "Long", profit_ticks: float = 8.0) -> dict:
    return {
        "timestampUtc": pd.Timestamp(ts, tz="UTC"),
        "direction": direction,
        "score": 55,
        "profitTicks": profit_ticks,
    }


def test_comparator_detects_both_match() -> None:
    ultron = pd.DataFrame([_ultron_row("u1", "2026-06-01T14:30:00Z")])
    nr = pd.DataFrame([_nr_row("2026-06-01T14:32:00Z")])

    joined = join_ultron_nr(ultron, nr)

    assert (joined["matchType"] == "Both").any()
    both = joined[joined["matchType"] == "Both"].iloc[0]
    assert both["ultronDir"] == "Long"
    assert both["nrDir"] == "Long"


def test_comparator_detects_ultron_only() -> None:
    ultron = pd.DataFrame([_ultron_row("u1", "2026-06-01T14:30:00Z")])
    nr = pd.DataFrame([_nr_row("2026-06-01T15:00:00Z")])

    joined = join_ultron_nr(ultron, nr)

    assert (joined["matchType"] == "UltronOnly").any()
    assert not (joined["matchType"] == "Both").any()


def test_comparator_detects_nr_only() -> None:
    ultron = pd.DataFrame([_ultron_row("u1", "2026-06-01T14:30:00Z")])
    nr = pd.DataFrame([_nr_row("2026-06-01T15:00:00Z")])

    joined = join_ultron_nr(ultron, nr)

    assert (joined["matchType"] == "NROnly").any()
    assert (joined["matchType"] == "UltronOnly").any()
