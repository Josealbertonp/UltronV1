# Fase 5 — Outcome Collection & Paper Execution

**Objetivo:** completar o ciclo `Signal → Trade → Outcome → Dataset → Validation → ML`.

**Não é:** automação para capital real. **Não adiciona** regras de entrada.

## Componentes

| Peça | Local | Função |
|------|-------|--------|
| `TradeCycleTracker` | `UltronV1.Core/Execution/` | Ciclo paper: open → MFE/MAE → stop/target/max bars → outcome |
| `OutcomeBuilder` | Core | R-multiple, PnL ticks/USD |
| `PaperExecutionConfig` | Core | SL/TP/max bars (default 30/32 ticks, alinhado NR paper) |
| `DatasetEngine.EnrichOutcome` | Core | Escreve `outcomes.jsonl` join por `signalId` |
| `UltronV1Paper` | `NinjaTrader/UltronV1Paper.cs` | Strategy NT8 para Replay / Sim101 / Paper |

## Campos em `outcomes.jsonl`

- `entryPrice`, `exitPrice`, `stopPrice`, `targetPrice`
- `mfeTicks`, `maeTicks`, `rMultiple`
- `realizedPnLTicks`, `realizedPnLUsd`
- `barsInTrade`, `secondsInTrade`
- `exitReason` (`stop_loss`, `take_profit`, `max_bars`)
- `executionMode` (`PaperBarSim`)
- `label` (1=win, 0=loss)

## Deploy NT8

1. Build Core: `dotnet build UltronV1.Core -c Release`
2. Copiar `UltronV1.Core.dll` → `Documents\NinjaTrader 8\bin\Custom\`
3. Copiar `NinjaTrader\UltronV1Paper.cs` → `Documents\NinjaTrader 8\bin\Custom\Strategies\`
4. Compilar NinjaScript (F5)
5. Aplicar strategy **UltronV1Paper** em chart **MNQ 09-26** 5m
6. Conta: **Sim101** ou Paper — **nunca live**

## Propriedades da Strategy

| Propriedade | Default | Descrição |
|-------------|---------|-----------|
| Paper Stop (ticks) | 30 | Stop loss fixo |
| Paper Target (ticks) | 32 | Take profit fixo |
| Max Bars In Trade | 48 | Saída por tempo |
| Min Tier For Entry | B | Mesmo critério do indicador |
| Submit Sim101 Orders | true | Ordens visuais no Sim101 (outcome autoritativo = bar sim no Core) |

## Saída do dataset

```
Documents/NinjaTrader 8/UltronV1/dataset/sessions/{sessionId}/
  opportunities.jsonl   ← sinal + features (Fase 3)
  outcomes.jsonl        ← execução paper (Fase 5)
  manifest.json           ← outcomeCount incrementado
```

## Gate para Validation Protocol v1

Coletar até atingir mínimos do protocolo:

- **150 trades** com outcome linkado (confirmatório)
- **40 dias** de sessão replay/paper

Depois executar:

```powershell
cd UltronV1.Validation
python validate.py --ultron-dataset "$env:USERPROFILE\Documents\NinjaTrader 8\UltronV1\dataset" ...
```

## Regras invioláveis

1. Strategy **não** altera gates, score ou order flow.
2. Uma posição por vez — sinal ignorado se já em trade.
3. Exit no mesmo bar do sinal é proibido (entrada no close).
4. Same-bar stop+target → **stop first** (conservador).
5. Capital real **fora de escopo** até Gate L3 do Validation Protocol.
