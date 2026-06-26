#!/usr/bin/env python3
"""Ultron V1 ML training CLI — reproducible, auditable, no auto-promotion."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from ultron_ml.training.trainer import UltronTrainer


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Ultron V1 ML Training Platform")
    parser.add_argument(
        "--dataset",
        type=Path,
        default=None,
        help="Path to UltronV1/dataset root (sessions/*/opportunities.jsonl)",
    )
    parser.add_argument(
        "--registry",
        type=Path,
        default=Path("registry"),
        help="Model registry root (models/ + registry.json)",
    )
    parser.add_argument(
        "--synthetic",
        action="store_true",
        help="Use synthetic data (for pipeline validation without NT8 dataset)",
    )
    parser.add_argument("--seed", type=int, default=42)
    parser.add_argument("--oos-ratio", type=float, default=0.2)
    args = parser.parse_args(argv)

    trainer = UltronTrainer(args.registry, random_seed=args.seed)
    df = trainer.load_data(args.dataset, use_synthetic=args.synthetic or args.dataset is None)

    source = "synthetic" if args.synthetic or args.dataset is None else str(args.dataset)
    print(f"[Ultron ML] Loading data from {source} — {len(df)} rows")

    try:
        result = trainer.train(df, dataset_source=source, oos_ratio=args.oos_ratio)
    except Exception as exc:
        print(f"[Ultron ML] Training failed: {exc}", file=sys.stderr)
        return 1

    print(f"[Ultron ML] Model trained: {result.model_id}")
    print(f"[Ultron ML] Artifacts: {result.model_dir}")
    print("[Ultron ML] Status: trained_not_promoted (manual promotion only)")
    print(json.dumps(
        {
            "modelId": result.model_id,
            "auc": result.metrics.get("auc"),
            "f1": result.metrics.get("f1"),
            "profitFactor": result.metrics.get("profitFactor"),
            "winRate": result.metrics.get("winRate"),
            "tradeCount": result.metrics.get("tradeCount"),
            "tierDistribution": result.metrics.get("tierDistribution"),
        },
        indent=2,
    ))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
