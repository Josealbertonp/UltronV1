# RC2 Fase 4A/4B — Auditoria de UI (Propriedades ML no NinjaTrader)

**Data da auditoria:** 26/06/2026  
**Escopo:** Somente leitura — nenhum código foi alterado.  
**Objetivo:** Explicar por que as propriedades ML da RC2 Fase 4A/4B **não aparecem** na janela de propriedades da strategy `UltronV1Paper` no NinjaTrader 8.

---

## 1. Resumo executivo

| Pergunta | Resultado |
|----------|-----------|
| As 6 propriedades ML existem no **workspace**? | **Sim** — todas presentes e corretamente anotadas |
| As 6 propriedades existem em **Custom\Strategies**? | **Não** — arquivo desatualizado (pré-Fase 4A/4B) |
| Workspace == Custom? | **Não** — hashes, tamanho e conteúdo divergem |
| Fase 4B copiada para Custom? | **Não** — faltam `.cs` Shared ML + `UltronML.Core.dll` + entradas no `.csproj` |
| O NT8 está usando versão antiga compilada? | **Sim** — `NinjaTrader.Custom.dll` não contém símbolos ML |

**Conclusão:** As propriedades ML não aparecem porque o NinjaTrader está executando uma **compilação anterior** da strategy, cuja fonte em `Documents\NinjaTrader 8\bin\Custom\Strategies\UltronV1Paper.cs` **nunca recebeu** o código da Fase 4A/4B. O problema **não é** ausência de `[NinjaScriptProperty]` no workspace — é **gap de deploy**.

---

## 2. Inventário das propriedades no workspace

**Arquivo auditado:** `UltronV1/NinjaTrader/UltronV1Paper.cs` (864 linhas, 34 320 bytes, SHA256 `5A07BC16…`)

Todas as seis propriedades solicitadas **existem** no workspace.

### 2.1 Defaults em `SetDefaults` (linhas 82–88)

| Propriedade | Default |
|-------------|---------|
| `EnableShadowMl` | `false` |
| `MlFilterMode` | `UltronMlFilterMode.Disabled` |
| `EnableMlFilter` | `false` |
| `ForcePassThrough` | `false` |
| `MlUseAutoThreshold` | `true` |
| `MlThresholdManual` | `0.70` |

### 2.2 Declarações de propriedade (detalhe por item)

| Propriedade | Linha | Tipo | Default | `[NinjaScriptProperty]` | `[Display]` | GroupName | Order |
|-------------|-------|------|---------|-------------------------|-------------|-----------|-------|
| `EnableShadowMl` | 828 | `bool` | `false` | **Sim** (825) | **Sim** — Name = `"Enable Shadow ML"` (826) | **Shadow ML** | **1** |
| `MlFilterMode` | 838 | `UltronMlFilterMode` | `Disabled` | **Sim** (835) | **Sim** — Name = `"ML Filter Mode"` (836) | **ML Filter** | **1** |
| `EnableMlFilter` | 843 | `bool` | `false` | **Sim** (840) | **Sim** — Name = `"Enable ML Filter"` (841) | **ML Filter** | **2** |
| `ForcePassThrough` | 848 | `bool` | `false` | **Sim** (845) | **Sim** — Name = `"Force Pass Through"` (846) | **ML Filter** | **3** |
| `MlUseAutoThreshold` | 853 | `bool` | `true` | **Sim** (850) | **Sim** — Name = `"Use Auto Threshold"` (851) | **ML Filter** | **4** |
| `MlThresholdManual` | 859 | `double` | `0.70` | **Sim** (855) | **Sim** — Name = `"Manual Threshold"` (857) | **ML Filter** | **5** |

**Observações adicionais (workspace):**

- `MlThresholdManual` possui também `[Range(0.40, 0.95)]` na linha 856.
- Propriedade correlata da Fase 4A (não solicitada na checklist, mas relevante): `ShadowMlModelDirectory` (linha 833), grupo **Shadow ML**, Order **2**, default `string.Empty` (linha 83), com `[NinjaScriptProperty]` e `[Display]`.
- O enum `UltronMlFilterMode` está em `NinjaTrader/Shared/UltronMlFilterMode.cs` (`Disabled=0`, `Shadow=1`, `Active=2`). Não usa `TypeConverter` customizado — enums públicos no mesmo assembly são suportados nativamente pelo NT8.
- O workspace importa `global::UltronML` (linha 18) e usa `UltronMlHost` (campo `_mlHost`, linha 41; `InitializeMlHost`, linhas 708–734).

**Veredito sobre atributos no workspace:** Configuração **correta** para exibição na Property Grid do NinjaTrader. Se o código fosse deployado e compilado com sucesso, os grupos **Shadow ML** e **ML Filter** deveriam aparecer.

---

## 3. As propriedades aparecem na janela de propriedades do NinjaTrader?

**Resposta: NÃO** (no ambiente auditado).

### 3.1 Evidência no assembly compilado

Arquivo: `Documents\NinjaTrader 8\bin\Custom\NinjaTrader.Custom.dll`  
Última escrita: **26/06/2026 11:19:54**

Busca por strings UTF-8 no DLL:

| Símbolo | Presente no DLL? |
|---------|------------------|
| `EnableSim101DebugLog` | **Sim** (última propriedade da versão antiga) |
| `EnableShadowMl` | **Não** |
| `MlFilterMode` | **Não** |
| `EnableMlFilter` | **Não** |
| `ForcePassThrough` | **Não** |
| `MlUseAutoThreshold` | **Não** |
| `MlThresholdManual` | **Não** |
| `ShadowMlModelDirectory` | **Não** |
| `UltronMlFilterMode` | **Não** |
| `UltronMlHost` | **Não** |

O XML de documentação gerado (`NinjaTrader.Custom.xml`) também **não referencia** nenhuma das propriedades ML — apenas propriedades pré-4B (ex.: `Quantity`, `ExecutionMode`, `EnableSim101DebugLog`).

### 3.2 Evidência em runtime

Nos logs em `Documents\NinjaTrader 8\UltronV1\diagnostics\` **não há** ocorrências de `[ML FILTER]` ou `Shadow ML`, o que confirma que a strategy em execução nunca inicializou o host ML (`InitializeMlHost` imprime essas mensagens quando ativo).

---

## 4. Por que exatamente as propriedades não aparecem

O NinjaTrader exibe propriedades de strategies a partir do **assembly compilado** (`NinjaTrader.Custom.dll`), não do arquivo `.cs` no workspace de desenvolvimento. A cadeia causal é:

```
Workspace (Fase 4B completa)
        ↓  [NÃO COPIADO]
Custom\Strategies\UltronV1Paper.cs (versão antiga, sem ML)
        ↓  [Compilação NT8 F5 às 11:19]
NinjaTrader.Custom.dll (sem propriedades ML)
        ↓
Property Grid mostra apenas propriedades da versão antiga
```

**Causa raiz (única e determinística):** O código Fase 4A/4B existe no repositório/workspace, mas **não foi implantado** na pasta `Custom` do NinjaTrader. O NT8 compilou e carrega a versão **anterior** da strategy.

**O que NÃO é a causa** (no workspace):

- Falta de `[NinjaScriptProperty]` — todas as 6 propriedades possuem o atributo.
- Falta de `[Display]` — todas possuem GroupName e Order.
- Enum `UltronMlFilterMode` mal definido — enum público válido em `NinjaTrader.NinjaScript.Ultron`.

**O que SERIA causa adicional se alguém copiasse só o `.cs` sem o restante** (cenário hipotético de deploy parcial — **não testado porque o deploy nem chegou a esse ponto**):

1. Compilação falharia por `using global::UltronML` sem `UltronML.Core.dll` em Custom.
2. Compilação falharia por tipos ausentes: `UltronMlHost`, `UltronMlFilterMode`, `UltronShadowFeatureBuilder`, `UltronShadowPredictionSink`.
3. `NinjaTrader.Custom.csproj` não lista os `.cs` ML em `<Compile Include=…>`.
4. NT8 manteria o último DLL compilado com sucesso (comportamento padrão) — propriedades antigas permaneceriam.

No estado atual, o deploy parcial **nem foi tentado** — o `.cs` em Custom simplesmente não contém as propriedades.

---

## 5. Comparação workspace vs `Custom\Strategies`

| Métrica | Workspace | Custom (`Documents\NinjaTrader 8\bin\Custom\Strategies`) |
|---------|-----------|----------------------------------------------------------|
| Caminho | `UltronV1/NinjaTrader/UltronV1Paper.cs` | `UltronV1Paper.cs` |
| Linhas | **864** | **748** |
| Tamanho | **34 320 bytes** | **29 057 bytes** |
| SHA256 | `5A07BC1641B88A43DBFD720CD59E90F98C5C5E07F799A852C7E28A82D053E8F3` | `E0EED803C3779CE33EB9A12E8146E19EDE63CD6AADE0EB7D869110BE5E1B3E8F` |
| Última modificação | **26/06/2026 11:15:34** | **26/06/2026 10:26:10** |
| `using global::UltronML` | **Sim** (linha 18) | **Não** |
| Campo `_mlHost` / `UltronMlHost` | **Sim** | **Não** |
| Propriedades Shadow ML / ML Filter | **Sim** (825–859) | **Não** — `#endregion` na linha 745 após `EnableSim101DebugLog` |
| `InitializeMlHost` / `EvaluateEntry` | **Sim** | **Não** |

**Veredito:** Os arquivos **não são o mesmo**. O Custom está **~49 minutos mais antigo** que o workspace e corresponde à versão com migração ATM (Execution Mode, Quantity, etc.) mas **sem** RC2 Fase 4A/4B.

---

## 6. Fase 4B copiada para Custom?

**Não.**

### 6.1 Arquivos Shared ausentes em Custom

Comparação `UltronV1/NinjaTrader/Shared/` vs `Custom/Ultron/Shared/`:

| Arquivo (Fase 4A/4B) | No workspace | Em Custom |
|----------------------|:------------:|:---------:|
| `UltronMlFilterMode.cs` | Sim | **Não** |
| `UltronMlHost.cs` | Sim | **Não** |
| `UltronShadowFeatureBuilder.cs` | Sim | **Não** |
| `UltronShadowPredictionSink.cs` | Sim | **Não** |
| `UltronShadowMlHost.cs` (legado 4A) | Sim | **Não** |

Arquivos presentes em Custom (9 arquivos): apenas infra Ultron pré-ML (`UltronPaperExecutionMode`, `UltronPipelineDiagnostics`, `UltronExecutionTelemetry`, etc.).

### 6.2 DLL e referências

| Artefato | Workspace / esperado Fase 4 | Em Custom |
|----------|----------------------------|-----------|
| `UltronML.Core.dll` | Necessário (`using UltronML`, ONNX) | **Ausente** |
| `UltronV1.Core.dll` | Sim | **Presente** (25/06/2026) |
| Referência `UltronML.Core` em `NinjaTrader.Custom.csproj` | Necessária | **Ausente** |
| `<Compile Include>` para `.cs` ML Shared | Necessário | **Ausente** |

O `README.md` do projeto documenta deploy apenas de `UltronV1.Core.dll` e `UltronV1.cs` — **não inclui** o checklist de deploy da Fase 4 documentado em `RC2_PHASE4_SHADOW_REPORT.md` (seção 11: copiar `UltronML.Core.dll`, ONNX runtime, atualizar `UltronV1Paper.cs` + Shared).

---

## 7. Compilação utilizando versão antiga

**Sim — confirmado.**

| Evento | Timestamp | Implicação |
|--------|-----------|------------|
| `Custom\Strategies\UltronV1Paper.cs` salvo | 10:26:10 | Versão **sem** ML |
| Workspace `UltronV1Paper.cs` atualizado (Fase 4B) | 11:15:34 | **Não propagado** para Custom |
| `NinjaTrader.Custom.dll` gerado | 11:19:54 | Compilou a fonte **Custom** (antiga) |

A compilação às 11:19 foi **bem-sucedida** (DLL atualizada), mas a partir do `.cs` antigo. O NinjaTrader **não** recompila automaticamente a partir do workspace em `Downloads\…` — só usa arquivos em `Documents\NinjaTrader 8\bin\Custom\`.

Propriedades visíveis na UI hoje (confirmadas no DLL):

- Grupo **Ultron Paper**: `PaperStopLossTicks`, `PaperTakeProfitTicks`, `PaperMaxBarsInTrade`, `MinTierForEntry`, diagnostics, `EnableSim101DebugLog`
- Grupo **Execution**: `ExecutionMode`, `AccountName`, `AtmTemplateName`, `Quantity`

Propriedades **ausentes** na UI: todos os itens dos grupos **Shadow ML** e **ML Filter**.

---

## 8. Checklist de verificação solicitada

| # | Verificação | Status |
|---|-------------|--------|
| 1 | `MlFilterMode` no workspace | ✅ Linha 838 |
| 1 | `EnableMlFilter` no workspace | ✅ Linha 843 |
| 1 | `EnableShadowMl` no workspace | ✅ Linha 828 |
| 1 | `ForcePassThrough` no workspace | ✅ Linha 848 |
| 1 | `MlUseAutoThreshold` no workspace | ✅ Linha 853 |
| 1 | `MlThresholdManual` no workspace | ✅ Linha 859 |
| 2 | Metadados (tipo, default, atributos) | ✅ Seção 2 |
| 3 | Aparecem na Property Grid NT8 | ❌ Não |
| 4 | Explicação da ausência | ✅ Seção 4 — deploy não realizado |
| 5 | Workspace == Custom | ❌ Arquivos diferentes |
| 6 | Fase 4B em Custom | ❌ Não copiada |
| 7 | Compilação com versão antiga | ✅ DLL sem símbolos ML |

---

## 9. O que seria necessário para as propriedades aparecerem (informativo — sem implementação)

Esta seção documenta o gap; **nenhuma ação foi executada** nesta auditoria.

1. Copiar `UltronV1Paper.cs` atualizado para `Custom\Strategies\`.
2. Copiar os 4–5 arquivos Shared ML para `Custom\Ultron\Shared\`.
3. Build Release de `UltronML.Core` + copiar `UltronML.Core.dll` (e dependências ONNX, conforme `RC2_PHASE4_SHADOW_REPORT.md`) para `Custom\`.
4. Adicionar referências e `<Compile Include>` no `NinjaTrader.Custom.csproj` (ou deixar o NT8 regenerar via Editor).
5. Compilar no NinjaScript Editor (F5) **sem erros**.
6. Reaplicar a strategy no chart — os grupos **Shadow ML** e **ML Filter** devem surgir.

Enquanto o passo 1 não for feito (ou a compilação falhar nos passos 3–5), o NT8 continuará exibindo a strategy da Fase 5 + ATM, **sem** UI ML.

---

## 10. Conclusão

As propriedades ML da RC2 Fase 4A/4B estão **implementadas corretamente no workspace** (`UltronV1/NinjaTrader/UltronV1Paper.cs`), com `[NinjaScriptProperty]`, `[Display]`, defaults seguros e grupos dedicados. Elas **não aparecem** no NinjaTrader porque:

1. O arquivo em `Documents\NinjaTrader 8\bin\Custom\Strategies\UltronV1Paper.cs` é uma **versão anterior** que termina em `EnableSim101DebugLog` e não declara nenhuma propriedade ML.
2. O assembly `NinjaTrader.Custom.dll` em uso foi compilado dessa fonte antiga e **não contém** os membros ML.
3. Os artefatos de suporte da Fase 4B (`UltronMlHost`, `UltronMlFilterMode`, `UltronML.Core.dll`, etc.) **não existem** na pasta Custom.

**Diagnóstico final:** Problema de **deploy/sincronização**, não de **modelagem de propriedades NinjaScript** no código-fonte do workspace.

---

*Auditoria RC2 Fase 4B UI — somente leitura, sem alteração de código.*
