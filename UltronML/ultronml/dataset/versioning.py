"""Dataset versioning and export to parquet + metadata."""

from __future__ import annotations

import json
import subprocess
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path

import pandas as pd

from ultronml.constants import DATASET_SCHEMA_VERSION, ULTRONML_VERSION
from ultronml.dataset.builder import DatasetBuilder
from ultronml.features.validator import FeatureValidator, ValidationReport


@dataclass
class DatasetExportResult:
    version: str
    parquet_path: Path
    metadata_path: Path
    row_count: int
    validation: ValidationReport


@dataclass
class DatasetMetadata:
    version: str
    created_at_utc: str
    git_commit: str
    ultronml_version: str
    dataset_schema_version: int
    trade_count: int
    win_count: int
    loss_count: int
    breakeven_count: int
    feature_columns: list[str]
    label_columns: list[str]
    parquet_file: str
    validation_passed: bool
    extra: dict = field(default_factory=dict)

    def to_dict(self) -> dict:
        return {
            "version": self.version,
            "createdAtUtc": self.created_at_utc,
            "gitCommit": self.git_commit,
            "ultronmlVersion": self.ultronml_version,
            "datasetSchemaVersion": self.dataset_schema_version,
            "tradeCount": self.trade_count,
            "winCount": self.win_count,
            "lossCount": self.loss_count,
            "breakevenCount": self.breakeven_count,
            "features": self.feature_columns,
            "labels": self.label_columns,
            "parquetFile": self.parquet_file,
            "validationPassed": self.validation_passed,
            **self.extra,
        }


class DatasetVersionManager:
    """Export versioned datasets under datasets/dataset_vN.parquet + metadata.json."""

    def __init__(self, output_dir: Path | str):
        self._output_dir = Path(output_dir)
        self._output_dir.mkdir(parents=True, exist_ok=True)

    def next_version_name(self) -> str:
        existing = sorted(self._output_dir.glob("dataset_v*.parquet"))
        if not existing:
            return "dataset_v1"
        last = existing[-1].stem
        try:
            n = int(last.replace("dataset_v", ""))
        except ValueError:
            n = len(existing)
        return f"dataset_v{n + 1}"

    def export(
        self,
        df: pd.DataFrame,
        *,
        version: str | None = None,
        validate: bool = True,
    ) -> DatasetExportResult:
        version = version or self.next_version_name()
        parquet_path = self._output_dir / f"{version}.parquet"
        metadata_path = self._output_dir / "metadata.json"

        report = FeatureValidator().validate(df) if validate else ValidationReport.skipped(len(df))

        df.to_parquet(parquet_path, index=False)

        feat_cols = DatasetBuilder.feature_columns(df)
        label_cols = [c for c in df.columns if c in {
            "mfe_ticks", "mae_ticks", "realized_pnl_ticks", "is_win", "is_loss",
            "is_breakeven", "label", "bars_in_trade", "r_multiple",
        }]

        meta = DatasetMetadata(
            version=version,
            created_at_utc=datetime.now(timezone.utc).isoformat(),
            git_commit=_resolve_git_commit(),
            ultronml_version=ULTRONML_VERSION,
            dataset_schema_version=DATASET_SCHEMA_VERSION,
            trade_count=len(df),
            win_count=int(df["is_win"].sum()) if "is_win" in df.columns else 0,
            loss_count=int(df["is_loss"].sum()) if "is_loss" in df.columns else 0,
            breakeven_count=int(df["is_breakeven"].sum()) if "is_breakeven" in df.columns else 0,
            feature_columns=feat_cols,
            label_columns=label_cols,
            parquet_file=parquet_path.name,
            validation_passed=report.passed,
            extra={"validation": report.to_dict()},
        )

        history = _load_metadata_history(metadata_path)
        history[version] = meta.to_dict()
        metadata_path.write_text(json.dumps(history, indent=2), encoding="utf-8")

        return DatasetExportResult(
            version=version,
            parquet_path=parquet_path,
            metadata_path=metadata_path,
            row_count=len(df),
            validation=report,
        )


def _load_metadata_history(path: Path) -> dict:
    if path.exists():
        return json.loads(path.read_text(encoding="utf-8"))
    return {}


def _resolve_git_commit() -> str:
    try:
        root = Path(__file__).resolve().parents[2]
        out = subprocess.check_output(
            ["git", "rev-parse", "--short", "HEAD"],
            cwd=root,
            stderr=subprocess.DEVNULL,
            text=True,
        )
        return out.strip()
    except (subprocess.CalledProcessError, FileNotFoundError, OSError):
        return "unknown"
