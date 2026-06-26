"""Ultron ML constants. Feature names are loaded from schema/*.json (exported from FeatureSchema.cs)."""

from __future__ import annotations

import sys
from pathlib import Path

_SCHEMA_DIR = Path(__file__).resolve().parents[2] / "schema"
if str(_SCHEMA_DIR) not in sys.path:
    sys.path.insert(0, str(_SCHEMA_DIR))

import ultron_schema  # noqa: E402

_schema = ultron_schema.get_feature_schema()
ultron_schema.validate_schema_consistency()

FEATURE_SCHEMA_VERSION = int(_schema["schemaVersion"])
FEATURE_COUNT = int(_schema["featureCount"])
FEATURE_NAMES = list(ultron_schema.get_feature_names())

SCORE_VERSION = 1
ULTRON_VERSION = "1.0.0"
