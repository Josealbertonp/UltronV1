# Ultron V1 — ML Training Platform (Fase 4)

Plataforma reproduzível de Machine Learning para o Ultron V1 (MNQ 09-26).

## Princípios

- **Advisory only** — o MlAdvisor em C# nunca veta sinais; apenas boost/penalty de score
- **Sem auto-promoção** — todo treinamento gera `status: trained_not_promoted`
- **Métricas antes da exportação** — ONNX só é gerado após validação completa
- **Reproducibilidade** — cada modelo possui pasta própria com 6 artefatos obrigatórios

## Artefatos por modelo

```
registry/models/ultron_mnq_v1_YYYYMMDD_HHMMSS/
├── model.onnx
├── metrics.json
├── config.json
├── feature_schema.json
├── training_manifest.json
└── feature_importance.json
```

## Instalação

```powershell
cd UltronV1.Training
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

## Treinar (dados sintéticos — validação do pipeline)

```powershell
python train.py --synthetic --registry ./registry
```

## Treinar (dataset NT8)

```powershell
python train.py --dataset "$env:USERPROFILE\Documents\NinjaTrader 8\UltronV1\dataset" --registry ./registry
```

## Pipeline executado

1. Feature Validation
2. Temporal Cross-Validation
3. Walk-Forward Validation
4. Out-of-Sample Evaluation
5. Métricas completas (AUC, Precision, Recall, F1, PF, Expectancy, Win Rate, trades, tiers)
6. Feature Importance + SHAP
7. Export ONNX + manifest
8. Registro no Model Registry (**sem promoção automática**)

## Promoção manual

```python
from pathlib import Path
from ultron_ml.registry.model_registry import ModelRegistry

registry = ModelRegistry(Path("./registry"))
registry.promote_model("ultron_mnq_v1_YYYYMMDD_HHMMSS")
```

## Integração C# (MlAdvisor)

```csharp
var config = UltronConfig.Default;
config.EnableMlAdvisor = true;
config.MlModelDirectory = @"C:\path\to\registry\models\ultron_mnq_v1_...";

using var advisor = new MlAdvisor(config.MlModelDirectory);
var engine = new UltronEngine(config, mlAdvisor: advisor);
```
