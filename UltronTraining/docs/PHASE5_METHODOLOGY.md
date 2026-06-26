# RC2 Phase 5 — Metodologia sem vazamento

## Problema identificado (Phase 3/4)

Na pipeline original, `optimize_thresholds(test_proba, test_pnl)` escolhia o threshold
**no conjunto de teste**. Isso inflava PF/WR reportados e invalidava a comparação RC1 vs ML.

## Novo fluxo obrigatório (Phase 5+)

```
Treino (60%)
    ↓
Validação (20%)  → calibração (Platt/Isotonic) + escolha de threshold
    ↓
Teste (20%)        → somente leitura (métricas finais, sem tuning)
```

## Regras

| Etapa | Permitido | Proibido |
|-------|-----------|----------|
| Treino | fit do modelo | usar val/test |
| Validação | calibração, threshold grid, feature selection | usar test |
| Teste | métricas ROC, Brier, PF/WR com threshold **congelado** | qualquer otimização |

## Configuração

`TrainingConfig.threshold_on_validation = True` (default Phase 5)

## Campos exportados

- `threshold_metrics` — PnL/WR/PF no **validation** com threshold escolhido
- `test_threshold_metrics` — PnL/WR/PF no **test** (read-only)
- `thresholds.tuned_on` — `"validation"`

## Promoção Shadow (Phase 5 critérios)

Todos obrigatórios:

1. ROC AUC test >= 0.60
2. Brier test < champion v1
3. Calibration error test <= v1
4. Walk-forward estável (std ROC AUC < 0.08 entre folds)
5. Threshold tunado somente em validation

## Referência

`ultrontraining/pipeline/training_pipeline.py` — linha `optimize_thresholds(tune_proba, tune_pnl, ...)`
