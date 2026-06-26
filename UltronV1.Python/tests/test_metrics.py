"""Tests for Training and Validation metric helpers."""

from __future__ import annotations

import numpy as np
import pandas as pd
import pytest

from ultron_ml.metrics.calculator import trading_metrics
from ultron_validation.metrics import overlap_summary, trading_metrics_from_returns


def test_training_trading_metrics_profit_factor_golden() -> None:
    df = pd.DataFrame({"rMultiple": [2.0, 1.0, -1.0, -0.5]})

    metrics = trading_metrics(df)

    assert metrics["tradeCount"] == 4
    assert metrics["profitFactor"] == pytest.approx(2.0)
    assert metrics["winRate"] == pytest.approx(0.5)


def test_training_trading_metrics_expectancy_golden() -> None:
    df = pd.DataFrame({"rMultiple": [2.0, 1.0, -1.0, -0.5]})

    metrics = trading_metrics(df)

    assert metrics["expectancy"] == pytest.approx(0.375)
    assert metrics["avgWinR"] == pytest.approx(1.5)
    assert metrics["avgLossR"] == pytest.approx(-0.75)


def test_training_trading_metrics_empty_returns_zeros() -> None:
    metrics = trading_metrics(pd.DataFrame())

    assert metrics["tradeCount"] == 0
    assert metrics["profitFactor"] == 0.0
    assert metrics["expectancy"] == 0.0
    assert metrics["winRate"] == 0.0


def test_validation_trading_metrics_from_returns_golden() -> None:
    returns = np.array([2.0, 1.0, -1.0, -0.5], dtype=float)

    metrics = trading_metrics_from_returns(returns)

    assert metrics["tradeCount"] == 4
    assert metrics["profitFactor"] == pytest.approx(2.0)
    assert metrics["expectancy"] == pytest.approx(0.375)
    assert metrics["totalReturn"] == pytest.approx(1.5)
    assert metrics["maxDrawdown"] <= 0.0


def test_validation_overlap_summary_counts_match_types() -> None:
    df = pd.DataFrame(
        {
            "matchType": [
                "Both",
                "Both",
                "UltronOnly",
                "NROnly",
                "Conflict",
            ]
        }
    )

    summary = overlap_summary(df)

    assert summary["Both"]["count"] == 2
    assert summary["UltronOnly"]["count"] == 1
    assert summary["NROnly"]["count"] == 1
    assert summary["Conflict"]["count"] == 1
    assert summary["Both"]["pct"] == pytest.approx(0.4)
