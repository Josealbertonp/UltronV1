# Ultron V1 — Phase 4.5 Validation Framework

Infraestrutura quantitativa para validar se o **Ultron V1 evoluiu em relação ao NightRanger** antes da Fase 5 (Strategy + TradeCycle).

**Nenhuma lógica do engine Ultron é modificada.** Este projeto opera sobre dados exportados.

## Componentes

| Módulo | Função |
|--------|--------|
| Backtest Comparator | Ultron vs NR — overlap, timing, PF, drawdown |
| Parameter Sweep | Counterfactual em Score, RVOL, Delta, ADX |
| Heatmaps | Visualização de robustez paramétrica |
| Walk-Forward Reports | Estabilidade temporal da vantagem Ultron |
| Monte Carlo | Distribuição de PF, drawdown, prob. lucrativa |
| Stability Tests | Sensibilidade paramétrica + detecção de overfitting |
| HTML Report | Relatório automático com respostas executivas |

## Perguntas respondidas

- O Ultron entrou **antes**?
- Entrou **melhor** (PF, expectancy)?
- **Reduziu drawdown**?
- Quais parâmetros são **mais robustos**?
- Existe **overfitting**?

## Instalação

```powershell
cd UltronV1.Validation
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
```

## Uso — dados reais (NT8)

Colete durante replay/backtest:

1. **Ultron dataset** — `Enable Dataset Engine = true`
2. **NR comparison** — `Enable NR Comparison Mode = true` → `UltronV1_NR_Comparison.csv`
3. **NR trades** — `Documents\NinjaTrader 8\NightRangerLogs\NightRanger_trades_*.jsonl`

```powershell
python validate.py `
  --ultron-dataset "$env:USERPROFILE\Documents\NinjaTrader 8\UltronV1\dataset" `
  --nr-logs "$env:USERPROFILE\Documents\NinjaTrader 8\NightRangerLogs" `
  --comparison-csv "$env:USERPROFILE\Documents\NinjaTrader 8\UltronV1\UltronV1_NR_Comparison.csv" `
  --output ./reports
```

## Uso — demo (synthetic)

```powershell
python validate.py --synthetic --output ./reports
```

## Saídas

```
reports/
├── validation_report.html      ← relatório principal
├── validation_summary.json     ← métricas-chave (CI/automação)
└── heatmaps/
    ├── heatmap_score_rvol_pf.png
    ├── heatmap_score_rvol_wr.png
    └── ...
```

## Parameter Sweep (counterfactual)

O sweep **não re-executa o engine**. Filtra `opportunities.jsonl` com thresholds alternativos:

- `score_min` (45–70)
- `rvol_min` (0.30–0.70)
- `delta_min` (0–300)
- `adx_min` (15–30)

Métricas calculadas sobre outcomes linkados (`rMultiple`) ou labels sintéticos.

## Gate para Fase 5

Recomendado iniciar Strategy apenas quando:

- Verdict = `stable_evolution` ou `improving`
- Overfit risk = `low` ou `moderate`
- Ultron PF ≥ NR PF em walk-forward majority
- Robustness score ≥ 50%

## Arquitetura

```
UltronV1.Validation/          ← Phase 4.5 (este projeto)
UltronV1.Training/            ← Phase 4 (ML platform)
UltronV1.Core/                ← Engine congelado
```
