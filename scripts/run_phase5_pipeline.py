#!/usr/bin/env python3
"""RC2 Phase 5 — Feature Engineering & ML Improvement (offline only)."""

from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "UltronTraining"))

from ultrontraining.pipeline.phase5_pipeline import Phase5Pipeline


def main() -> None:
    datasets_dir = ROOT / "UltronML" / "datasets"
    output_dir = ROOT / "UltronTraining" / "trained_models"
    v1_dir = output_dir / "champion_candidate"

    pipeline = Phase5Pipeline(
        root=ROOT,
        datasets_dir=datasets_dir,
        output_dir=output_dir,
        v1_dir=v1_dir,
        source_parquet="dataset_v6.parquet",
    )
    result = pipeline.run()
    print(f"Phase 5 complete in {result.elapsed_ms:.0f} ms")
    print(f"Champion v2: {result.champion_v2.get('name')} -> {result.output_dir}")
    print(f"Shadow ready: {result.promotion.get('ready_for_shadow')}")
    print(f"Report: {ROOT / 'RC2_PHASE5_FEATURE_ENGINEERING_REPORT.md'}")


if __name__ == "__main__":
    main()
