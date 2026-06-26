"""In-memory tests for ValidationFramework pipeline (no CLI/subprocess)."""

from __future__ import annotations

from pathlib import Path

from ultron_validation.pipeline import ValidationFramework

REQUIRED_SUMMARY_KEYS = (
    "reportPath",
    "verdict",
    "overlap",
    "ultronPF",
    "nrPF",
    "robustnessScore",
    "overfitRisk",
)


def _summary_from_bundle(bundle) -> dict:
    return {
        "reportPath": str(bundle.report_path),
        "verdict": bundle.stability.verdict,
        "overlap": bundle.comparator.overlap,
        "ultronPF": bundle.comparator.ultron_metrics.get("profitFactor"),
        "nrPF": bundle.comparator.nr_metrics.get("profitFactor"),
        "robustnessScore": bundle.sweep.robustness_score,
        "overfitRisk": bundle.stability.overfitting.get("overfitRisk"),
    }


def _run_synthetic_pipeline(tmp_path: Path):
    framework = ValidationFramework(tmp_path, mc_simulations=100)
    ultron, nr, comp = framework.load(
        ultron_dataset=None,
        nr_logs=None,
        comparison_csv=None,
        synthetic=True,
    )
    bundle = framework.run(ultron, nr, comp, mode="synthetic")
    return bundle, _summary_from_bundle(bundle)


def test_validation_pipeline_returns_summary_for_synthetic_dataset(tmp_path: Path) -> None:
    bundle, summary = _run_synthetic_pipeline(tmp_path)

    assert bundle.comparator is not None
    assert bundle.sweep is not None
    assert bundle.stability is not None
    assert bundle.report_path is not None
    assert bundle.report_path.exists()
    assert len(bundle.ultron_df) > 0
    assert len(bundle.nr_trades) > 0
    assert isinstance(summary["verdict"], str)
    assert summary["verdict"]


def test_validation_summary_contains_required_metrics(tmp_path: Path) -> None:
    _, summary = _run_synthetic_pipeline(tmp_path)

    for key in REQUIRED_SUMMARY_KEYS:
        assert key in summary, f"missing summary key: {key}"

    assert isinstance(summary["overlap"], dict)
    assert summary["ultronPF"] is not None
    assert summary["nrPF"] is not None
    assert isinstance(summary["robustnessScore"], (int, float))
    assert summary["overfitRisk"] is not None
