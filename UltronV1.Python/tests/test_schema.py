"""Tests for shared schema constants."""

from __future__ import annotations

import json
from pathlib import Path

from ultron_data.constants import FEATURE_COUNT, FEATURE_NAMES


def test_feature_count_is_40() -> None:
    assert FEATURE_COUNT == 40
    assert len(FEATURE_NAMES) == 40


def test_feature_names_match_committed_schema_json() -> None:
    repo_root = Path(__file__).resolve().parents[2]
    schema_path = repo_root / "schema" / "feature_names.json"
    payload = json.loads(schema_path.read_text(encoding="utf-8"))

    assert payload["featureCount"] == 40
    assert list(FEATURE_NAMES) == payload["names"]
