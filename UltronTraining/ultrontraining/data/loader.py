"""Load UltronML-exported parquet datasets."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

import pandas as pd

from ultrontraining.constants import LABEL_COLUMN, TIME_COLUMN


@dataclass
class DatasetBundle:
    df: pd.DataFrame
    version: str
    metadata: dict
    source_path: str


class DatasetLoader:
    """Read-only consumer of UltronML datasets/."""

    def __init__(self, datasets_dir: Path | str):
        self._dir = Path(datasets_dir)

    def list_versions(self) -> list[str]:
        return sorted(p.stem for p in self._dir.glob("dataset_v*.parquet"))

    def load(self, version: str | None = None) -> DatasetBundle:
        versions = self.list_versions()
        if version:
            path = self._dir / f"{version}.parquet"
        elif versions:
            path = self._dir / f"{versions[-1]}.parquet"
            version = versions[-1]
        else:
            raise FileNotFoundError(f"No dataset_v*.parquet in {self._dir}")

        if not path.exists():
            raise FileNotFoundError(path)

        df = pd.read_parquet(path)
        if TIME_COLUMN in df.columns:
            df[TIME_COLUMN] = pd.to_datetime(df[TIME_COLUMN], utc=True, errors="coerce")
            df = df.sort_values(TIME_COLUMN).reset_index(drop=True)

        if LABEL_COLUMN not in df.columns and "label" in df.columns:
            df[LABEL_COLUMN] = df["label"].astype(int) == 1
        if LABEL_COLUMN not in df.columns and "realized_pnl_ticks" in df.columns:
            df[LABEL_COLUMN] = df["realized_pnl_ticks"] > 0

        meta_path = self._dir / "metadata.json"
        meta = {}
        if meta_path.exists():
            all_meta = json.loads(meta_path.read_text(encoding="utf-8"))
            meta = all_meta.get(version or "", all_meta)

        return DatasetBundle(
            df=df,
            version=version or path.stem,
            metadata=meta if isinstance(meta, dict) else {},
            source_path=str(path),
        )
