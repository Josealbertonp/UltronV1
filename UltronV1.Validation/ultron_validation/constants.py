"""Shared constants for validation framework."""

from __future__ import annotations

# Counterfactual sweep parameters (mirrors UltronConfig defaults)
DEFAULT_SCORE_MIN = 55
DEFAULT_RVOL_MIN = 0.44
DEFAULT_DELTA_MIN = 0
DEFAULT_ADX_MIN = 20.0

SWEEP_SCORE_RANGE = (45, 70, 5)      # start, stop (exclusive step end), step
SWEEP_RVOL_RANGE = (0.30, 0.70, 0.05)
SWEEP_DELTA_RANGE = (0, 300, 50)
SWEEP_ADX_RANGE = (15, 30, 5)

MATCH_TYPES = ("Both", "UltronOnly", "NROnly", "Conflict", "Neither")

# Join tolerance for timestamp alignment (minutes)
TIMESTAMP_TOLERANCE_MIN = 6
