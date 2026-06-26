"""Build tabular ML datasets from Ultron JSONL (opportunities + outcomes)."""

from __future__ import annotations

from pathlib import Path

import pandas as pd

from ultronml.constants import FEATURE_COUNT, FEATURE_NAMES
from ultronml.io.jsonl import iter_jsonl


class DatasetBuilder:
    """
    Join opportunities.jsonl + outcomes.jsonl into one row per trade/signal.

  Pre-entry fields only influence feature columns. MFE/MAE/PnL are label columns.
    """

    def __init__(self, dataset_root: Path | str):
        self._root = Path(dataset_root)

    def build(self, *, accepted_only: bool = False, with_outcomes_only: bool = False) -> pd.DataFrame:
        sessions_dir = self._root / "sessions"
        rows: list[dict] = []

        if not sessions_dir.exists():
            return pd.DataFrame()

        for session_dir in sorted(sessions_dir.iterdir()):
            if not session_dir.is_dir():
                continue
            opp_path = session_dir / "opportunities.jsonl"
            out_path = session_dir / "outcomes.jsonl"
            outcomes = {o["signalId"]: o for o in iter_jsonl(out_path) if o.get("signalId")}

            for rec in iter_jsonl(opp_path):
                row = self._flatten_opportunity(rec, session_dir.name)
                oid = row.get("trade_id")
                if oid and oid in outcomes:
                    row.update(self._flatten_outcome_labels(outcomes[oid]))
                elif with_outcomes_only:
                    continue
                if accepted_only and row.get("decision") != "Accepted":
                    continue
                rows.append(row)

        if not rows:
            return pd.DataFrame()

        df = pd.DataFrame(rows)
        if "timestamp_utc" in df.columns:
            df["timestamp_utc"] = pd.to_datetime(df["timestamp_utc"], utc=True)
            df = df.sort_values("timestamp_utc").reset_index(drop=True)
        return df

    def _flatten_opportunity(self, rec: dict, session_id: str) -> dict:
        ts = rec.get("timestampUtc")
        gates = rec.get("gates") or {}
        of = rec.get("orderFlow") or {}
        versions = rec.get("versions") or {}
        shadow = rec.get("shadow") or {}

        row: dict = {
            "trade_id": rec.get("signalId"),
            "session": session_id,
            "timestamp_utc": ts,
            "bar_index": rec.get("barIndex"),
            "instrument": rec.get("instrument"),
            "direction": rec.get("direction"),
            "score": rec.get("score"),
            "long_score": rec.get("longScore"),
            "short_score": rec.get("shortScore"),
            "tier": rec.get("tier"),
            "archetype": rec.get("archetype"),
            "regime": rec.get("regime"),
            "decision": rec.get("decision"),
            "rejection_stage": rec.get("rejectionStage"),
            "rejection_reason": rec.get("rejectionReason"),
            "close": rec.get("close"),
            "gates_long_allowed": gates.get("longAllowed"),
            "gates_short_allowed": gates.get("shortAllowed"),
            "gates_block_reason": gates.get("blockReason"),
            "order_flow_passed": of.get("passed"),
            "order_flow_reason": of.get("reason"),
            "order_flow_detail": of.get("detail"),
            "ultron_version": versions.get("ultron"),
            "feature_schema_version": versions.get("featureSchema"),
            "score_version": versions.get("score"),
            "config_version": versions.get("config"),
            "score_profile_hash": versions.get("scoreProfileHash"),
            "shadow_rvol": shadow.get("rvolAtEvaluation"),
            "shadow_rvol_low_would_block": shadow.get("rvolLowWouldBlock"),
            "has_outcome": False,
        }

        if ts:
            try:
                dt = pd.to_datetime(ts, utc=True)
                row["date"] = dt.date().isoformat()
                row["time"] = dt.time().isoformat()
                row["hour"] = int(dt.hour)
                row["minute"] = int(dt.minute)
                row["day_of_week"] = int(dt.dayofweek)
            except (ValueError, TypeError):
                pass

        features = rec.get("features") or []
        for i, name in enumerate(FEATURE_NAMES):
            key = self._feature_col(name)
            row[key] = float(features[i]) if i < len(features) else None

        for gate in gates.get("checks") or []:
            gname = gate.get("name", "gate")
            row[f"gate_{gname}_long"] = gate.get("longPass")
            row[f"gate_{gname}_short"] = gate.get("shortPass")

        for part in rec.get("scoreBreakdown") or []:
            pname = part.get("name", "score")
            safe = "".join(c if c.isalnum() else "_" for c in str(pname))
            row[f"score_contrib_{safe}"] = part.get("points")
            row[f"score_applied_{safe}"] = part.get("applied")

        flags = shadow.get("flags") or []
        for i, flag in enumerate(flags):
            row[f"feature_flag_{i}"] = flag

        return row

    @staticmethod
    def _flatten_outcome_labels(out: dict) -> dict:
        pnl = float(out.get("realizedPnLTicks") or 0)
        is_win = bool(out.get("isWin"))
        is_loss = not is_win and pnl < 0
        is_be = not is_win and abs(pnl) < 0.01
        return {
            "has_outcome": True,
            "label": int(out.get("label", 1 if is_win else 0)),
            "is_win": is_win,
            "is_loss": is_loss,
            "is_breakeven": is_be,
            "entry_price": out.get("entryPrice"),
            "exit_price": out.get("exitPrice"),
            "stop_price": out.get("stopPrice"),
            "target_price": out.get("targetPrice"),
            "mfe_ticks": out.get("mfeTicks"),
            "mae_ticks": out.get("maeTicks"),
            "r_multiple": out.get("rMultiple"),
            "realized_pnl_ticks": pnl,
            "realized_pnl_usd": out.get("realizedPnLUsd"),
            "bars_in_trade": out.get("barsInTrade"),
            "seconds_in_trade": out.get("secondsInTrade"),
            "exit_reason": out.get("exitReason"),
            "execution_mode": out.get("executionMode"),
        }

    @staticmethod
    def _feature_col(name: str) -> str:
        return "feat_" + name

    @staticmethod
    def feature_columns(df: pd.DataFrame) -> list[str]:
        return [c for c in df.columns if c.startswith("feat_")]

    @staticmethod
    def expected_feature_count() -> int:
        return FEATURE_COUNT
