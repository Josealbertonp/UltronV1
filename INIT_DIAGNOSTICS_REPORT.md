# INIT_DIAGNOSTICS_REPORT — UltronV1Paper

**Data:** 2026-06-26  
**Objetivo:** Diagnosticar por que o checkbox **Habilitado** da strategy fica desmarcado logo após ser adicionada ao gráfico.  
**Escopo:** Telemetria temporária apenas — **nenhuma lógica operacional foi alterada**.

---

## Hipótese principal

No NinjaTrader 8, quando uma strategy lança **exceção não tratada** durante `OnStateChange` (tipicamente em `State.DataLoaded`), o NT **desabilita automaticamente** a strategy (checkbox **Habilitado** desmarcado).  
A telemetria abaixo identifica **em qual etapa** a exceção ocorre (ou se a strategy chega a `OnBarUpdate`).

---

## Arquivos modificados

| Arquivo | Deploy NT8 |
|---------|------------|
| `UltronV1/NinjaTrader/UltronV1Paper.cs` | `Documents\NinjaTrader 8\bin\Custom\Strategies\UltronV1Paper.cs` |

**Não alterados:** Engine, SignalEvaluator, FeatureEngine, PaperBarSim, UltronMlHost, UltronML.Core.

---

## Telemetria adicionada

### Ciclo de vida (`OnStateChange`)

| Estado | Log |
|--------|-----|
| `SetDefaults` | `[INIT] Entrou em State.SetDefaults` → `Depois de State.SetDefaults` |
| `Configure` | `[INIT] Entrou em State.Configure` → `Depois de State.Configure` |
| `DataLoaded` | `[INIT] Entrou em State.DataLoaded` → `Depois de State.DataLoaded` |
| `Historical` | `[INIT] Entrou em State.Historical` |
| `Realtime` | `[INIT] Entrou em State.Realtime` |
| `Terminated` | `[INIT] Entrou em State.Terminated` → `Depois de State.Terminated` |

Qualquer falha imprime `[INIT ERROR] <contexto>: <ex.ToString()>` e **re-lança** a exceção (comportamento original preservado).

### Etapas de inicialização (`State.DataLoaded`)

Cada bloco envolvido com `try/catch` + `Print` antes/depois:

| Etapa | Mensagens |
|-------|-----------|
| Snapshot de config | Account, ExecutionMode, ATM Template, Shadow ML, MlFilterMode, caminho do modelo |
| `InitializeDatasetEngine()` | Criação de `UltronConfig` + `DatasetEngine` |
| `InitializeTradeCycle()` | Criação de `TradeCycleTracker` |
| `InitializeExecution()` | Criação de `UltronEngine` + `Initialize(Instrument)` |
| `InitializeAtm()` | Validação **somente leitura** do template ATM (sem ordens) |
| `InitializeMlHost()` | Ver seção ML abaixo |

### ML / ONNX (`InitializeMlHost`)

| Etapa | Mensagens |
|-------|-----------|
| Entrada | `[INIT] Antes de InitializeMlHost()` |
| ML desabilitado | `InitializeMlHost() ignorado — ML desabilitado` |
| Diretório Shadow ML | Caminho resolvido, `Directory.Exists`, presença de `model.onnx`, `thresholds.json`, `metadata.json` |
| DLL ONNX | `Assembly.Load("Microsoft.ML.OnnxRuntime")` + localização |
| Modelo | `new UltronMlHost(...)` + `OpenSession` + `loadError` |
| Saída | `[INIT] Depois de InitializeMlHost()` |

### `OnBarUpdate` (uma vez)

```
[INIT]
Strategy initialized successfully
```

Impresso na **primeira** chamada a `OnBarUpdate`, **antes** dos guards existentes.  
Se esta linha **não aparecer**, a strategy foi desabilitada antes de processar barras (falha em `DataLoaded` ou guards impedem chegada tardia — improvável se NT desabilitou no init).

### Validação ATM (diagnóstico)

```
[INIT] ATM Template validation configured=...
[INIT] ATM Template validation exists=...
[INIT] ATM Template validation result=OK|NOT_FOUND|NOT_CONFIGURED
[INIT] ATM account resolve=... status=conectada|desconectada|nao encontrada
```

---

## Procedimento de teste

1. **Feche** o NinjaTrader (se aberto).
2. Confirme que `Custom\Strategies\UltronV1Paper.cs` contém os logs `[INIT]`.
3. Reabra o NT → **F5** (compilar).
4. Abra **Output Window** (Ctrl+O) e filtre por `[INIT]`.
5. Adicione `UltronV1Paper` a um gráfico **MNQ 09-26** (contrato exigido pelo engine).
6. Observe a sequência de logs e se o checkbox **Habilitado** permanece marcado.

### Cenários recomendados

| # | Configuração | Objetivo |
|---|--------------|----------|
| A | Defaults (ML off, PaperBarSim) | Baseline — init deve completar |
| B | `EnableShadowMl=true` + pasta modelo válida | Testar ONNX + carregamento |
| C | `ExecutionMode=NinjaTraderAtm` + template inválido | Ver validação ATM (não deve desabilitar sozinha) |
| D | Instrumento **não-MNQ** | Ver `contractValid=false` (não deve desabilitar, mas strategy não opera) |

---

## Interpretação dos resultados

| Último log antes do silêncio | Causa provável |
|------------------------------|----------------|
| `[INIT ERROR] InitializeDatasetEngine` | Falha em IO/dataset (`UltronV1/dataset`) |
| `[INIT ERROR] InitializeExecution` | Falha no `UltronEngine.Initialize` ou DLL `UltronV1.Core` |
| `[INIT ERROR] carregamento DLL ONNX` | `Microsoft.ML.OnnxRuntime.dll` ausente ou não referenciada |
| `[INIT ERROR] carregamento de modelo` | `UltronML.Core.dll`, modelo ONNX corrompido, ou exceção no `ShadowMlService` |
| `[INIT ERROR] acesso ao diretório Shadow ML` | Caminho inválido / permissão |
| `Depois de State.DataLoaded` mas **sem** `Strategy initialized successfully` | Strategy desabilitada após DataLoaded (raro) ou gráfico sem barras suficientes |
| `Strategy initialized successfully` + checkbox desmarcado | Desabilitação por motivo **externo** ao init (ex.: erro em ordem, template ATM em realtime, ou ação manual) |

### Sequência saudável esperada

```
[INIT] Entrou em State.SetDefaults
[INIT] Depois de State.SetDefaults
[INIT] Entrou em State.Configure
[INIT] Depois de State.Configure
[INIT] Entrou em State.DataLoaded
[INIT] Account selecionada=...
... InitializeDatasetEngine / TradeCycle / Execution ...
[INIT] Depois de State.DataLoaded
[INIT] Entrou em State.Historical
[INIT] Entrou em State.Realtime   (ou só Historical em replay)
[INIT]
Strategy initialized successfully
```

---

## Remoção pós-diagnóstico

Após identificar a etapa falha, remover:

- Região `#region Init diagnostics (temporary)`
- Campo `_initDiagOnBarUpdateLogged`
- Todos os `Print("[INIT]...")` e blocos `try/catch` de diagnóstico
- `using System.Reflection;` (se não usado em outro lugar)

Recompilar (F5) e restaurar o arquivo limpo no repositório.

---

## Notas

- `InitDiagProbeOnnxDll()` só executa quando ML está habilitado (`EnableShadowMl` ou `EnableMlFilter` ou `MlFilterMode != Disabled`).
- `InitDiagValidateAtm()` é **somente leitura** — não chama `StartAtmStrategy` nem cria ordens.
- Exceções continuam sendo propagadas (`throw;`) para preservar o comportamento original do NT.
