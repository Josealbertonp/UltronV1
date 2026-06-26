"""Runtime loader for Ultron feature schema artifacts.

Feature names are NOT defined in Python. They are exported from FeatureSchema.cs
via tools/ExportFeatureSchema. See schema/*.json (generated — do not edit manually).
"""

from __future__ import annotations

import json
from functools import lru_cache
from pathlib import Path

_SCHEMA_DIR = Path(__file__).resolve().parent
_FULL_SCHEMA_FILE = "ultron_mnq_feature_schema_v1.json"
_FEATURE_NAMES_FILE = "feature_names.json"


@lru_cache(maxsize=1)
def get_feature_schema() -> dict:
    path = _SCHEMA_DIR / _FULL_SCHEMA_FILE
    with path.open("r", encoding="utf-8") as f:
        return json.load(f)


@lru_cache(maxsize=1)
def get_feature_names() -> tuple[str, ...]:
    path = _SCHEMA_DIR / _FEATURE_NAMES_FILE
    with path.open("r", encoding="utf-8") as f:
        payload = json.load(f)
    names = tuple(payload["names"])
    expected = int(payload.get("featureCount", len(names)))
    if len(names) != expected:
        raise ValueError(f"{_FEATURE_NAMES_FILE}: featureCount={expected} but len(names)={len(names)}")
    return names


def validate_schema_consistency() -> None:
    """Ensure full schema and feature_names.json agree (guards export drift)."""
    schema = get_feature_schema()
    names = get_feature_names()
    features = tuple(schema["features"])
    if features != names:
        raise ValueError("ultron_mnq_feature_schema_v1.json features[] != feature_names.json names[]")
    if int(schema["featureCount"]) != len(names):
        raise ValueError("featureCount mismatch between schema JSON files")
