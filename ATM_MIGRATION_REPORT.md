# Relatório técnico — Migração ATM nativo `UltronV1Paper`

**Data:** 2026-06-26  
**Escopo:** Execução visual via ATM NinjaTrader, mantendo **PaperBarSim** como fonte autoritativa.

---

## 1. Arquivos alterados

| Arquivo | Ação |
|---------|------|
| `NinjaTrader/UltronV1Paper.cs` | Modificado — modos de execução, ATM, propriedades |
| `NinjaTrader/Shared/UltronPaperExecutionMode.cs` | **Novo** — enum `UltronPaperExecutionMode` |
| `Documents/NinjaTrader 8/bin/Custom/Strategies/UltronV1Paper.cs` | Deploy |
| `Documents/NinjaTrader 8/bin/Custom/Ultron/Shared/UltronPaperExecutionMode.cs` | Deploy |
| `Documents/NinjaTrader 8/bin/Custom/NinjaTrader.Custom.csproj` | Entrada de compilação do enum |

**Não alterados (conforme pedido):** `UltronEngine`, `SignalEvaluator`, `FeatureEngine`, `TradeCycleTracker`, scoring, gates, order flow, `outcomes.jsonl`.

---

## 2. Métodos novos / alterados

### Novos

| Método | Função |
|--------|--------|
| `TrySubmitVisualEntry()` | Despacha Sim101 vs ATM conforme `ExecutionMode` |
| `TrySubmitNinjaTraderAtmEntry()` | Cria ordem na conta + `AtmStrategy.StartAtmStrategy()` |
| `ResolveExecutionAccount()` | Resolve `AccountName` → `Account` |
| `LogAtmFailed()` | Telemetria `[ATM] ATM Failed` |

### Alterados

| Método | Mudança |
|--------|---------|
| `TryOpenPaperTrade()` | Chama visual só se `ExecutionMode != PaperBarSim` |
| `ProcessOutcomeCycle()` | `ExitLong`/`ExitShort` só em `Sim101Visual` |
| `LogPaperExecutionMode()` | Log por modo de execução |

### Removido

| Propriedade | Substituída por |
|-------------|-----------------|
| `SubmitSimOrders` (bool) | `ExecutionMode` (enum) |

---

## 3. Fluxo antes / depois

### Antes

```
Signal Accepted
    ↓
TradeCycleTracker.TryOpen()     ← PaperBarSim (outcomes)
    ↓
if SubmitSimOrders
    EnterLong(1) + SetStopLoss + SetProfitTarget   ← 1 ct, 1 alvo
```

### Depois

```
Signal Accepted
    ↓
TradeCycleTracker.TryOpen()     ← inalterado (PaperBarSim autoritativo)
    ↓
switch (ExecutionMode)
    PaperBarSim        → (nada no broker)
    Sim101Visual       → EnterLong(1) + SetStopLoss + SetProfitTarget
    NinjaTraderAtm     → Account.CreateOrder(qty) + StartAtmStrategy(template)
```

---

## 4. Propriedades novas (grupo **Execution**)

| Propriedade | Default | Descrição |
|-------------|---------|-----------|
| **Execution Mode** | `PaperBarSim` | Modo de execução visual |
| **Account** | `Sim101` | Conta para ATM (converter `UltronAccountConverter`) |
| **ATM Template** | *(vazio)* | Nome do XML em `templates\AtmStrategy` (converter `UltronAtmTemplateConverter`) |
| **Quantity** | `1` | Contratos na ordem de entrada ATM |

---

## 5. API ATM utilizada

### Pedido: `AtmStrategyCreate()`

A API **Strategy** `AtmStrategyCreate()` (ex.: `SampleAtmStrategy`) **não aceita quantidade dinâmica** — a qty vem do template XML salvo no NinjaTrader ([documentação NT8](https://ninjatrader.com/support/helpGuides/nt8/using_atm_strategies.htm), fórum NT).

Para honrar **Quantity = 3** (requisito 3), a implementação usa o mesmo padrão do **NightRanger** e **UTBot**:

```csharp
Order entryOrder = account.CreateOrder(..., qty, ...);
NinjaTrader.NinjaScript.AtmStrategy.StartAtmStrategy(template, entryOrder);
```

Isso é **ATM nativo** do NinjaTrader (Add-On / Account API), não `EnterLong`/`SetProfitTarget`.

| API | Quantity dinâmica | Usado |
|-----|-------------------|-------|
| `AtmStrategyCreate()` (Strategy) | Não (template) | Não |
| `AtmStrategy.StartAtmStrategy()` (Account) | Sim (`CreateOrder`) | **Sim** |

---

## 6. Telemetria `[ATM]`

### Sucesso (antes do submit)

```
[ATM]
Creating ATM...
Template=MeuATM_3ct
Account=Sim101
Quantity=3
ATM Id=<unique id>
```

### Sucesso

```
[ATM]
ATM Started Successfully
```

### Falha

```
[ATM]
ATM Failed
Erro=<mensagem>
```

Falhas **não** revertem `TradeCycleTracker.TryOpen()` — PaperBarSim continua normalmente.

---

## 7. Compatibilidade RC1

| Aspecto | Status |
|---------|--------|
| PaperBarSim / `outcomes.jsonl` | **Inalterado** — default `ExecutionMode = PaperBarSim` |
| Gates / score / engine | **Inalterado** |
| Comportamento legado Sim101 | `ExecutionMode = Sim101Visual` ≡ antigo `SubmitSimOrders = true` |
| Breaking change | `SubmitSimOrders` removido → migrar para enum |
| ATM vs outcomes | **Desacoplado** — fills ATM não escrevem outcomes |

---

## 8. Limitações

1. **ATM não roda em barras históricas** — NT8 bloqueia; em Replay só dispara em tempo real do playback (`State != Historical`).
2. **Execuções ATM não atualizam `Position` da strategy** — NT documenta que ATM é independente da posição/PnL da strategy hospedeira.
3. **`OnOrderUpdate` / `[SIM101]`** — não refletem ordens ATM (geridas pela conta, não pela strategy managed).
4. **Sem sync ATM ↔ PaperBarSim** — fechamento PaperBarSim não fecha ATM; em `NinjaTraderAtm` não há `ExitLong` automático.
5. **Template XML** — nome deve ser **exatamente** o filename sem `.xml` em `Documents\NinjaTrader 8\templates\AtmStrategy\`.
6. **Alvos múltiplos** — vêm do **template ATM**, não do código; qty de parciais também no template.
7. **Quantity** — enviada na **ordem de entrada**; targets/stops do template aplicam-se sobre essa qty.

---

## 9. Como testar no Replay

1. **F5** no NinjaScript Editor (compilar).
2. Aplicar `UltronV1Paper` em MNQ 5m, conta **Sim101**.
3. **Execution Mode** = `NinjaTraderAtm`.
4. **ATM Template** = nome exato do seu XML (ex. `Ultron_Scalp_3L`).
5. **Account** = `Sim101`.
6. **Quantity** = `3`.
7. Iniciar **Market Replay** (não backtest Analyzer).
8. Aguardar sinal → Output deve mostrar `[ATM] Creating ATM...` e `ATM Started Successfully`.
9. Verificar **Chart Trader / SuperDOM** — múltiplos alvos do template.
10. Confirmar `outcomes.jsonl` continua com `executionMode: PaperBarSim` e SL/TP 30/32.

> Se só vir logs em barras realtime do replay e não no histórico carregado: comportamento esperado (limitação NT8).

---

## 10. Como testar no Sim101 (sem ATM)

1. **Execution Mode** = `Sim101Visual`.
2. Comportamento idêntico ao antigo `SubmitSimOrders = true`.
3. Output `[SIM101]` se `Enable Sim101 Debug Log` = true.
4. 1 contrato, 1 stop, 1 target (`Paper Stop` / `Paper Target`).

---

## 11. Como testar coleta pura (RC1 default)

1. **Execution Mode** = `PaperBarSim`.
2. Nenhuma ordem no broker.
3. Apenas `outcomes.jsonl` via `TradeCycleTracker`.

---

## 12. Migração de configuração

| Antes | Depois |
|-------|--------|
| `SubmitSimOrders = false` | `Execution Mode = PaperBarSim` |
| `SubmitSimOrders = true` | `Execution Mode = Sim101Visual` |
| ATM manual no indicador | `Execution Mode = NinjaTraderAtm` + template na **Strategy** |

---

*PaperBarSim permanece a única fonte para ML, validation e `ATM_RECOMMENDATIONS.md`.*
