"""Export dataset CLI."""

from __future__ import annotations

import argparse
from pathlib import Path

from ultronml.dataset.builder import DatasetBuilder
from ultronml.dataset.versioning import DatasetVersionManager
from ultronml.training.pipeline import TrainingPipeline, TrainingPipelineConfig


def main() -> None:
    parser = argparse.ArgumentParser(description="Export Ultron ML dataset (RC2 Phase 1)")
    parser.add_argument(
        "--dataset-root",
        type=Path,
        required=True,
        help="Path to UltronV1/dataset (contains sessions/)",
    )
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("datasets"),
        help="Output directory for parquet + metadata",
    )
    parser.add_argument("--accepted-only", action="store_true", default=True)
    parser.add_argument("--with-outcomes-only", action="store_true", default=True)
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[2]
    output = args.output_dir if args.output_dir.is_absolute() else root / args.output_dir

    pipeline = TrainingPipeline(
        TrainingPipelineConfig(
            dataset_root=args.dataset_root,
            output_dir=output,
            models_dir=root / "models",
            accepted_only=args.accepted_only,
            with_outcomes_only=args.with_outcomes_only,
        )
    )
    result = pipeline.run()

    if result.skipped_reason:
        print(f"SKIPPED: {result.skipped_reason}")
        print(f"Stages: {result.stages_completed}")
        return

    exp = result.export
    assert exp is not None
    print(f"Exported {exp.version}: {exp.row_count} rows")
    print(f"Parquet: {exp.parquet_path}")
    print(f"Metadata: {exp.metadata_path}")
    print(f"Validation passed: {exp.validation.passed}")


if __name__ == "__main__":
    main()
