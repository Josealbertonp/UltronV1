"""Feature importance report schema — calculation deferred to training phase."""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path


@dataclass
class FeatureImportanceEntry:
    feature: str
    importance: float
    rank: int
    method: str = "pending"

    def to_dict(self) -> dict:
        return {
            "feature": self.feature,
            "importance": self.importance,
            "rank": self.rank,
            "method": self.method,
        }


@dataclass
class FeatureImportanceReport:
    model_version: str
    created_at_utc: str
    method: str
    entries: list[FeatureImportanceEntry] = field(default_factory=list)
    status: str = "placeholder"

    def to_dict(self) -> dict:
        return {
            "modelVersion": self.model_version,
            "createdAtUtc": self.created_at_utc,
            "method": self.method,
            "status": self.status,
            "entries": [e.to_dict() for e in self.entries],
        }

    def save(self, path: Path | str) -> None:
        Path(path).write_text(json.dumps(self.to_dict(), indent=2), encoding="utf-8")

    @classmethod
    def placeholder(cls, model_version: str = "none") -> FeatureImportanceReport:
        return cls(
            model_version=model_version,
            created_at_utc=datetime.now(timezone.utc).isoformat(),
            method="none",
            status="placeholder",
        )
