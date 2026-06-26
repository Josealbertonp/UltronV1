"""CLI — ML Filter dashboard."""

from __future__ import annotations

import argparse
from pathlib import Path

from ultronresearch.filter.analysis import analyze_ml_filter, load_shadow_predictions
from ultronresearch.filter.dashboard import render_filter_dashboard


def main() -> None:
    parser = argparse.ArgumentParser(description="Ultron ML Filter dashboard")
    parser.add_argument("--shadow-file", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, default=Path("research_output"))
    args = parser.parse_args()

    df = load_shadow_predictions(args.shadow_file)
    analysis = analyze_ml_filter(df)
    path = render_filter_dashboard(analysis, args.output_dir)
    print(f"ML Filter dashboard: {path}")


if __name__ == "__main__":
    main()
