"""Training configuration."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path


@dataclass
class TrainingConfig:
    datasets_dir: Path
    output_dir: Path = Path("trained_models")
    dataset_version: str | None = None
    train_ratio: float = 0.60
    val_ratio: float = 0.20
    test_ratio: float = 0.20
    min_rows: int = 80
    random_seed: int = 42
    walk_forward_folds: int = 3
    rolling_window_size: int | None = None
    threshold_grid: list[float] = field(
        default_factory=lambda: [round(x * 0.05, 2) for x in range(10, 20)]
    )
    run_walk_forward: bool = True
    run_shap: bool = True
    shap_sample_size: int = 200
    model_names: list[str] | None = None
    threshold_on_validation: bool = True  # RC2 Phase 5: never tune threshold on test

    def resolve_output(self) -> Path:
        self.output_dir.mkdir(parents=True, exist_ok=True)
        return self.output_dir
