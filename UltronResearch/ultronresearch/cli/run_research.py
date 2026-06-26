"""CLI entry point."""

from __future__ import annotations

import argparse
from pathlib import Path

from ultronresearch.config import ResearchConfig
from ultronresearch.pipeline.research_pipeline import ResearchPipeline


def main() -> None:
    parser = argparse.ArgumentParser(description="Ultron Research Lab — RC2 Phase 2")
    parser.add_argument(
        "--datasets-dir",
        type=Path,
        required=True,
        help="UltronML datasets directory (dataset_v*.parquet + metadata.json)",
    )
    parser.add_argument("--output-dir", type=Path, default=Path("research_output"))
    parser.add_argument("--version", type=str, default=None, help="dataset_vN name")
    parser.add_argument("--monte-carlo-runs", type=int, default=5000)
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[2]
    output = args.output_dir if args.output_dir.is_absolute() else root / args.output_dir

    pipeline = ResearchPipeline(
        ResearchConfig(
            datasets_dir=args.datasets_dir,
            output_dir=output,
            dataset_version=args.version,
            monte_carlo_runs=args.monte_carlo_runs,
        )
    )
    result = pipeline.run()

    print(f"Research complete in {result.elapsed_ms:.0f} ms")
    print(f"Trades analyzed: {result.results.get('general_stats', {}).get('trades', 0)}")
    for name, path in result.output_paths.items():
        print(f"  {name}: {path}")


if __name__ == "__main__":
    main()
