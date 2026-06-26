"""Training constants — no Core import."""

from __future__ import annotations

LABEL_COLUMN = "is_win"
PNL_COLUMN = "realized_pnl_ticks"
TIME_COLUMN = "timestamp_utc"

EXCLUDE_FROM_FEATURES = frozenset({
    "trade_id", "session", "timestamp_utc", "date", "time",
    "instrument", "direction", "decision", "rejection_stage", "rejection_reason",
    "mfe_ticks", "mae_ticks", "realized_pnl_ticks", "realized_pnl_usd",
    "r_multiple", "bars_in_trade", "seconds_in_trade", "exit_reason",
    "is_win", "is_loss", "is_breakeven", "label", "has_outcome",
    "entry_price", "exit_price", "stop_price", "target_price",
    "execution_mode", "close", "bar_index",
    "gates_block_reason", "order_flow_reason", "order_flow_detail",
    "ultron_version", "feature_schema_version", "score_version",
    "config_version", "score_profile_hash",
})
