"""UltronML tests."""

import pytest
from pathlib import Path

FIXTURE_ROOT = Path(__file__).resolve().parents[2] / "UltronV1.Python" / "tests" / "fixtures" / "dataset"


@pytest.fixture
def fixture_dataset_root():
    return FIXTURE_ROOT


def test_dataset_builder_loads(fixture_dataset_root):
    from ultronml.dataset.builder import DatasetBuilder

    df = DatasetBuilder(fixture_dataset_root).build()
    assert len(df) >= 1
    assert "trade_id" in df.columns
    assert "feat_Delta" in df.columns


def test_validator_passes_fixture(fixture_dataset_root):
    from ultronml.dataset.builder import DatasetBuilder
    from ultronml.features.validator import FeatureValidator

    df = DatasetBuilder(fixture_dataset_root).build(with_outcomes_only=True)
    report = FeatureValidator().validate(df)
    assert report.row_count >= 1


def test_dummy_provider_confidence():
    from ultronml.config import MlFilterConfig

    cfg = MlFilterConfig()
    assert cfg.enable_ml_filter is False
    assert cfg.is_active() is False
