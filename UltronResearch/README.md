# Ultron Research Lab

RC2 Phase 2 — independent research module. Consumes **only** UltronML-exported parquet datasets.

## Architecture

```
UltronCore  →  (no dependency)
UltronML    →  exports dataset_vN.parquet
UltronResearch  →  reads parquet, generates reports & dashboard
```

## Quick start

```powershell
# 1. Export dataset with UltronML (if not done yet)
cd UltronV1\UltronML
pip install -e .
ultronml-export --dataset-root "..\dataset" --output-dir datasets

# 2. Run Research Lab
cd ..\UltronResearch
pip install -e ".[dev]"
ultronresearch --datasets-dir ..\UltronML\datasets --output-dir research_output
```

Open `research_output/dashboard/index.html` in a browser.

## Outputs

| Path | Format |
|------|--------|
| `reports/research_results.json` | Full analysis JSON |
| `reports/research_summary.md` | Markdown summary |
| `reports/segments_summary.csv` | Segment tables |
| `reports/trades_analyzed.parquet` | Analyzed trades |
| `dashboard/index.html` | Interactive HTML dashboard |
| `reports/benchmarks.json` | Timing & memory |

## No training, no Core changes

This package does **not** train ML models and does **not** modify Ultron trading logic.
