"""Research Lab orchestration pipeline."""

from __future__ import annotations

import time
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from ultronresearch.analytics.atm_analysis import analyze_atm
from ultronresearch.analytics.features_analysis import analyze_features
from ultronresearch.analytics.gates_analysis import analyze_gates
from ultronresearch.analytics.metrics import compute_general_stats
from ultronresearch.analytics.mfe_mae_analysis import analyze_mfe_mae
from ultronresearch.analytics.segment_analysis import (
    analyze_archetypes,
    analyze_score,
    analyze_tiers,
)
from ultronresearch.analytics.time_analysis import analyze_days, analyze_hours
from ultronresearch.benchmark.runner import ResearchBenchmarkRunner
from ultronresearch.config import ResearchConfig
from ultronresearch.dashboard.generator import DashboardGenerator
from ultronresearch.data.loader import DataAnalyzer
from ultronresearch.ml_prep.schemas import build_ml_prep_bundle
from ultronresearch.optimization.threshold_optimizer import run_threshold_optimizer
from ultronresearch.reports.exporter import ReportExporter
from ultronresearch.simulation.monte_carlo import run_monte_carlo
from ultronresearch.simulation.walk_forward import analyze_walk_forward, create_walk_forward_split


@dataclass
class ResearchPipelineResult:
    results: dict[str, Any] = field(default_factory=dict)
    output_paths: dict[str, Path] = field(default_factory=dict)
    elapsed_ms: float = 0.0


class ResearchPipeline:
    def __init__(self, config: ResearchConfig):
        self._config = config

    def run(self) -> ResearchPipelineResult:
        t0 = time.perf_counter()
        cfg = self._config
        out = cfg.resolve_output()
        bench = ResearchBenchmarkRunner()

        load_result = bench.run("data_load", lambda: self._load())
        dataset = load_result.details["dataset"]
        df = dataset.combined

        results: dict[str, Any] = {
            "dataset": {
                "version": dataset.version,
                "source": dataset.source_path,
                "metadata": dataset.metadata,
                "row_count": len(df),
            },
            "benchmarks": {},
        }

        results["general_stats"] = compute_general_stats(df)
        results["hours"] = analyze_hours(df, cfg.min_trades_per_bucket)
        results["days"] = analyze_days(df, cfg.min_trades_per_bucket)
        results["archetypes"] = analyze_archetypes(df, cfg.min_trades_per_bucket)
        results["tiers"] = analyze_tiers(df, cfg.min_trades_per_bucket)
        results["score"] = analyze_score(df, step=cfg.score_step, min_trades=cfg.min_trades_per_bucket)
        results["gates"] = analyze_gates(df, dataset.opportunities)
        results["features"] = analyze_features(df)
        results["mfe_mae"] = analyze_mfe_mae(df)
        results["atm"] = analyze_atm(df, cfg.atm_columns)
        results["monte_carlo"] = run_monte_carlo(df, n_simulations=cfg.monte_carlo_runs)

        split = create_walk_forward_split(
            df,
            cfg.walk_forward_train_ratio,
            cfg.walk_forward_val_ratio,
            cfg.walk_forward_test_ratio,
        )
        results["walk_forward"] = analyze_walk_forward(split)
        results["thresholds"] = run_threshold_optimizer(df, cfg.min_trades_per_bucket)
        results["ml_prep"] = build_ml_prep_bundle(
            results["thresholds"], len(df), dataset.metadata
        )

        exporter = ReportExporter(out)
        paths = exporter.export_all(results, df)
        paths["html_fragment"] = exporter.export_html_fragment(results)

        dashboard_path = DashboardGenerator(out).generate(results, df)
        paths["dashboard"] = dashboard_path

        bench_path = out / "reports" / "benchmarks.json"
        bench.save(bench_path)
        results["benchmarks"] = bench.to_dict()

        elapsed = (time.perf_counter() - t0) * 1000
        results["pipeline"] = {"elapsed_ms": round(elapsed, 2), "stages": list(results.keys())}

        exporter.export_json(results)

        return ResearchPipelineResult(results=results, output_paths=paths, elapsed_ms=elapsed)

    def _load(self):
        analyzer = DataAnalyzer(self._config.datasets_dir)
        dataset = analyzer.load(
            version=self._config.dataset_version,
            opportunities_path=self._config.opportunities_parquet,
            outcomes_path=self._config.outcomes_parquet,
        )
        return dataset
