# Ultron Training — RC2 Phase 3

Offline ML training pipeline. Predicts **P(win)** only — never direction.

## Flow

```
Dataset (UltronML parquet)
  → Feature Validation
  → Temporal Split (60/20/20)
  → Train 5 models
  → Calibration (Platt vs Isotonic)
  → Threshold Optimization
  → Model Selection
  → ONNX Export
  → Dashboard
```

## Quick start

```powershell
cd UltronV1\UltronTraining
pip install -e ".[dev]"
ultrontrain --datasets-dir ..\UltronML\datasets --output-dir trained_models
```

## Models trained

- Logistic Regression
- Random Forest
- XGBoost
- LightGBM
- CatBoost

## Output

```
trained_models/
  champion/
    model.onnx
    metadata.json
    feature_list.json
    metrics.json
    calibration.json
    thresholds.json
    training_report.json
  dashboard/index.html
  shap/{model}/
```

**No Core changes. No NinjaTrader integration in this phase.**
