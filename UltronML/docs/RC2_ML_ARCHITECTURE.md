# RC2 — ML Architecture (Phase 1)

## Target flow (future integration)

```
Features → Hard Gates → Order Flow → Score → Tier → ML Confidence → Entrada
```

ML is a **confidence filter only**. It never replaces the engine.

## RC1 vs RC2 Phase 1

| Layer | RC1 | RC2 Phase 1 |
|-------|-----|-------------|
| Engine / gates / score | Active | **Unchanged** |
| ML filter | N/A | Infrastructure only |
| `EnableMlFilter` | N/A | Default **false** |
| `DummyProvider` | N/A | Confidence **1.0** (pass-through) |
| Dataset export | JSONL manual | `DatasetBuilder` → parquet |
| Training | UltronV1.Training (separate) | `TrainingPipeline` stub |

## Project boundaries

```
UltronV1.Core          ← authoritative trading logic (NO reference to UltronML)
UltronML               ← NEW — dataset, validation, providers (independent)
UltronV1.Training      ← existing Phase 4 training (unchanged this task)
NinjaTrader host       ← future wiring of MLModelLoader at RC2 Phase 2
```

## Python components

| Module | Role |
|--------|------|
| `dataset/builder.py` | JSONL → tabular, pre-entry features + label columns |
| `dataset/versioning.py` | `dataset_vN.parquet` + `metadata.json` |
| `features/metadata.py` | Per-feature schema registry |
| `features/validator.py` | NaN, inf, duplicates, leakage checks |
| `training/pipeline.py` | Offline orchestration (no train step) |
| `benchmark/runner.py` | Export/inference timing |
| `importance/schema.py` | Feature importance report format |

## C# components (`UltronML.Core`)

| Type | Role |
|------|------|
| `IMLProvider` | Load / Predict / Unload contract |
| `DummyProvider` | Always confidence 1.0 |
| `MLModelLoader` | Version + feature validation, dispatches provider |
| `MlFilterConfig` | `EnableMlFilter` default false |

## Extension points

1. **New provider:** implement `IMLProvider`, register in `MLModelLoader.ResolveProvider`
2. **New features:** update `schema/feature_names.json` + `FeatureMetadataRegistry`
3. **Training:** implement step in `TrainingPipeline` after export
4. **Host integration:** call `MLModelLoader.Predict` after tier check in NinjaTrader (Phase 2)

## Export datasets

```powershell
cd UltronV1\UltronML
pip install -e .
ultronml-export --dataset-root "...\UltronV1\dataset"
```

## Validate datasets

Validation runs automatically before export. Checks:

- Duplicate `trade_id`
- Feature column count (40)
- NaN / Infinity / absurd values
- Label leakage into feature columns

## Train (future)

1. Export dataset_vN.parquet
2. Run trainer (XGBoost/LightGBM) — **not in Phase 1**
3. Export ONNX to `models/`
4. Deploy manifest + `feature_list.json`
5. Set `EnableMlFilter=true` in host config

## Model directory layout

```
models/
  model.onnx          # Phase 2+
  metadata.json
  feature_list.json
  version.json
```

## Leakage policy

**Inference features:** snapshot at signal time only (`feat_*`, score, tier, gates, order flow).

**Labels only:** `mfe_ticks`, `mae_ticks`, `realized_pnl_ticks`, `is_win`, `bars_in_trade`, etc.

Never use labels as model inputs.
