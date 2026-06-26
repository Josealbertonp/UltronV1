"""Shared constants for UltronML."""

from __future__ import annotations

import json
from pathlib import Path

ULTRONML_VERSION = "2.0.0-rc2"
DATASET_SCHEMA_VERSION = 1

_ULTRONV1_ROOT = Path(__file__).resolve().parents[2]
_SCHEMA_PATH = _ULTRONV1_ROOT / "schema" / "feature_names.json"


def load_feature_names() -> list[str]:
    if not _SCHEMA_PATH.exists():
        raise FileNotFoundError(f"Feature schema not found: {_SCHEMA_PATH}")
    data = json.loads(_SCHEMA_PATH.read_text(encoding="utf-8"))
    return list(data["names"])


FEATURE_NAMES = load_feature_names()
FEATURE_COUNT = len(FEATURE_NAMES)

# Columns that are labels / post-entry — never used as model inputs at inference.
LABEL_COLUMNS = frozenset({
    "mfe_ticks",
    "mae_ticks",
    "realized_pnl_ticks",
    "realized_pnl_usd",
    "r_multiple",
    "bars_in_trade",
    "seconds_in_trade",
    "exit_reason",
    "is_win",
    "is_loss",
    "is_breakeven",
    "label",
    "entry_price",
    "exit_price",
    "stop_price",
    "target_price",
})
