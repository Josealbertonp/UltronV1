# ML_CHAMPION_REPORT — Ultron Production Training

**Generated:** 2026-06-26T20:15:55.055623+00:00
**Dataset:** `dataset_v6_sel` (1632 trades)
**Champion model:** CatBoost
**Artifacts:** `C:\Users\alber\Downloads\nightranger v4.2.12\UltronV1\UltronTraining\trained_models\champion_candidate`

> NÃO promovido automaticamente — candidate only.

---

## Etapa 1 — Sessões descobertas

| Métrica | Valor |
|---------|-------|
| Pastas de sessão | 30 |
| Sessões válidas | 30 |
| Sessões com outcomes | 19 |
| Sessões corrompidas | 0 |
| Trades (outcomes) | 1632 |
| Winners | 488 |
| Losers | 1144 |
| Período | 2026-06-10 02:10:00+00:00 → 2026-06-22 10:05:00+00:00 |
| Dias únicos | 11 |

## Etapa 2 — Dataset

- Parquet: `UltronML/datasets/dataset_v6_sel.parquet`
- Labels OK: `True`
- Features: None
- NaN ratio: None
- Constant features: 0

## Etapa 3 — Feature engineering

- {"low_variance_removed": ["archetype", "feat_AbsorptionDetected", "feat_AdxRising", "feat_ErRising", "feat_FreshBreakoutLong", "feat_FreshBreakoutShort", "feat_IcebergRefills", "feat_SweepDetected", "feat_SweepTicks", "regime", "tier"]}
- {"duplicates_removed": ["feat_TrendAlignedLong", "feat_TrendAlignedShort", "gate_ADX_long", "gate_ChopFilter_long", "gate_ChopFilter_short", "gate_EMA_long", "gate_EMA_short", "gate_ER_long", "gate_ER_short", "gate_TrendAcceleration_long", "gate_TrendAcceleration_short", "gate_Wyckoff_long", "gate_Wyckoff_short", "gates_long_allowed", "gates_short_allowed"]}
- {"high_correlation_removed": ["feat_AtrTicks", "feat_ContinuationShort", "feat_DonchianUpper", "feat_PriceAboveEmaFast", "gate_ADX_short", "long_score", "short_score"]}

### Top 15 — Mutual Information

- `feat_VolRatio`: 0.467671
- `feat_Atr`: 0.457208
- `feat_Rvol`: 0.439823
- `feat_EmaSpreadTicks`: 0.431142
- `feat_Rsi`: 0.406239
- `feat_Er`: 0.383826
- `feat_Adx`: 0.377143
- `feat_VwapDistanceTicks`: 0.333674
- `feat_BarRangeTicks`: 0.294571
- `feat_DonchianLower`: 0.215045
- `feat_TickCount`: 0.044274
- `score`: 0.040442
- `feat_ChopFlips`: 0.034509
- `feat_Delta`: 0.023757
- `feat_PullbackShort`: 0.020308

## Etapa 4–6 — Modelos, calibração, SHAP

| Modelo | Val ROC-AUC | Test ROC-AUC |
|--------|-------------|--------------|
| LogisticRegression | 0.6268 | None |
| RandomForest | 0.5725 | None |
| XGBoost | 0.4615 | None |
| LightGBM | 0.5336 | None |
| CatBoost | 0.6009 | None |

**Calibração escolhida:** platt
**Brier (cal):** 0.2339

### Top 30 SHAP (fatores de vitória)

- `feat_VwapDistanceTicks`: 0.45671
- `feat_Adx`: 0.391481
- `feat_Rsi`: 0.371089
- `feat_VolRatio`: 0.260193
- `feat_BarRangeTicks`: 0.232662
- `hour`: 0.208332
- `feat_Atr`: 0.167332
- `feat_Er`: 0.16564
- `feat_Rvol`: 0.153007
- `feat_EmaSpreadTicks`: 0.14714
- `feat_DonchianLower`: 0.137703
- `minute`: 0.110044
- `feat_WyckoffBlockShort`: 0.091475
- `day_of_week`: 0.084623
- `score`: 0.081789
- `feat_ChopFlips`: 0.078167
- `feat_Delta`: 0.044201
- `feat_AdxStrong`: 0.029309
- `feat_TickCount`: 0.024615
- `feat_SellVolume`: 0.024138
- `feat_BuyVolume`: 0.020584
- `feat_BidAskPressure`: 0.015333
- `feat_WyckoffBlockLong`: 0.010064
- `order_flow_passed`: 0.008441
- `feat_PullbackLong`: 0.006551
- `feat_PriceAboveVwap`: 0.005635
- `feat_PullbackShort`: 0.00398
- `feat_RecentCrossShort`: 0.00305
- `feat_ContinuationLong`: 0.0
- `feat_MtfEmaRising`: 0.0

**Interpretação:** `feat_VwapDistanceTicks` é o principal driver de P(win) no holdout; features de order-flow, score e regime dominam a explicação local.

## Etapa 7 — Threshold grid

| T | WR | PF | Exp | DD | RF | Blocked | W blk | L blk |
|---|----|----|-----|----|----|---------|-------|-------|
| 0.50 | 0.4462 | 0.8593 | -2.3385 | -264.0 | -0.5758 | 262 | 92 | 170 |
| 0.55 | 0.5 | 1.0667 | 1.0 | -118.0 | 0.1864 | 305 | 110 | 195 |
| 0.60 | 0.5 | 1.0667 | 1.0 | -60.0 | 0.2 | 315 | 115 | 200 |
| 0.65 | 0.0 | None | 0.0 | 0.0 | None | 327 | 121 | 206 |
| 0.70 | 0.0 | None | 0.0 | 0.0 | None | 327 | 121 | 206 |
| 0.75 | 0.0 | None | 0.0 | 0.0 | None | 327 | 121 | 206 |
| 0.80 | 0.0 | None | 0.0 | 0.0 | None | 327 | 121 | 206 |
| 0.85 | 0.0 | None | 0.0 | 0.0 | None | 327 | 121 | 206 |
| 0.90 | 0.0 | None | 0.0 | 0.0 | None | 327 | 121 | 206 |

**Threshold recomendado:** 0.55

## Etapa 9 — RC1 vs Ultron+ML (test set)

{
  "rc1": {
    "threshold": 0.0,
    "trades": 327,
    "win_rate": 0.37,
    "profit_factor": 0.6265,
    "expectancy": -7.0581,
    "max_drawdown_ticks": -2332.0
  },
  "ml_filter": {
    "threshold": 0.55,
    "trades": 22,
    "win_rate": 0.5,
    "profit_factor": 1.0667,
    "expectancy": 1.0,
    "max_drawdown_ticks": -118.0
  },
  "delta_win_rate": 0.13,
  "delta_profit_factor": 0.4402,
  "delta_expectancy": 8.0581,
  "delta_max_drawdown_ticks": 2214.0,
  "trades_blocked": 305,
  "blocked_winners": 110,
  "blocked_losers": 195,
  "blocked_winners_pct": 0.3607,
  "blocked_losers_pct": 0.6393
}

## Respostas finais

1. **Modelo campeão:** CatBoost
2. **ROC AUC (test):** 0.5016
3. **Precision (test):** 0.4462
4. **Recall (test):** 0.2397
5. **Brier Score (test):** 0.2356
6. **Calibration Error (test):** 0.0437
7. **Threshold recomendado:** 0.55
8. **Pronto para Shadow?** Não — validar mais dados/métricas
9. **Pronto para Active?** Não — PF/WR delta insuficiente ou bloqueio de winners alto
10. **Ganho esperado vs RC1:** WR Δ=0.13, PF Δ=0.4402, Exp Δ=8.0581

**Trades bloqueados pelo ML:** 305 (195 ruins, 110 bons)

Pipeline time: 6421 ms