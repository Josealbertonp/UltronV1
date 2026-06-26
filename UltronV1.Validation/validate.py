#!/usr/bin/env python3
"""Ultron V1 Phase 4.5 — Validation Framework CLI."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from ultron_validation.pipeline import ValidationFramework


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Ultron V1 Validation Framework — quantitative Ultron vs NightRanger analysis"
    )
    parser.add_argument(
        "--ultron-dataset",
        type=Path,
        default=None,
        help="Ultron dataset root (sessions/*/opportunities.jsonl)",
    )
    parser.add_argument(
        "--nr-logs",
        type=Path,
        default=None,
        help="NightRanger logs root (NightRanger_trades_*.jsonl)",
    )
    parser.add_argument(
        "--comparison-csv",
        type=Path,
        default=None,
        help="UltronV1_NR_Comparison.csv from EnableComparisonMode",
    )
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("reports"),
        help="Output directory for HTML report and heatmaps",
    )
    parser.add_argument("--synthetic", action="store_true", help="Demo mode with synthetic data")
    parser.add_argument("--mc-sims", type=int, default=2000, help="Monte Carlo simulations")
    args = parser.parse_args(argv)

    framework = ValidationFramework(args.output, mc_simulations=args.mc_sims)
    ultron, nr, comp = framework.load(
        ultron_dataset=args.ultron_dataset,
        nr_logs=args.nr_logs,
        comparison_csv=args.comparison_csv,
        synthetic=args.synthetic or args.ultron_dataset is None,
    )

    mode = "synthetic" if args.synthetic or args.ultron_dataset is None else "live"
    print(f"[Ultron Validation] Mode={mode} | Ultron rows={len(ultron)} | NR trades={len(nr)}")

    if ultron.empty:
        print("[Ultron Validation] No data available.", file=sys.stderr)
        return 1

    bundle = framework.run(ultron, nr, comp, mode=mode)

    summary = {
        "reportPath": str(bundle.report_path),
        "verdict": bundle.stability.verdict,
        "overlap": bundle.comparator.overlap,
        "ultronPF": bundle.comparator.ultron_metrics.get("profitFactor"),
        "nrPF": bundle.comparator.nr_metrics.get("profitFactor"),
        "robustnessScore": bundle.sweep.robustness_score,
        "overfitRisk": bundle.stability.overfitting.get("overfitRisk"),
    }
    summary_path = args.output / "validation_summary.json"
    summary_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")

    print(f"[Ultron Validation] Report: {bundle.report_path}")
    print(f"[Ultron Validation] Verdict: {bundle.stability.verdict}")
    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
