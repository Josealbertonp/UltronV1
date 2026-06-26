"""CLI — offline training."""

from __future__ import annotations

import argparse
from pathlib import Path

from ultrontraining.config import TrainingConfig
from ultrontraining.pipeline.training_pipeline import TrainingPipeline


def main() -> None:
    parser = argparse.ArgumentParser(description="Ultron Training — RC2 Phase 3")
    parser.add_argument("--datasets-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, default=Path("trained_models"))
    parser.add_argument("--version", type=str, default=None, help="dataset_vN")
    parser.add_argument("--min-rows", type=int, default=80)
    parser.add_argument("--monte-carlo-runs", type=int, default=0, help="unused, compat")
    parser.add_argument("--models", type=str, default=None, help="Comma-separated model names")
    parser.add_argument("--no-shap", action="store_true")
    parser.add_argument("--no-walk-forward", action="store_true")
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[2]
    output = args.output_dir if args.output_dir.is_absolute() else root / args.output_dir
    model_names = [m.strip() for m in args.models.split(",")] if args.models else None

    pipeline = TrainingPipeline(
        TrainingConfig(
            datasets_dir=args.datasets_dir,
            output_dir=output,
            dataset_version=args.version,
            min_rows=args.min_rows,
            model_names=model_names,
            run_shap=not args.no_shap,
            run_walk_forward=not args.no_walk_forward,
        )
    )
    result = pipeline.run()

    print(f"Training complete in {result.elapsed_ms:.0f} ms")
    print(f"Champion: {result.champion['name']}")
    print(f"ROC-AUC (val): {result.champion.get('val_metrics', {}).get('roc_auc')}")
    print(f"Artifacts: {result.output_dir / 'champion'}")
    print(f"Dashboard: {result.output_dir / 'dashboard' / 'index.html'}")


if __name__ == "__main__":
    main()
