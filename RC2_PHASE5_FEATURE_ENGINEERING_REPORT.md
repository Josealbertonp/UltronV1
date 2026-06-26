# RC2_PHASE5_FEATURE_ENGINEERING_REPORT

**Generated:** 2026-06-26T20:41:36.132256+00:00
**Dataset:** `dataset_v7_phase5`
**Champion v2:** LightGBM
**Artifacts:** `C:\Users\alber\Downloads\nightranger v4.2.12\UltronV1\UltronTraining\trained_models\champion_candidate_v2`

> NÃO promovido automaticamente.

## Fase 1 — Auditoria

- Features base + derivadas: 98
- Derivadas adicionadas: 34

## Fase 2 — Novas features derivadas

- `der_adx_norm`
- `der_atr_pct_close`
- `der_buy_ratio`
- `der_delta_norm`
- `der_donchian_position`
- `der_dow_cos`
- `der_dow_sin`
- `der_ema_spread_atr`
- `der_er_norm`
- `der_gates_pass_long_count`
- `der_hour_cos`
- `der_hour_sin`
- `der_long_score_mean_3`
- `der_long_score_mean_5`
- `der_minute_cos`
- `der_minute_sin`
- `der_range_position`
- `der_regime_code`
- `der_rr_ratio`
- `der_rsi_norm`
- `der_rvol_change_3`
- `der_rvol_mean_5`
- `der_score_dominance`
- `der_score_mean_10`
- `der_score_mean_3`
- `der_score_mean_5`
- `der_score_spread`
- `der_score_std_10`
- `der_score_std_3`
- `der_score_std_5`
- `der_session_minute`
- `der_stop_distance_atr`
- `der_target_distance_atr`
- `der_vwap_distance_atr`

## Fase 3 — Top 50 importância (MI)

- `feat_VolRatio`: MI=0.467671
- `feat_Atr`: MI=0.457208
- `feat_AtrTicks`: MI=0.457208
- `der_stop_distance_atr`: MI=0.456952
- `der_target_distance_atr`: MI=0.456952
- `der_atr_pct_close`: MI=0.447283
- `feat_Rvol`: MI=0.439823
- `feat_EmaSpreadTicks`: MI=0.431142
- `der_ema_spread_atr`: MI=0.424821
- `der_rvol_mean_5`: MI=0.42419
- `der_donchian_position`: MI=0.419221
- `der_range_position`: MI=0.419157
- `der_rvol_change_3`: MI=0.40911
- `feat_Rsi`: MI=0.403151
- `der_rsi_norm`: MI=0.401863
- `feat_Adx`: MI=0.377815
- `der_adx_norm`: MI=0.37504
- `der_er_norm`: MI=0.368958
- `feat_Er`: MI=0.356235
- `feat_VwapDistanceTicks`: MI=0.333674
- `der_vwap_distance_atr`: MI=0.329215
- `der_session_minute`: MI=0.316873
- `feat_BarRangeTicks`: MI=0.299094
- `der_score_std_10`: MI=0.254374
- `feat_DonchianUpper`: MI=0.23587
- `feat_DonchianLower`: MI=0.225471
- `der_score_std_5`: MI=0.183319
- `der_long_score_mean_5`: MI=0.181151
- `der_score_mean_10`: MI=0.180163
- `der_score_std_3`: MI=0.128776
- `der_score_mean_5`: MI=0.122685
- `der_long_score_mean_3`: MI=0.095567
- `der_score_mean_3`: MI=0.080225
- `der_score_spread`: MI=0.067821
- `der_score_dominance`: MI=0.058589
- `score`: MI=0.050821
- `hour`: MI=0.046038
- `feat_RecentCrossShort`: MI=0.042596
- `short_score`: MI=0.03968
- `feat_TickCount`: MI=0.039456
- `der_hour_sin`: MI=0.036105
- `feat_MtfEmaRising`: MI=0.031073
- `feat_SellVolume`: MI=0.028226
- `gate_Wyckoff_short`: MI=0.026001
- `feat_ChopFlips`: MI=0.025826
- `long_score`: MI=0.025618
- `der_dow_cos`: MI=0.02299
- `feat_BidAskPressure`: MI=0.021092
- `day_of_week`: MI=0.020905
- `feat_PullbackShort`: MI=0.017614

### Top 30 SHAP (champion v2)

- `der_donchian_position`: 0.699111
- `feat_EmaSpreadTicks`: 0.386501
- `der_minute_sin`: 0.320773
- `der_adx_norm`: 0.282683
- `feat_VolRatio`: 0.280184
- `der_score_std_5`: 0.270531
- `der_rvol_change_3`: 0.21626
- `der_er_norm`: 0.166876
- `feat_BarRangeTicks`: 0.163926
- `feat_VwapDistanceTicks`: 0.151424
- `der_ema_spread_atr`: 0.146284
- `der_vwap_distance_atr`: 0.137422
- `feat_Rvol`: 0.136406
- `der_long_score_mean_3`: 0.122436
- `feat_DonchianUpper`: 0.119275
- `feat_WyckoffBlockShort`: 0.119185
- `der_rvol_mean_5`: 0.112593
- `der_session_minute`: 0.111774
- `der_hour_sin`: 0.099558
- `der_score_mean_3`: 0.082734
- `der_atr_pct_close`: 0.082316
- `der_stop_distance_atr`: 0.071392
- `der_rsi_norm`: 0.059922
- `der_long_score_mean_5`: 0.058534
- `der_score_mean_5`: 0.053556
- `minute`: 0.052693
- `score`: 0.05231
- `feat_ChopFlips`: 0.052014
- `der_score_std_3`: 0.050729
- `feat_DonchianLower`: 0.045628

## Fase 4 — Comparação de conjuntos de features (CatBoost rápido)

| Set | Features | Val ROC | Test ROC |
|-----|----------|---------|----------|
| top_10 | 10 | 0.5873 | 0.5903 |
| top_20 | 20 | 0.5511 | 0.4967 |
| top_30 | 30 | 0.5435 | 0.5389 |
| top_40 | 40 | 0.6277 | 0.5335 |
| all | 98 | 0.5727 | 0.5865 |

## Fase 5 — Metodologia sem vazamento

- Threshold tunado em: **validation**
- Threshold congelado: **0.5**
- Ver: `UltronTraining/docs/PHASE5_METHODOLOGY.md`

## Fase 6 — Walk Forward

{
  "mode": "expanding",
  "aggregate": {
    "threshold": 0.5,
    "accuracy": 0.616,
    "precision": 0.456,
    "recall": 0.1905,
    "f1": 0.2664,
    "log_loss": 1.1149,
    "brier_score": 0.3066,
    "roc_auc": 0.5252,
    "pr_auc": 0.4265,
    "calibration_error": 0.27
  },
  "folds": [
    {
      "fold": 1,
      "metrics": {
        "threshold": 0.5,
        "accuracy": 0.6755,
        "precision": 0.5889,
        "recall": 0.2819,
        "f1": 0.3813,
        "log_loss": 1.2184,
        "brier_score": 0.2891,
        "roc_auc": 0.5945,
        "pr_auc": 0.4546,
        "calibration_error": 0.2779,
        "confusion_matrix": [
          [
            305,
            37
          ],
          [
            135,
            53
          ]
        ]
      }
    },
    {
      "fold": 2,
      "metrics": {
        "threshold": 0.5,
        "accuracy": 0.5566,
        "precision": 0.3231,
        "recall": 0.0991,
        "f1": 0.1516,
        "log_loss": 1.0113,
        "brier_score": 0.3242,
        "roc_auc": 0.4559,
        "pr_auc": 0.3985,
        "calibration_error": 0.2621,
        "confusion_matrix": [
          [
            274,
            44
          ],
          [
            191,
            21
          ]
        ]
      }
    }
  ]
}

## Fase 7-8 — Modelos e comparação v1 vs v2

| Métrica | Champion v1 (test) | Champion v2 (test) |
|---------|-------------------|-------------------|
| ROC AUC | 0.5016 | 0.5068 |
| Precision | 0.4462 | 0.4138 |
| Recall | 0.2397 | 0.0992 |
| F1 | 0.3118 | 0.16 |
| Brier | 0.2356 | 0.2352 |
| Cal Error | 0.0437 | 0.0449 |
| PF (test, frozen T) | — | 0.7529 |
| Expectancy | — | -4.3448 |
| Max DD | — | -238.0 |

## Fase 9 — Critérios de promoção

{
  "checks": {
    "roc_auc_test_gte_060": false,
    "brier_better_than_v1": true,
    "calibration_better_than_v1": false,
    "walk_forward_stable": true,
    "no_test_leakage": true,
    "threshold_on_validation": true
  },
  "ready_for_shadow": false,
  "ready_for_active": false,
  "wf_roc_auc_std": 0.0693,
  "v1_baseline_test_roc": 0.5016,
  "v2_test_roc": 0.5068,
  "roc_improved_vs_v1": true
}

## Respostas finais

1. **Novas features com maior ganho:** ver Top MI/SHAP acima (derivadas temporais e normalizadas)
2. **Vazamento eliminado?** Sim
3. **Modelo campeão v2:** LightGBM
4. **ROC melhorou vs v1?** True (0.5016 -> 0.5068)
5. **Pronto para Shadow?** Não
6. **Pronto para Active?** Não
7. **Ganho vs RC1 (test, frozen threshold):** PF=0.7529, WR=0.4138, trades=29

Pipeline time: 19311 ms