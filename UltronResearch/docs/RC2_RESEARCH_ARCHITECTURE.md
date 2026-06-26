# Ultron Research Lab — Architecture

## Dependency flow

```
UltronCore  (no deps on Research)
     ↓ JSONL
UltronML    (exports parquet — no deps on Research)
     ↓ parquet
UltronResearch  (read-only consumer)
```

## Input contract

Research reads from UltronML `datasets/`:

| File | Required | Description |
|------|----------|-------------|
| `dataset_vN.parquet` | Yes* | Combined opportunities + outcomes |
| `metadata.json` | Optional | Version history |
| `opportunities.parquet` | Optional | Split view |
| `outcomes.parquet` | Optional | Split view |

\*Or explicit split parquet pair.

## Extension points

1. **New segment dimension:** add analyzer in `analytics/`, wire in `ResearchPipeline`
2. **New chart:** extend `DashboardGenerator`
3. **New export format:** extend `ReportExporter`
4. **ML training:** use `walk_forward` splits + `ml_prep` schemas in external trainer

## Leakage policy

Research may use labels (MFE, MAE, PnL) for analysis.  
Future ML training must use only pre-entry columns — documented in UltronML.
