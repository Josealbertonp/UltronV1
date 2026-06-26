"""Column conventions for UltronML-exported datasets."""

from __future__ import annotations

LABEL_COLUMNS = frozenset({
    "mfe_ticks", "mae_ticks", "realized_pnl_ticks", "realized_pnl_usd",
    "r_multiple", "bars_in_trade", "seconds_in_trade", "exit_reason",
    "is_win", "is_loss", "is_breakeven", "label",
    "entry_price", "exit_price", "stop_price", "target_price",
    "has_outcome",
})

PNL_COLUMN = "realized_pnl_ticks"
WIN_COLUMN = "is_win"
TIER_ORDER = ["A+", "A", "B", "C", "D"]
DAY_NAMES = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"]
