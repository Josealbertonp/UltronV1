# FEATURE_AUDIT_REPORT — RC2 Phase 5

**Generated:** 2026-06-26T20:41:23.801651+00:00
**Features audited:** 98
**Rows:** 1632

## Resumo

- Features constantes: **28**
- Pares correlacionados (|r| >= 0.95): **74**
- Features úteis (MI >= 0.01): **56**
- Features baixo valor: **40**

## Quais features realmente ajudam?

- `feat_VolRatio` — MI=0.467671, corr_target=-0.296311
- `feat_Atr` — MI=0.457208, corr_target=-0.148907
- `feat_AtrTicks` — MI=0.457208, corr_target=-0.148907
- `der_stop_distance_atr` — MI=0.456952, corr_target=0.167242
- `der_target_distance_atr` — MI=0.456952, corr_target=0.167242
- `der_atr_pct_close` — MI=0.447283, corr_target=-0.153808
- `feat_Rvol` — MI=0.439823, corr_target=-0.112222
- `feat_EmaSpreadTicks` — MI=0.431142, corr_target=-0.188176
- `der_ema_spread_atr` — MI=0.424821, corr_target=-0.127833
- `der_rvol_mean_5` — MI=0.423189, corr_target=-0.097501
- `der_donchian_position` — MI=0.419221, corr_target=0.025797
- `der_range_position` — MI=0.419157, corr_target=0.025797
- `der_rvol_change_3` — MI=0.407098, corr_target=-0.019774
- `feat_Rsi` — MI=0.403151, corr_target=-0.01623
- `der_rsi_norm` — MI=0.401863, corr_target=-0.01623
- `feat_Adx` — MI=0.377815, corr_target=-0.000665
- `der_adx_norm` — MI=0.37504, corr_target=-0.000665
- `der_er_norm` — MI=0.368958, corr_target=-0.111255
- `feat_Er` — MI=0.356235, corr_target=-0.111255
- `feat_VwapDistanceTicks` — MI=0.333674, corr_target=-0.070874

## Features praticamente inúteis

- `feat_FreshBreakoutLong`
- `feat_FreshBreakoutShort`
- `gate_ChopFilter_long`
- `der_delta_norm`
- `order_flow_passed`
- `feat_PriceAboveVwap`
- `der_buy_ratio`
- `archetype`
- `der_dow_sin`
- `der_minute_cos`
- `der_minute_sin`
- `der_regime_code`
- `der_rr_ratio`
- `feat_AbsorptionDetected`
- `feat_AdxRising`
- `feat_ContinuationLong`
- `feat_ContinuationShort`
- `feat_ErRising`
- `feat_IcebergRefills`
- `feat_PriceAboveEmaFast`
- `feat_PullbackLong`
- `feat_SweepDetected`
- `feat_SweepTicks`
- `feat_WyckoffBlockLong`
- `gate_ADX_long`

## Top 20 — Permutation Importance (CatBoost proxy)

- `der_score_mean_3`: 0.0048
- `feat_BarRangeTicks`: 0.00449
- `feat_ChopFlips`: 0.004061
- `der_er_norm`: 0.003775
- `feat_DonchianLower`: 0.002961
- `der_rvol_mean_5`: 0.002809
- `der_minute_sin`: 0.002796
- `der_minute_cos`: 0.002436
- `feat_DonchianUpper`: 0.002156
- `feat_Rsi`: 0.002117
- `der_score_std_5`: 0.002095
- `der_score_std_10`: 0.00205
- `feat_WyckoffBlockShort`: 0.001715
- `feat_EmaSpreadTicks`: 0.001648
- `der_ema_spread_atr`: 0.001607
- `der_score_std_3`: 0.001554
- `feat_Adx`: 0.001478
- `minute`: 0.001451
- `der_rvol_change_3`: 0.001407
- `der_donchian_position`: 0.001291

## Pares altamente correlacionados (>0.95)

| A | B | r |
|---|---|---|
| der_adx_norm | feat_Adx | 1.0 |
| der_atr_pct_close | feat_Atr | 0.999239 |
| der_atr_pct_close | feat_AtrTicks | 0.999239 |
| der_buy_ratio | feat_BidAskPressure | 1.0 |
| der_donchian_position | der_gates_pass_long_count | 0.956805 |
| der_donchian_position | der_range_position | 1.0 |
| der_donchian_position | der_rsi_norm | 0.967295 |
| der_donchian_position | der_score_dominance | 0.956746 |
| der_donchian_position | feat_ContinuationLong | 0.963111 |
| der_donchian_position | feat_ContinuationShort | -0.96165 |
| der_donchian_position | feat_PriceAboveEmaFast | 0.956805 |
| der_donchian_position | feat_Rsi | 0.967295 |
| der_donchian_position | feat_TrendAlignedLong | 0.963111 |
| der_donchian_position | feat_TrendAlignedShort | -0.96165 |
| der_er_norm | feat_Er | 1.0 |
| der_gates_pass_long_count | der_range_position | 0.956805 |
| der_gates_pass_long_count | der_score_dominance | 0.999999 |
| der_gates_pass_long_count | der_score_spread | 0.992641 |
| der_gates_pass_long_count | feat_ContinuationLong | 0.986425 |
| der_gates_pass_long_count | feat_ContinuationShort | -0.99008 |
| der_gates_pass_long_count | feat_PriceAboveEmaFast | 1.0 |
| der_gates_pass_long_count | feat_TrendAlignedLong | 0.986425 |
| der_gates_pass_long_count | feat_TrendAlignedShort | -0.99008 |
| der_gates_pass_long_count | long_score | 0.983583 |
| der_gates_pass_long_count | short_score | -0.987268 |
| der_range_position | der_rsi_norm | 0.967295 |
| der_range_position | der_score_dominance | 0.956746 |
| der_range_position | feat_ContinuationLong | 0.963111 |
| der_range_position | feat_ContinuationShort | -0.96165 |
| der_range_position | feat_PriceAboveEmaFast | 0.956805 |

## Tabela completa

| Feature | Tipo | Var | Missing% | MI | Corr Target | Const |
|---------|------|-----|----------|-----|-------------|-------|
| feat_VolRatio | float64 | 0.37202465 | 0.0 | 0.467671 | -0.296311 | False |
| feat_Atr | float64 | 414.40249872 | 0.0 | 0.457208 | -0.148907 | False |
| feat_AtrTicks | float64 | 6630.44023982 | 0.0 | 0.457208 | -0.148907 | False |
| der_stop_distance_atr | float64 | 0.00069783 | 0.0 | 0.456952 | 0.167242 | False |
| der_target_distance_atr | float64 | 0.00079398 | 0.0 | 0.456952 | 0.167242 | False |
| der_atr_pct_close | float64 | 0.00478552 | 0.0 | 0.447283 | -0.153808 | False |
| feat_Rvol | float64 | 6.59867535 | 0.0 | 0.439823 | -0.112222 | False |
| feat_EmaSpreadTicks | float64 | 12523.90392101 | 0.0 | 0.431142 | -0.188176 | False |
| der_ema_spread_atr | float64 | 0.12635172 | 0.0 | 0.424821 | -0.127833 | False |
| der_rvol_mean_5 | float64 | 4.57542651 | 0.0 | 0.423189 | -0.097501 | False |
| der_donchian_position | float64 | 0.11925404 | 0.0 | 0.419221 | 0.025797 | False |
| der_range_position | float64 | 0.11925404 | 0.0 | 0.419157 | 0.025797 | False |
| der_rvol_change_3 | float64 | 6.18697355 | 0.0 | 0.407098 | -0.019774 | False |
| feat_Rsi | float64 | 319.19196894 | 0.0 | 0.403151 | -0.01623 | False |
| der_rsi_norm | float64 | 0.0319192 | 0.0 | 0.401863 | -0.01623 | False |
| feat_Adx | float64 | 87.19955172 | 0.0 | 0.377815 | -0.000665 | False |
| der_adx_norm | float64 | 0.00871996 | 0.0 | 0.37504 | -0.000665 | False |
| der_er_norm | float64 | 0.04145724 | 0.0 | 0.368958 | -0.111255 | False |
| feat_Er | float64 | 0.04145724 | 0.0 | 0.356235 | -0.111255 | False |
| feat_VwapDistanceTicks | float64 | 454480.83858099 | 0.0 | 0.333674 | -0.070874 | False |
| der_vwap_distance_atr | float64 | 16.02763432 | 0.0 | 0.329215 | -0.024396 | False |
| der_session_minute | int64 | 156575.64066968 | 0.0 | 0.316873 | 0.005943 | False |
| feat_BarRangeTicks | float64 | 10942.57792638 | 0.0 | 0.299094 | -0.258506 | False |
| der_score_std_10 | float64 | 8.6278515 | 0.0 | 0.255264 | 0.15366 | False |
| feat_DonchianUpper | float64 | 388809.57184202 | 0.0 | 0.23587 | 0.201504 | False |
| feat_DonchianLower | float64 | 462747.5814472 | 0.0 | 0.225471 | 0.219888 | False |
| der_long_score_mean_5 | float64 | 864.49060961 | 0.0 | 0.184315 | 0.04076 | False |
| der_score_std_5 | float64 | 14.23484811 | 0.0 | 0.182818 | 0.161294 | False |
| der_score_mean_10 | float64 | 73.29991351 | 0.0 | 0.181661 | 0.008521 | False |
| der_score_mean_5 | float64 | 84.97262514 | 0.0 | 0.125935 | -0.010982 | False |
| der_score_std_3 | float64 | 23.67403509 | 0.0 | 0.123468 | 0.146208 | False |
| der_long_score_mean_3 | float64 | 1047.94145396 | 0.0 | 0.097559 | 0.041412 | False |
| der_score_mean_3 | float64 | 95.84948872 | 0.0 | 0.078842 | -0.032747 | False |
| der_score_spread | int64 | 5289.58720204 | 0.0 | 0.067821 | 0.022791 | False |
| der_score_dominance | float64 | 0.95778092 | 0.0 | 0.058589 | 0.023723 | False |
| score | int64 | 77.75421563 | 0.0 | 0.050821 | -0.053044 | False |
| hour | int64 | 43.26215879 | 0.0 | 0.046038 | 0.006827 | False |
| feat_RecentCrossShort | float64 | 0.06602629 | 0.0 | 0.042596 | -0.138997 | False |
| short_score | int64 | 1352.91543179 | 0.0 | 0.03968 | -0.028891 | False |
| feat_TickCount | float64 | 0.88807039 | 0.0 | 0.039456 | 0.263115 | False |
| der_hour_sin | float64 | 0.43933633 | 0.0 | 0.036105 | -0.019373 | False |
| feat_MtfEmaRising | float64 | 0.2452058 | 0.0 | 0.031073 | 0.00057 | False |
| feat_SellVolume | float64 | 0.70151955 | 0.0 | 0.028226 | 0.162158 | False |
| feat_ChopFlips | float64 | 0.22656212 | 0.0 | 0.025826 | -0.215199 | False |
| long_score | int64 | 1330.75527704 | 0.0 | 0.025618 | 0.016309 | False |
| der_dow_cos | float64 | 0.61829344 | 0.0 | 0.02299 | 0.095326 | False |
| feat_BidAskPressure | float64 | 120184.80336289 | 0.0 | 0.021092 | 0.136736 | False |
| day_of_week | int64 | 2.68305422 | 0.0 | 0.020905 | -0.114883 | False |
| feat_PullbackShort | float64 | 0.02215757 | 0.0 | 0.017614 | 0.026401 | False |
| feat_Delta | float64 | 2.12456597 | 0.0 | 0.01748 | 0.007193 | False |
| feat_FreshBreakoutLong | float64 | 0.0 | 0.0 | 0.016982 | nan | True |
| feat_WyckoffBlockShort | float64 | 0.11573523 | 0.0 | 0.016243 | -0.177767 | False |
| feat_TrendAlignedLong | float64 | 0.2469588 | 0.0 | 0.015897 | 0.021783 | False |
| feat_FreshBreakoutShort | float64 | 0.0 | 0.0 | 0.01369 | nan | True |
| feat_RecentCrossLong | float64 | 0.04272958 | 0.0 | 0.013428 | -0.108957 | False |
| feat_AdxStrong | float64 | 0.16095334 | 0.0 | 0.010927 | -0.025429 | False |
| feat_BuyVolume | float64 | 1.23024197 | 0.0 | 0.010784 | 0.131904 | False |
| feat_TrendAlignedShort | float64 | 0.2455392 | 0.0 | 0.010207 | -0.03351 | False |
| der_hour_cos | float64 | 0.54608798 | 0.0 | 0.009774 | -0.025865 | False |
| der_gates_pass_long_count | int64 | 8.86211884 | 0.0 | 0.009456 | 0.023735 | False |
| gate_ChopFilter_long | float64 | 0.0 | 0.0 | 0.005562 | nan | True |
| der_delta_norm | float64 | 0.27385163 | 0.0 | 0.001246 | 0.001818 | False |
| order_flow_passed | float64 | 0.0 | 0.0 | 0.001118 | nan | True |
| feat_PriceAboveVwap | float64 | 0.24394426 | 0.0 | 0.001099 | -0.008063 | False |
| der_buy_ratio | float64 | 0.12062931 | 0.0 | 0.000938 | 0.136892 | False |
| archetype | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| der_dow_sin | float64 | 0.29539077 | 0.0 | 0.0 | -0.019579 | False |
| der_minute_cos | float64 | 0.47592541 | 0.0 | 0.0 | 0.043904 | False |
| der_minute_sin | float64 | 0.51604644 | 0.0 | 0.0 | -0.015123 | False |
| der_regime_code | int64 | 0.0 | 0.0 | 0.0 | nan | True |
| der_rr_ratio | float64 | 0.0 | 0.0 | 0.0 | -0.0 | True |
| feat_AbsorptionDetected | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| feat_AdxRising | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| feat_ContinuationLong | float64 | 0.2469588 | 0.0 | 0.0 | 0.021783 | False |
| feat_ContinuationShort | float64 | 0.2455392 | 0.0 | 0.0 | -0.03351 | False |
| feat_ErRising | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| feat_IcebergRefills | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| feat_PriceAboveEmaFast | float64 | 0.24616997 | 0.0 | 0.0 | 0.023735 | False |
| feat_PullbackLong | float64 | 0.02449141 | 0.0 | 0.0 | 0.057642 | False |
| feat_SweepDetected | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| feat_SweepTicks | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| feat_WyckoffBlockLong | float64 | 0.11841599 | 0.0 | 0.0 | -0.104935 | False |
| gate_ADX_long | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gate_ADX_short | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gate_ChopFilter_short | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gate_EMA_long | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gate_EMA_short | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gate_ER_long | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gate_ER_short | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gate_TrendAcceleration_long | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gate_TrendAcceleration_short | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gate_Wyckoff_long | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gate_Wyckoff_short | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gates_long_allowed | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| gates_short_allowed | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| minute | int64 | 312.27381558 | 0.0 | 0.0 | -0.019378 | False |
| regime | float64 | 0.0 | 0.0 | 0.0 | nan | True |
| tier | float64 | 0.0 | 0.0 | 0.0 | nan | True |