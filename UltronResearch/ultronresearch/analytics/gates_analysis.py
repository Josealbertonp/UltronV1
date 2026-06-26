"""Gate efficiency analysis."""

from __future__ import annotations

import pandas as pd

from ultronresearch.analytics.metrics import compute_group_metrics


def _gate_columns(df: pd.DataFrame) -> list[str]:
    return sorted(c for c in df.columns if c.startswith("gate_"))


def analyze_gates(df: pd.DataFrame, opportunities: pd.DataFrame | None = None) -> dict:
    """
    Rank gates by how often they block winners vs losers.

    Uses opportunity-level data (including rejected signals) when available.
    """
    opp = opportunities if opportunities is not None and not opportunities.empty else df
    gate_cols = _gate_columns(opp)
    if opp.empty or not gate_cols:
        return {"gates": [], "ranking": [], "note": "No gate_* columns found"}

    pnl_map: dict[str, float] = {}
    win_map: dict[str, bool] = {}
    if "trade_id" in df.columns and "realized_pnl_ticks" in df.columns:
        for _, row in df.iterrows():
            tid = row.get("trade_id")
            if tid is not None:
                pnl_map[str(tid)] = float(row.get("realized_pnl_ticks") or 0)
                win_map[str(tid)] = bool(row.get("is_win", pnl_map[str(tid)] > 0))

    results = []
    for col in gate_cols:
        series = opp[col]
        if series.dtype == bool or series.dropna().isin([True, False]).all():
            blocked = ~series.fillna(True)
        else:
            blocked = series.isna() | (series == False)  # noqa: E712

        blocked_ids = opp.loc[blocked, "trade_id"].astype(str) if "trade_id" in opp.columns else []
        winners_blocked = sum(1 for tid in blocked_ids if win_map.get(tid, False))
        losers_blocked = sum(1 for tid in blocked_ids if tid in win_map and not win_map[tid])

        passed = ~blocked
        passed_grp = opp.loc[passed]
        if "trade_id" in df.columns:
            passed_ids = set(passed_grp["trade_id"].astype(str))
            executed = df[df["trade_id"].astype(str).isin(passed_ids)]
        else:
            executed = df

        passed_metrics = compute_group_metrics(executed)

        results.append({
            "gate_column": col,
            "blocked_count": int(blocked.sum()),
            "winners_blocked": winners_blocked,
            "losers_blocked": losers_blocked,
            "losses_avoided_ratio": round(
                losers_blocked / max(losers_blocked + winners_blocked, 1), 4
            ),
            "winners_lost_ratio": round(
                winners_blocked / max(losers_blocked + winners_blocked, 1), 4
            ),
            "passed_trades": passed_metrics["trades"],
            "passed_profit_factor": passed_metrics["profit_factor"],
            "passed_expectancy": passed_metrics["expectancy"],
        })

    ranking = sorted(
        results,
        key=lambda g: (g["losses_avoided_ratio"], -g["winners_lost_ratio"]),
        reverse=True,
    )

    worst_winner_killer = max(results, key=lambda g: g["winners_blocked"]) if results else None
    best_loss_avoider = max(results, key=lambda g: g["losers_blocked"]) if results else None

    return {
        "gates": results,
        "ranking": ranking,
        "most_winners_blocked": worst_winner_killer,
        "most_losses_avoided": best_loss_avoider,
    }
