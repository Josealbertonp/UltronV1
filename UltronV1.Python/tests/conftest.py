"""Shared pytest fixtures for Ultron Python tests."""

from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

TESTS_DIR = Path(__file__).resolve().parent
REPO_ROOT = TESTS_DIR.parents[1]
TRAINING_ROOT = REPO_ROOT / "UltronV1.Training"
VALIDATION_ROOT = REPO_ROOT / "UltronV1.Validation"
SCHEMA_FIXTURE = TESTS_DIR / "fixtures" / "schema" / "feature_names.json"
DATASET_FIXTURE = TESTS_DIR / "fixtures" / "dataset"


def _ensure_import_paths() -> None:
    for path in (str(TRAINING_ROOT), str(VALIDATION_ROOT)):
        if path not in sys.path:
            sys.path.insert(0, path)


_ensure_import_paths()


@pytest.fixture
def dataset_fixture_root() -> Path:
    return DATASET_FIXTURE


@pytest.fixture
def schema_fixture_path() -> Path:
    return SCHEMA_FIXTURE


@pytest.fixture
def schema_fixture_names() -> tuple[str, ...]:
    payload = json.loads(SCHEMA_FIXTURE.read_text(encoding="utf-8"))
    return tuple(payload["names"])
