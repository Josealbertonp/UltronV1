"""Shared Ultron V1 Python data infrastructure (schema + JSONL + dataset loader)."""

from ultron_data.constants import (
    FEATURE_COUNT,
    FEATURE_NAMES,
    FEATURE_SCHEMA_VERSION,
    SCORE_VERSION,
    ULTRON_VERSION,
)
from ultron_data.jsonl import iter_jsonl
from ultron_data.loader import (
    feature_matrix,
    flatten_opportunity,
    flatten_outcome,
    load_ultron_sessions,
)

__all__ = [
    "FEATURE_COUNT",
    "FEATURE_NAMES",
    "FEATURE_SCHEMA_VERSION",
    "SCORE_VERSION",
    "ULTRON_VERSION",
    "iter_jsonl",
    "feature_matrix",
    "flatten_opportunity",
    "flatten_outcome",
    "load_ultron_sessions",
]
