"""Load UltronML-exported datasets (parquet + metadata)."""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path

import pandas as pd


@dataclass
class LoadedDataset:
    """Combined research dataframe plus optional split views."""

    combined: pd.DataFrame
    opportunities: pd.DataFrame
    outcomes: pd.DataFrame
    metadata: dict = field(default_factory=dict)
    version: str = ""
    source_path: str = ""


class DataAnalyzer:
    """
    Reads versioned datasets from UltronML export directory.

    Supports:
    - dataset_vN.parquet (primary)
    - opportunities.parquet + outcomes.parquet (optional split)
    - metadata.json history
    """

    OPP_PREFIX_EXCLUDE = {
        "mfe_ticks", "mae_ticks", "realized_pnl_ticks", "realized_pnl_usd",
        "r_multiple", "bars_in_trade", "seconds_in_trade", "exit_reason",
        "is_win", "is_loss", "is_breakeven", "label", "has_outcome",
        "entry_price", "exit_price", "stop_price", "target_price",
        "execution_mode",
    }

    def __init__(self, datasets_dir: Path | str):
        self._dir = Path(datasets_dir)

    def list_versions(self) -> list[str]:
        return sorted(p.stem for p in self._dir.glob("dataset_v*.parquet"))

    def load_metadata(self) -> dict:
        path = self._dir / "metadata.json"
        if not path.exists():
            return {}
        return json.loads(path.read_text(encoding="utf-8"))

    def resolve_parquet_path(self, version: str | None = None) -> Path:
        if version:
            path = self._dir / f"{version}.parquet"
            if path.exists():
                return path
            raise FileNotFoundError(f"Dataset version not found: {path}")

        versions = self.list_versions()
        if not versions:
            raise FileNotFoundError(f"No dataset_v*.parquet in {self._dir}")
        return self._dir / f"{versions[-1]}.parquet"

    def load(
        self,
        *,
        version: str | None = None,
        opportunities_path: Path | None = None,
        outcomes_path: Path | None = None,
    ) -> LoadedDataset:
        meta_history = self.load_metadata()

        if opportunities_path and outcomes_path:
            opp = pd.read_parquet(opportunities_path)
            out = pd.read_parquet(outcomes_path)
            combined = self._join_opp_out(opp, out)
            version_name = version or "split"
            source = str(opportunities_path)
        else:
            parquet_path = self.resolve_parquet_path(version)
            combined = pd.read_parquet(parquet_path)
            version_name = parquet_path.stem
            source = str(parquet_path)
            opp_path = self._dir / "opportunities.parquet"
            out_path = self._dir / "outcomes.parquet"
            if opp_path.exists() and out_path.exists():
                opp = pd.read_parquet(opp_path)
                out = pd.read_parquet(out_path)
            else:
                opp, out = self._split_combined(combined)

        meta = meta_history.get(version_name, {})
        combined = self._normalize(combined)
        opp = self._normalize(opp)
        out = self._normalize(out)

        return LoadedDataset(
            combined=combined,
            opportunities=opp,
            outcomes=out,
            metadata=meta,
            version=version_name,
            source_path=source,
        )

    def _normalize(self, df: pd.DataFrame) -> pd.DataFrame:
        if df.empty:
            return df
        out = df.copy()
        if "timestamp_utc" in out.columns:
            out["timestamp_utc"] = pd.to_datetime(out["timestamp_utc"], utc=True, errors="coerce")
        if "hour" not in out.columns and "timestamp_utc" in out.columns:
            out["hour"] = out["timestamp_utc"].dt.hour
        if "day_of_week" not in out.columns and "timestamp_utc" in out.columns:
            out["day_of_week"] = out["timestamp_utc"].dt.dayofweek
        if "is_win" not in out.columns and "realized_pnl_ticks" in out.columns:
            out["is_win"] = out["realized_pnl_ticks"] > 0
        return out

    def _split_combined(self, df: pd.DataFrame) -> tuple[pd.DataFrame, pd.DataFrame]:
        label_cols = [c for c in df.columns if c in self.OPP_PREFIX_EXCLUDE]
        opp_cols = [c for c in df.columns if c not in label_cols or c == "trade_id"]
        out_cols = ["trade_id"] + [c for c in label_cols if c in df.columns]
        if "trade_id" not in df.columns:
            df = df.reset_index().rename(columns={"index": "trade_id"})
        return df[opp_cols].copy(), df[out_cols].copy()

    @staticmethod
    def _join_opp_out(opp: pd.DataFrame, out: pd.DataFrame) -> pd.DataFrame:
        key = "trade_id"
        if key not in opp.columns:
            raise ValueError("opportunities parquet missing trade_id")
        if key not in out.columns:
            raise ValueError("outcomes parquet missing trade_id")
        return opp.merge(out, on=key, how="inner", suffixes=("", "_out"))
