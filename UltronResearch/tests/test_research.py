"""UltronResearch infrastructure tests."""

from pathlib import Path

import pytest

from ultronresearch.analytics.metrics import compute_general_stats
from ultronresearch.data.loader import DataAnalyzer
from ultronresearch.pipeline.research_pipeline import ResearchPipeline
from ultronresearch.config import ResearchConfig


@pytest.fixture
def fixture_datasets_dir(tmp_path, build_fixture):
    return build_fixture


def test_data_analyzer_loads(fixture_datasets_dir):
    loaded = DataAnalyzer(fixture_datasets_dir).load()
    assert len(loaded.combined) >= 50
    assert "score" in loaded.combined.columns


def test_general_stats(fixture_datasets_dir):
    df = DataAnalyzer(fixture_datasets_dir).load().combined
    stats = compute_general_stats(df)
    assert stats["trades"] == len(df)
    assert 0 <= stats["win_rate"] <= 1


def test_full_pipeline(fixture_datasets_dir, tmp_path):
    out = tmp_path / "out"
    result = ResearchPipeline(
        ResearchConfig(
            datasets_dir=fixture_datasets_dir,
            output_dir=out,
            monte_carlo_runs=100,
            min_trades_per_bucket=3,
        )
    ).run()
    assert result.results["general_stats"]["trades"] > 0
    assert (out / "dashboard" / "index.html").exists()
    assert (out / "reports" / "research_results.json").exists()
