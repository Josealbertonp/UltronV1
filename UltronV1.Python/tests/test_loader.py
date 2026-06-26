"""Tests for ultron_data loader and feature matrix."""

from __future__ import annotations

from pathlib import Path

import numpy as np
import pandas as pd
import pytest

from ultron_data.constants import FEATURE_COUNT, FEATURE_NAMES
from ultron_data.loader import (
    feature_matrix,
    flatten_opportunity,
    flatten_outcome,
    load_ultron_sessions,
)


def test_flatten_opportunity_maps_40_feature_columns() -> None:
    features = [float(i) for i in range(FEATURE_COUNT)]
    rec = {
        "signalId": "sig-flat",
        "features": features,
        "gates": {"longAllowed": True, "shortAllowed": False},
        "orderFlow": {"passed": True},
        "versions": {"featureSchema": 1, "score": 1, "ultron": "1.0.0", "config": "1.0.0"},
    }

    row = flatten_opportunity(rec)

    assert len([k for k in row if k.startswith("f_")]) == FEATURE_COUNT
    assert row["f_Delta"] == 0.0
    assert row["f_TickCount"] == 39.0
    assert row["gatesLongAllowed"] is True
    assert row["orderFlowPassed"] is True


def test_flatten_outcome_maps_execution_fields() -> None:
    out = {
        "signalId": "sig-001",
        "direction": "Long",
        "isWin": True,
        "label": 1,
        "rMultiple": 1.07,
        "realizedPnLTicks": 32.0,
        "realizedPnLUsd": 16.0,
        "executionMode": "PaperBarSim",
        "exitReason": "take_profit",
    }

    row = flatten_outcome(out)

    assert row["hasOutcome"] is True
    assert row["label"] == 1
    assert row["isWin"] is True
    assert row["rMultiple"] == 1.07
    assert row["realizedPnLTicks"] == 32.0
    assert row["executionMode"] == "PaperBarSim"
    assert row["exitReason"] == "take_profit"


def test_load_sessions_joins_outcome_by_signal_id(dataset_fixture_root: Path) -> None:
    df = load_ultron_sessions(dataset_fixture_root)

    accepted = df[df["signalId"] == "sig-001"].iloc[0]
    rejected = df[df["signalId"] == "sig-002"].iloc[0]

    assert accepted["hasOutcome"] is True
    assert accepted["rMultiple"] == pytest.approx(1.07)
    assert accepted["label"] == 1
    assert pd.isna(rejected.get("hasOutcome")) or rejected.get("hasOutcome") is not True


def test_load_sessions_empty_root_returns_empty_frame(tmp_path: Path) -> None:
    empty_root = tmp_path / "empty-dataset"
    empty_root.mkdir()

    df = load_ultron_sessions(empty_root)

    assert df.empty


def test_load_sessions_sorts_by_timestamp_utc(dataset_fixture_root: Path) -> None:
    df = load_ultron_sessions(dataset_fixture_root)

    timestamps = df["timestampUtc"].tolist()
    assert timestamps == sorted(timestamps)
    assert df.iloc[0]["signalId"] == "sig-001"
    assert df.iloc[1]["signalId"] == "sig-002"


def test_feature_matrix_shape_and_missing_columns(dataset_fixture_root: Path) -> None:
    df = load_ultron_sessions(dataset_fixture_root)
    matrix = feature_matrix(df)

    assert matrix.shape == (len(df), FEATURE_COUNT)
    assert matrix.dtype == float
    assert matrix[0, 0] == pytest.approx(0.0)
    assert matrix[0, -1] == pytest.approx(39.0)

    incomplete = df.drop(columns=["f_Delta"])
    with pytest.raises(ValueError, match="Missing feature columns"):
        feature_matrix(incomplete)
