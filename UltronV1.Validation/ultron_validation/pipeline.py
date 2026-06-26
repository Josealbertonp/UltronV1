"""Phase 4.5 validation orchestrator."""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

import pandas as pd

from ultron_validation.analysis.heatmaps import generate_heatmaps
from ultron_validation.analysis.monte_carlo import monte_carlo_comparison
from ultron_validation.analysis.stability import run_stability_tests
from ultron_validation.analysis.walk_forward_report import run_walk_forward_report
from ultron_validation.backtest.comparator import run_comparator
from ultron_validation.data.nr_loader import load_nr_trades, load_nr_trades_csv, load_synthetic_nr_trades
from ultron_validation.data.ultron_loader import load_comparison_csv, load_synthetic_ultron, load_ultron_sessions
from ultron_validation.report.html_report import generate_html_report
from ultron_validation.sweep.parameter_sweep import run_parameter_sweep


@dataclass
class ValidationBundle:
    ultron_df: pd.DataFrame
    nr_trades: pd.DataFrame
    comparator: object = None
    sweep: object = None
    walk_forward: object = None
    monte_carlo: dict = field(default_factory=dict)
    stability: object = None
    heatmap_paths: dict = field(default_factory=dict)
    report_path: Path | None = None
    mode: str = "live"


class ValidationFramework:
    """Full Phase 4.5 pipeline — no Ultron engine logic modified."""

    def __init__(self, output_dir: Path, mc_simulations: int = 2000):
        self.output_dir = output_dir
        self.mc_simulations = mc_simulations
        self.output_dir.mkdir(parents=True, exist_ok=True)

    def load(
        self,
        *,
        ultron_dataset: Path | None,
        nr_logs: Path | None,
        comparison_csv: Path | None,
        synthetic: bool,
    ) -> tuple[pd.DataFrame, pd.DataFrame, pd.DataFrame | None]:
        if synthetic or ultron_dataset is None:
            ultron = load_synthetic_ultron()
            nr = load_synthetic_nr_trades(ultron)
            comp = None
            return ultron, nr, comp

        ultron = load_ultron_sessions(ultron_dataset)
        if ultron.empty:
            ultron = load_synthetic_ultron()
            nr = load_synthetic_nr_trades(ultron)
            return ultron, nr, None

        nr = pd.DataFrame()
        if nr_logs and nr_logs.exists():
            nr = load_nr_trades(nr_logs)
            if nr.empty:
                nr = load_nr_trades_csv(nr_logs)

        comp = load_comparison_csv(comparison_csv) if comparison_csv else None
        if nr.empty and comp is None:
            nr = load_synthetic_nr_trades(ultron)

        return ultron, nr, comp

    def run(
        self,
        ultron_df: pd.DataFrame,
        nr_trades: pd.DataFrame,
        comparison_csv: pd.DataFrame | None = None,
        mode: str = "live",
    ) -> ValidationBundle:
        bundle = ValidationBundle(ultron_df=ultron_df, nr_trades=nr_trades, mode=mode)

        bundle.comparator = run_comparator(ultron_df, nr_trades, comparison_csv)
        bundle.sweep = run_parameter_sweep(ultron_df)
        bundle.walk_forward = run_walk_forward_report(ultron_df, nr_trades)
        bundle.monte_carlo = monte_carlo_comparison(ultron_df, nr_trades, self.mc_simulations)
        bundle.stability = run_stability_tests(ultron_df, bundle.sweep)

        heatmap_dir = self.output_dir / "heatmaps"
        bundle.heatmap_paths = generate_heatmaps(bundle.sweep.grid_df, heatmap_dir)

        report_path = self.output_dir / "validation_report.html"
        bundle.report_path = generate_html_report(
            comp=bundle.comparator,
            sweep=bundle.sweep,
            wf=bundle.walk_forward,
            mc=bundle.monte_carlo,
            stability=bundle.stability,
            heatmap_paths=bundle.heatmap_paths,
            output_path=report_path,
            mode=mode,
            row_count=len(ultron_df),
        )
        return bundle
