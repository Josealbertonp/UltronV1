# UltronML — RC2 Machine Learning Infrastructure

Phase 1: **infrastructure only** — no model training.

## Python package (`ultronml`)

```powershell
cd UltronML
pip install -e ".[dev]"
ultronml-export --dataset-root "$env:USERPROFILE\Documents\NinjaTrader 8\UltronV1\dataset"
```

## C# package (`UltronML.Core`)

Independent of `UltronV1.Core`. Host integration (NinjaTrader) in a future RC2 phase.

```powershell
dotnet build UltronML.Core\UltronML.Core.csproj -c Release
```

## Layout

```
UltronML/
  ultronml/           Python: dataset, validation, training pipeline stub
  UltronML.Core/      C#: IMLProvider, DummyProvider, MLModelLoader
  datasets/           Exported parquet + metadata.json
  models/             Future model.onnx, manifests
  docs/               Architecture documentation
  tests/
```

See `docs/RC2_ML_ARCHITECTURE.md` and `RC2_ML_INFRASTRUCTURE_REPORT.md`.
