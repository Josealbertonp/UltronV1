"""Research Lab configuration."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path


@dataclass
class ResearchConfig:
    """Paths and knobs for a research run. No Core coupling."""

    datasets_dir: Path
    output_dir: Path = Path("research_output")
    dataset_version: str | None = None
    opportunities_parquet: Path | None = None
    outcomes_parquet: Path | None = None
    monte_carlo_runs: int = 5000
    walk_forward_train_ratio: float = 0.60
    walk_forward_val_ratio: float = 0.20
    walk_forward_test_ratio: float = 0.20
    score_step: int = 1
    min_trades_per_bucket: int = 5
    atm_columns: list[str] = field(
        default_factory=lambda: ["atm_template", "atm_profile", "atm_name"]
    )

    def resolve_output(self) -> Path:
        self.output_dir.mkdir(parents=True, exist_ok=True)
        return self.output_dir
