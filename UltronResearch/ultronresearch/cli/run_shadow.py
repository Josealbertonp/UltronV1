"""CLI — Shadow ML dashboard."""

from __future__ import annotations

import argparse
from pathlib import Path

from ultronresearch.shadow.dashboard import generate_shadow_dashboard


def main() -> None:
    parser = argparse.ArgumentParser(description="Ultron Shadow ML dashboard")
    parser.add_argument("--shadow-file", type=Path, required=True,
                        help="Path to shadow_predictions.jsonl")
    parser.add_argument("--output-dir", type=Path, default=Path("research_output"))
    args = parser.parse_args()

    out = args.output_dir
    path = generate_shadow_dashboard(args.shadow_file, out)
    print(f"Shadow dashboard: {path}")


if __name__ == "__main__":
    main()
