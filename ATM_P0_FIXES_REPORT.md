# Relatório — Ajustes P0 modo `NinjaTraderAtm`

**Data:** 2026-06-26  
**Escopo:** Apenas camada ATM visual em `UltronV1Paper.cs` — compatibilidade com API oficial NT8.

---

## 1. Arquivos alterados

| Arquivo | Alteração |
|---------|-----------|
| `NinjaTrader/UltronV1Paper.cs` | `TrySubmitNinjaTraderAtmEntry()` + helper `AtmTemplateFileExists()` |
| `Documents/NinjaTrader 8/bin/Custom/Strategies/UltronV1Paper.cs` | Deploy (cópia) |

**Não alterados:** `UltronEngine`, `SignalEvaluator`, `FeatureEngine`, `TradeCycleTracker`, Core, gates, score, PaperBarSim, outcomes.

---

## 2. Diff resumido

### P0-1 — Nome da ordem

```diff
- "UV1_ATM_Entry",
+ "Entry",   // const entryOrderName — exigido por StartAtmStrategy (NT8 Help)
```

Demais parâmetros de `CreateOrder()` **inalterados**.

### P0-2 — Sem sucesso prematuro

```diff
- Print("ATM Started Successfully");
+ Print("ATM Start Requested");
```

**Motivo:** `StartAtmStrategy()` é fire-and-forget. Não há callback síncrono na API Add-On que confirme anexo do ATM ao template. O sucesso real só aparece nos eventos da conta (`OrderUpdate` / fill) — fora do escopo P0 (RC2). Logar "Started Successfully" era **falso positivo**.

### P0-3 — Validação de template

```csharp
+ if (!AtmTemplateFileExists(template)) {
+     Print("[ATM]");
+     Print("Template not found:");
+     Print(template);
+     return;
+ }
```

Verifica `Documents\NinjaTrader 8\templates\AtmStrategy\{nome}.xml`. Sem exceção — retorno silencioso com log.

Nome vazio: já bloqueado antes com `LogAtmFailed("ATM template not configured")`.

### P0-4 — Telemetria

**Antes:**

```
[ATM] Creating ATM...
Template=...
Account=...
Quantity=...
ATM Id=...
[ATM] ATM Started Successfully
```

**Depois:**

```
[ATM]
Creating ATM...
Account: Sim101
Template: MeuTemplate
Quantity: 3
Instrument: MNQ 09-26
Order Name:
Entry

[ATM]
ATM Start Requested
```

Removido `ATM Id=` (não utilizado pela API neste fluxo; tracking fica para RC2).

### P0-5 — Não implementado (confirmado)

- Sem `Account.OrderUpdate`
- Sem sync Paper ↔ ATM
- Sem guards multi-ATM
- Sem bridge / lifecycle

---

## 3. Por que cada mudança foi feita

| ID | Problema (auditoria) | Correção |
|----|----------------------|----------|
| P0-1 | NT8 exige `name == "Entry"` em `CreateOrder` para `StartAtmStrategy` anexar o template | Nome corrigido |
| P0-2 | "Started Successfully" imediato = falso positivo | "Start Requested" — honesto sobre o que a API garante |
| P0-3 | Typo no template só falhava em runtime/opaco | `File.Exists` antes do submit |
| P0-4 | Logs inconsistentes / enganosos | Formato estruturado + remoção de ATM Id |

---

## 4. PaperBarSim continua idêntico

| Verificação | Status |
|-------------|--------|
| `TradeCycleTracker.TryOpen()` / `OnBar()` | **Não tocado** |
| Ordem de execução: `TryOpen` → depois ATM | **Inalterada** |
| `PaperStopLossTicks` / `PaperTakeProfitTicks` | **Inalterados** |
| `outcomes.jsonl` / `executionMode: PaperBarSim` | **Inalterado** |
| `ExecutionMode.PaperBarSim` | **Sem ordens broker** — igual |
| Falha ATM após `TryOpen` | Paper continua aberto — **by design** |

Todas as mudanças estão **dentro** de `TrySubmitNinjaTraderAtmEntry()` e `AtmTemplateFileExists()`.

---

## 5. Nenhuma regra de trading modificada

| Componente | Modificado? |
|------------|-------------|
| `UltronEngine.ProcessBarClose` | Não |
| `SignalEvaluator` / gates / score | Não |
| `MinTierForEntry` / tier check | Não |
| `TryOpenPaperTrade` guard `!_tradeCycle.IsOpen` | Não |
| Stops/targets PaperBarSim | Não |
| ML / dataset | Não |

---

## 6. Como testar no Replay

1. **F5** — compilar NinjaScript.
2. `UltronV1Paper` em MNQ 5m, conta Sim101/Playback conectada.
3. **Execution Mode** = `NinjaTraderAtm`.
4. **ATM Template** = nome exato de um XML existente em `templates\AtmStrategy\`.
5. **Quantity** = 3 (ou valor desejado).
6. Iniciar **Market Replay** (não Strategy Analyzer).
7. Aguardar sinal em fase **realtime** do playback.

### Cenários de log esperados

| Cenário | Output |
|---------|--------|
| Template vazio | `[ATM] ATM Failed` / `Erro=ATM template not configured` |
| Template inexistente | `[ATM] Template not found:` + nome |
| Historical bar | `[ATM] ATM Failed` / `Erro=ATM does not run on historical bars...` |
| Submit OK | `Creating ATM...` → `ATM Start Requested` |
| Exception NT | `[ATM] ATM Failed` / `Erro=...` |

---

## 7. Como validar que o ATM anexou ao template

`ATM Start Requested` **não prova** anexo — apenas que `StartAtmStrategy()` foi invocado sem exception.

### Confirmação visual (Sim101 / Replay)

1. Após `ATM Start Requested`, abrir **Chart Trader** ou **SuperDOM** na mesma conta.
2. Verificar:
   - Ordem de entrada **Market** com nome **Entry**
   - Bracket ATM com **stop + targets** do template (não 30/32 do paper)
   - Quantidade = `Quantity` configurada (ex. 3)
3. Se só aparecer ordem solta sem bracket → ATM não anexou (verificar nome `Entry` e template).

### Confirmação na aba Orders / Executions

- **Control Center → Orders:** estado `Working` / `Filled` na entrada + ordens filhas do ATM.
- **Executions:** fill da entrada com qty correta.

### O que NÃO usar como prova (P0)

- `Strategy.Position` — NT8 não reflete posição ATM na strategy hospedeira.
- `OnOrderUpdate` da strategy — ordens ATM não passam pelos callbacks managed (RC2).
- Texto `ATM Start Requested` — é pedido de início, não confirmação.

### Prova de regressão PaperBarSim

- `outcomes.jsonl` deve continuar com `stopPrice`/`targetPrice` 30/32 ticks e `executionMode: PaperBarSim` — independente do ATM visual.

---

## 8. Referência API

- [StartAtmStrategy — order name must be `Entry`](https://ninjatrader.com/support/helpguides/nt8/startatmstrategy.htm)
- [Using ATM Strategies — ATM não afeta Strategy.Position](https://ninjatrader.com/support/helpguides/nt8/using_atm_strategies.htm)

---

*Próximos passos (RC2): `Account.OrderUpdate`, guards multi-ATM, confirmação assíncrona de anexo.*
