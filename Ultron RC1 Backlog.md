# Ultron V1 — RC1 Technical Backlog

**Status:** Documento oficial — derivado da Auditoria Técnica (Principal Engineer)  
**Objetivo:** Preparar Release Candidate 1 sem novas funcionalidades  
**Arquitetura:** Congelada — somente correções estruturais, dívida técnica e estabilização  
**Destino final:** Coleta oficial de dados em Replay conforme [Ultron Validation Protocol v1](Ultron%20Validation%20Protocol%20v1.md)

---

## Regras invioláveis desta fase

| # | Regra |
|---|--------|
| 1 | Não alterar regras de trading |
| 2 | Não alterar pesos do score |
| 3 | Não alterar Hard Gates |
| 4 | Não alterar Machine Learning (lógica advisory, thresholds, ONNX flow) |
| 5 | Não alterar lógica de entrada |
| 6 | Não alterar lógica operacional da Strategy (sinais, SL/TP ticks, max bars) |
| 7 | Não alterar Validation Framework (escopo, métricas, reports) |

**Permitido:** refatoração estrutural, extração de código compartilhado, testes, docs, versionamento, remoção de código morto, observabilidade.

---

## Legenda

| Campo | Significado |
|-------|-------------|
| **Risco** | Probabilidade de regressão comportamental (Baixo / Médio / Alto) |
| **Esforço** | S (≤4h) · M (1–2d) · L (3–5d) |
| **Deps** | Itens que devem ser concluídos antes |

---

## Visão das Sprints

| Sprint | Foco | Itens | Entregável |
|--------|------|-------|------------|
| **S1** | Integridade de dados | C1, C2, C3, C6 | Dataset/ML confiáveis, observável |
| **S2** | Redução de duplicação | C4, C5, C7 | Hosts NT + Python DRY |
| **S3** | Testes e qualidade | C8, I6 | CI verificável C# + Python |
| **S4** | RC1 | Dead code, docs, build, version | Tag `1.0.0-rc1` |

---

# Sprint 1 — Integridade de Dados

**Meta:** Zero mudança comportamental. Consistência de dataset, versionamento rastreável, falhas observáveis.

---

## S1-C1 — DatasetRecordBuilder vs SignalEvaluator

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | C1 |
| **Problema** | `DatasetRecordBuilder` infere `direction`, `archetype` e `scoreBreakdown` via heurística quando `EvaluatedDirection == None`, divergindo do `SignalEvaluator` em rejeições em gate/score. |
| **Impacto** | **Crítico.** `opportunities.jsonl` pode conter direção/score/tier inconsistentes com a decisão real. Contamina ML, Validation Protocol e auditoria quant. |
| **Solução** | Introduzir `EvaluationSnapshot` (ou campos finais explícitos em `EvaluationResult`) preenchidos **somente** pelo `SignalEvaluator` no ponto de saída. `DatasetRecordBuilder` consome exclusivamente esse snapshot — sem fallback `LongScore >= ShortScore`. Para rejeições pré-direção: gravar `direction=None`/`"None"`, `tier` e `rejectionStage` reais; score breakdown vazio ou lado avaliado quando aplicável. |
| **Risco** | **Médio** — muda formato de alguns registros rejeitados (mais honestos). Comportamento de trading inalterado. |
| **Esforço** | **M** |
| **Deps** | Nenhuma |
| **Aceite** | (1) Teste unitário: rejeição em HardGate grava `direction` coerente com `rejectionStage`, sem inferência de score. (2) Teste: sinal aceito grava mesmos campos que `UltronSignal`. (3) `dotnet test` 100% pass. (4) Diff manual de 10 registros rejeitados antes/depois documentado — apenas metadata, zero change em Accepted. |

**Arquivos esperados:** `EvaluationResult.cs`, `SignalEvaluator.cs`, `DatasetRecordBuilder.cs`, `OpportunityJsonWriter.cs` (se serialização de direction null), `DatasetEngineTests.cs`

---

## S1-C2 — Fonte única de feature names

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | C2 |
| **Problema** | 5 cópias de `FEATURE_NAMES`: `FeatureSchema`, `FeatureNameRegistry`, JSON schema, `ultron_ml/constants.py`, `ultron_validation/.../ultron_loader.py`. |
| **Impacto** | **Crítico.** Drift silencioso entre Core, dataset JSONL e Python → treino/validação com features permutadas. |
| **Solução** | **Core como fonte de verdade.** (1) Remover `FeatureNameRegistry`; `OpportunityJsonWriter` usa `FeatureSchema.FeatureNames`. (2) Script de build RC1 `tools/export_feature_schema.py` (ou MSBuild target) gera `ultron_mnq_feature_schema_v1.json` + `feature_names.json` a partir do Core. (3) Python Training e Validation importam `feature_names.json` em runtime — remover listas duplicadas em `constants.py` e `ultron_loader.py`. **Não alterar ordem/índices das 40 features.** |
| **Risco** | **Baixo** se gerado automaticamente; **Médio** se hand-edit Python. |
| **Esforço** | **M** |
| **Deps** | Nenhuma |
| **Aceite** | (1) Single source: `FeatureSchema.FeatureNames`. (2) JSON em disco byte-equal ao array Core. (3) Python loaders leem JSON — zero listas hardcoded. (4) Teste C#: count=40, ordem igual ao JSON. (5) Teste Python (S3): validação de schema passa com JSON exportado. |

**Arquivos esperados:** `FeatureSchema.cs`, `OpportunityJsonWriter.cs`, `ultron_mnq_feature_schema_v1.json`, `tools/export_feature_schema.py`, `ultron_ml/constants.py`, `ultron_validation/data/ultron_loader.py`

---

## S1-C3 — Score profile versionado

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | C3 |
| **Problema** | `ScoreVersion = 1` é constante; pesos em `UltronConfig` não são refletidos no versionamento de dataset/manifest. |
| **Impacto** | **Alto.** Impossível reproduzir experimentos; Validation Protocol exige rastreabilidade. |
| **Solução** | Criar `ScoreProfileV1` estático (readonly) espelhando pesos **atuais** de `UltronConfig.Default` — **sem alterar valores**. `UltronVersions.ScoreVersion` permanece `1`; adicionar `ScoreProfileHash` (SHA256 dos pesos+thresholds de score, **excluindo** flags ML/paper/dataset). Gravar `scoreProfileHash` em `OpportunityRecord`, `DatasetManifest`, `training_manifest.json` (via export). Documentar: mudança futura de peso exige bump de hash manual. |
| **Risco** | **Baixo** — additive metadata only. |
| **Esforço** | **S** |
| **Deps** | S1-C2 (manifest fields consistentes) — soft |
| **Aceite** | (1) Hash determinístico: rebuilds idênticos → mesmo hash. (2) `opportunities.jsonl` contém `scoreProfileHash`. (3) Hash calculado a partir dos defaults atuais — confirmado em teste golden. (4) Nenhum peso/threshold alterado. |

**Arquivos esperados:** `UltronVersions.cs`, `ScoreProfileV1.cs` (novo), `DatasetManifest.cs`, `OpportunityRecord.cs`, `OpportunityJsonWriter.cs`, `DatasetEngineTests.cs`

---

## S1-C6 — Observabilidade de falhas ONNX e I/O

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | C6 |
| **Problema** | `OnnxInferenceEngine` engole exceções (`catch { _session = null; }`) sem log. Falhas de I/O dataset não passam por logger estruturado. |
| **Impacto** | **Alto.** ML advisory pode operar em heurística sem diagnóstico; sessões dataset corrompidas passam despercebidas. |
| **Solução** | (1) Injetar `IUltronLogger` opcional em `OnnxInferenceEngine`/`MlAdvisor`. (2) `catch` → `_logger?.Warn/Error` com mensagem + tipo exceção (sem stack no hot path). (3) `JsonLinesDatasetSink`: log Error via callback `Action<string>` ou logger injetado em falha Write/Dispose. **Não alterar fallback heurístico ONNX** — apenas tornar visível. |
| **Risco** | **Baixo** |
| **Esforço** | **S** |
| **Deps** | Nenhuma |
| **Aceite** | (1) Teste: ONNX path inválido → logger recebe ≥1 Error/Warn. (2) Teste: sink com path inválido → exceção propagada ou logada (política documentada). (3) Zero change em scores/sinais com ML desabilitado. (4) `Warn` usado ≥1 vez no codebase. |

**Arquivos esperados:** `OnnxInferenceEngine.cs`, `MlAdvisor.cs`, `JsonLinesDatasetSink.cs`, `IUltronLogger.cs`, `MlAdvisorTests.cs`

---

### Sprint 1 — Exit Criteria

| Entregável | Critério |
|------------|----------|
| Arquivos alterados | Lista commitada no changelog S1 |
| Justificativa | PR/commit message referencia C1/C2/C3/C6 |
| Testes | `dotnet test UltronV1.Tests -c Release` — 0 failures; novos testes C1/C3/C6 |
| Riscos remanescentes | Registros rejeitados históricos pré-S1 incompatíveis (documentar) |
| Checklist auditoria | C1 ☑ C2 ☑ C3 ☑ C6 ☑ |

**Gate S1 → S2:** Todos aceites S1 verdes. Nenhum peso/gate/score threshold alterado (diff grep em `UltronConfig` defaults).

---

# Sprint 2 — Redução de Duplicação

**Meta:** Eliminar duplicação mantendo comportamento byte-for-byte equivalente em replay.

---

## S2-C4 — Host NT8 compartilhado (Indicator + Paper)

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | C4 |
| **Problema** | ~200 LOC duplicadas: `UpdateVwap`, `BuildIndicatorSnapshot`, `OnMarketData` tick feed, `NtPrintLogger`, wiring EMA/ADX/ATR/RSI/Donchian, MTF series. |
| **Impacto** | **Alto.** Bugfixes em indicadores exigem dupla manutenção; divergência Indicator vs Paper invalida outcomes. |
| **Solução** | Extrair para `NinjaTrader/Shared/` (ou `UltronV1.Core/Nt/` se possível sem ref NT — preferir Shared NT): `UltronVwapTracker`, `UltronIndicatorBundle` (holds indicator refs + snapshot builder), `UltronNtLogger`, `UltronTickFeed`. Indicator e Paper **delegam** — zero lógica duplicada. **Não mover** lógica de sinais/draw/comparison/paper trade. |
| **Risco** | **Médio** — refactor mecânico; validar replay diff. |
| **Esforço** | **L** |
| **Deps** | S1 completo |
| **Aceite** | (1) `UltronV1.cs` e `UltronV1Paper.cs` reduzem ≥150 LOC cada. (2) Teste manual replay 1 sessão: mesmo contagem de sinais Indicator vs Paper (dataset opportunities). (3) `dotnet build` + NT8 compile F5. (4) Nenhuma property default alterada. |

**Arquivos esperados:** `NinjaTrader/Shared/*.cs`, `UltronV1.cs`, `UltronV1Paper.cs`

---

## S2-C5 — Pacote Python compartilhado

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | C5 |
| **Problema** | `ultron_loader.py`, métricas PF/expectancy, constants duplicados em Training e Validation. |
| **Impacto** | **Alto.** Correção em um pipeline não propaga; Validation vs Training divergem. |
| **Solução** | Criar `UltronV1.Python/ultron_data/` (ou `UltronV1.SharedPython/`): `loader.py`, `metrics.py`, `constants.py` (lê `feature_names.json` de S1-C2). Training e Validation passam a `pip install -e ../UltronV1.Python` ou path relativo. **Não alterar** algoritmos de walk-forward, sweep, Monte Carlo do Validation — apenas imports. |
| **Risco** | **Médio** — path/import breakage |
| **Esforço** | **M** |
| **Deps** | S1-C2 (feature_names.json) |
| **Aceite** | (1) Zero duplicação de `FEATURE_NAMES` e `_flatten_outcome` em Training/Validation. (2) `validate.py --synthetic` produz report idêntico ao baseline (diff summary.json). (3) `train.py --synthetic` produz modelo com mesmas métricas ±ε float. (4) README atualizado com install único. |

**Arquivos esperados:** `UltronV1.Python/ultron_data/*`, `UltronV1.Training/`, `UltronV1.Validation/` (imports only)

---

## S2-C7 — Execução paper: path autoritativo único

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | C7 |
| **Problema** | `UltronV1Paper` grava outcomes via `TradeCycleTracker` (bar OHLC) **e** submete ordens Sim101 — paths divergentes. |
| **Impacto** | **Alto.** Outcomes no dataset podem ≠ fills visíveis; Validation Protocol inválido. |
| **Solução** | **Documentar + configurar, sem mudar lógica de SL/TP/ticks.** (1) Declarar oficialmente: `TradeCycleTracker` / `PaperBarSim` = **fonte autoritativa** para `outcomes.jsonl`. (2) Default `SubmitSimOrders = false` em RC1 (ordem Sim101 opt-in visual). (3) Log quando Sim101 ativo: `"Sim101 visual only — outcome source PaperBarSim"`. **Não alterar** StopLossTicks/TakeProfitTicks/MaxBars. Regra 6 preservada: mesma lógica, apenas default de submit. |
| **Risco** | **Baixo** |
| **Esforço** | **S** |
| **Deps** | S2-C4 (Paper host refactored) |
| **Aceite** | (1) `Fase5-OutcomeCollection.md` declara path autoritativo. (2) Default `SubmitSimOrders=false`. (3) Com Sim101 off, outcomes idênticos ao baseline S1. (4) `executionMode` sempre `PaperBarSim` em outcomes. |

**Arquivos esperados:** `UltronV1Paper.cs` (default only + log), `Fase5-OutcomeCollection.md`

---

### Sprint 2 — Exit Criteria

| Entregável | Critério |
|------------|----------|
| Testes | `dotnet test` pass; Python synthetic validate + train pass |
| Diff replay | 1 sessão replay: signal count ±0 vs pre-S2 |
| Checklist | C4 ☐ C5 ☐ C7 ☐ |

**Gate S2 → S3:** Duplicação NT/Python eliminada conforme aceites. Validation Framework **não modificado** (apenas imports).

---

# Sprint 3 — Testes e Qualidade

**Meta:** Cobertura mínima para RC1 e CI reproduzível.

---

## S3-C8 — Testes Python (pytest)

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | C8 |
| **Problema** | Zero testes em Training e Validation. |
| **Impacto** | **Crítico.** Pipeline ML/Validation não verificável; regressões silenciosas. |
| **Solução** | Adicionar `UltronV1.Python/tests/` com pytest: (1) `test_loader.py` — flatten, join outcomes, schema 40 features. (2) `test_metrics.py` — PF, expectancy golden values. (3) `test_comparator.py` — overlap Both/Uonly synthetic. (4) `test_e2e_synthetic.py` — `validate.py --synthetic` exit 0 + summary keys. **Não alterar** lógica de negócio — apenas testes. |
| **Risco** | **Baixo** |
| **Esforço** | **M** |
| **Deps** | S2-C5 |
| **Aceite** | (1) `pytest` ≥15 tests, 0 failures. (2) CI script `scripts/test_python.ps1` documentado. (3) Coverage loader + metrics ≥80%. |

**Arquivos esperados:** `UltronV1.Python/tests/*`, `scripts/test_python.ps1`, `requirements-dev.txt`

---

## S3-I6 — Cobertura C# (FeatureEngine + integração outcome)

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | I6 |
| **Problema** | Sem testes para `FeatureEngine` (~355 LOC), integração Engine→Dataset→TradeCycle→EnrichOutcome, `EnrichOutcome` negativo, `DatasetRecordBuilder` edge cases. |
| **Impacto** | **Alto.** Maior superfície de risco sem rede de segurança. |
| **Solução** | Novos testes xUnit: (1) `FeatureEngineTests` — ER, RVOL monotonic, archetype flags com snapshots fixos (golden bars). (2) `OutcomePipelineTests` — `UltronEngine` mock bar sequence → accepted signal → `TradeCycleTracker` → `EnrichOutcome` → sink in-memory. (3) `DatasetEngineTests` — EnrichOutcome signalId inválido → false. (4) `DatasetRecordBuilderTests` pós-S1-C1. **Não alterar** FeatureEngine logic — apenas asserções. |
| **Risco** | **Baixo** (test-only) |
| **Esforço** | **L** |
| **Deps** | S1-C1, S1-C3 |
| **Aceite** | (1) ≥10 novos testes C#. (2) `FeatureEngine` coberto em ≥5 cenários. (3) Pipeline integração 1 teste end-to-end in-memory. (4) Total `dotnet test` ≥45 tests, 0 failures. |

**Arquivos esperados:** `UltronV1.Tests/Features/FeatureEngineTests.cs`, `UltronV1.Tests/Integration/OutcomePipelineTests.cs`, `UltronV1.Tests/Dataset/DatasetRecordBuilderTests.cs`

---

### Sprint 3 — Exit Criteria

| Entregável | Critério |
|------------|----------|
| CI local | `scripts/test_all.ps1` roda dotnet + pytest |
| Checklist | C8 ☐ I6 ☐ |

**Gate S3 → S4:** All tests green. Baseline de contagem documentado.

---

# Sprint 4 — RC1

**Meta:** Tag `1.0.0-rc1`, documentação operacional, pronto para coleta Replay oficial.

---

## S4-DC — Remoção de código morto

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | Código morto (Seção 6) |
| **Problema** | `MarketContext`, `FeatureVector.Clone()`, footprint não exportado, `TryGetAcceptedRecord`, `CurrentOrderFlow` unused, `FeatureNameRegistry` (pós-S1-C2), `_vwapResetToday`, `OutcomeLinked` never set. |
| **Impacto** | **Médio.** Confusão, CPU footprint, superfície de manutenção. |
| **Solução** | Remover ou mar `[Obsolete]` com issue: (1) Delete `MarketContext.cs`. (2) Remove `Clone()` ou internalize. (3) **Footprint:** desabilitar `UpdateFootprint` atrás de `const bool EnableFootprint = false` — **zero change** em features exportadas. (4) Remove unused APIs ou document `#if DEBUG` only. (5) Set `OutcomeLinked=true` em `EnrichOutcome` success (metadata only). |
| **Risco** | **Baixo** se footprint disabled; **Médio** se delete público API |
| **Esforço** | **M** |
| **Deps** | S1, S2, S3 |
| **Aceite** | (1) Nenhum reference compile error. (2) `dotnet test` pass. (3) OrderFlow features byte-equal antes/depois em teste golden tick. (4) LOC Core reduz ≥100. |

---

## S4-NS — Namespaces e organização

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | DT-A |
| **Problema** | NT Shared novo; Python monorepo; `Execution/` naming. |
| **Impacto** | **Baixo.** Onboarding e navegação. |
| **Solução** | (1) Documentar árvore oficial em `ARCHITECTURE.md`. (2) Garantir `NinjaTrader/Shared/`, `NinjaTrader/Indicators/` (symlink doc), `NinjaTrader/Strategies/`. (3) Não renomear namespaces Core — congelado. |
| **Risco** | **Baixo** |
| **Esforço** | **S** |
| **Deps** | S2-C4, S2-C5 |
| **Aceite** | `ARCHITECTURE.md` reflete pastas reais. Zero rename breaking public API Core. |

---

## S4-DOC — Documentação unificada

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | I9 |
| **Problema** | README desatualizado ("Phase 0"), docs fragmentados, deploy não unificado. |
| **Impacto** | **Médio.** Erro operacional na coleta Replay. |
| **Solução** | (1) `README.md` → visão RC1 + links. (2) `DEPLOY.md` — build Core, NT8 indicator+paper, Python env, paths dataset. (3) `CHANGELOG.md` — S1–S4. (4) Atualizar `Fase5-OutcomeCollection.md` com C7. (5) **Não reescrever** Validation Protocol. |
| **Risco** | **Baixo** |
| **Esforço** | **M** |
| **Deps** | S1–S3 |
| **Aceite** | Operador consegue seguir DEPLOY.md cold start ≤30 min. README lista fases corretas. |

---

## S4-BUILD — Scripts de build

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | Preparação RC1 |
| **Problema** | Sem script único build/test/export schema. |
| **Impacto** | **Médio.** Releases manuais propensos a erro. |
| **Solução** | `scripts/build.ps1`: dotnet build Release, dotnet test, export feature schema, pytest. `scripts/export_feature_schema.ps1` wrapper. |
| **Risco** | **Baixo** |
| **Esforço** | **S** |
| **Deps** | S1-C2, S3 |
| **Aceite** | `build.ps1` exit 0 em clean clone. Artefatos em `artifacts/rc1/`. |

---

## S4-DEPLOY — Scripts de deploy NT8

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | Preparação RC1 |
| **Problema** | Deploy manual copy/paste descrito em README. |
| **Impacto** | **Médio.** DLL errada, path errado. |
| **Solução** | `scripts/deploy_nt8.ps1` — copia Core.dll, Indicators, Strategies, Shared para `%USERPROFILE%\Documents\NinjaTrader 8\bin\Custom\` com confirmação. **Não instalar** automaticamente sem prompt. |
| **Risco** | **Baixo** |
| **Esforço** | **S** |
| **Deps** | S4-BUILD |
| **Aceite** | Script idempotente; lista arquivos copiados; `-WhatIf` mode. |

---

## S4-VER — Versionamento RC1

| Campo | Detalhe |
|-------|---------|
| **ID auditoria** | DT-I, I10 |
| **Problema** | Assembly 1.0.0; sem tag RC; ML não wired documentado; ONNX sempre referenciado. |
| **Impacto** | **Médio.** Ambiguidade de release. |
| **Solução** | (1) Bump `1.0.0-rc1` em csproj + `UltronVersions.UltronVersion`. (2) Git tag `v1.0.0-rc1`. (3) `RC1_CHECKLIST.md` — status auditoria. (4) Documentar: ML advisory requer wiring manual; indicador RC1 não inclui ML por design congelado. |
| **Risco** | **Baixo** |
| **Esforço** | **S** |
| **Deps** | S4 all |
| **Aceite** | (1) Manifest dataset grava `ultronVersion: "1.0.0-rc1"`. (2) CHANGELOG entry RC1. (3) RC1_CHECKLIST 100% itens críticos ☑. |

---

## S4-OPT — Itens opcionais (se sobrar capacidade)

| ID | Item | Esforço | Nota |
|----|------|---------|------|
| I4 | Lazy `BuildState()` | S | CPU only |
| I8 | Manifest flush batch | S | I/O only |
| I10 | Doc ML wiring | S | Doc only |
| C9 | Doc ComparisonTracker limitação | S | Doc only — **não alterar** tracker |

**Regra:** opcionais só entram se zero risco e zero change comportamental.

---

### Sprint 4 — Exit Criteria (RC1 Release)

| Entregável | Critério |
|------------|----------|
| Tag | `v1.0.0-rc1` |
| Testes | `build.ps1` green |
| Checklist auditoria | Todos C1–C8, I6 ☑ |
| Coleta Replay | `DEPLOY.md` + Validation Protocol v1 prontos |
| Riscos remanescentes | Documentados em `RC1_KNOWN_LIMITATIONS.md` |

---

# Matriz de rastreabilidade — Auditoria → Backlog

| ID | Sprint | Status RC1 |
|----|--------|------------|
| C1 | S1 | ☑ |
| C2 | S1 | ☑ |
| C3 | S1 | ☑ |
| C6 | S1 | ☑ |
| C4 | S2 | ☐ |
| C5 | S2 | ☐ |
| C7 | S2 | ☐ |
| C8 | S3 | ☐ |
| I6 | S3 | ☐ |
| Código morto | S4 | ☐ |
| Docs/Build/Deploy/Version | S4 | ☐ |
| C9 | S4 opt | ☐ doc |
| I1–I5, I7–I10 | Pós-RC1 | ☐ defer |
| RF1–RF5 | Pós-RC1 | ☐ defer |

---

# Itens explicitamente adiados (pós-RC1)

Conforme regras de congelamento — **não entram nesta fase:**

| ID | Motivo |
|----|--------|
| I1 Split UltronConfig | Refactor amplo — risco desnecessário pré-coleta |
| I2 / RF1 RuleRegistry | Pode alterar comportamento se extração imprecisa |
| I3 Alocações bar close | Performance — não bloqueia coleta |
| I10 Wire ML no indicador | Altera superfície ML/host |
| C9 Fix ComparisonTracker | Altera comportamento NT |
| Validation Framework changes | Regra 7 |
| Strategy logic changes | Regra 6 |
| Qualquer peso/gate/score | Regras 1–3 |

---

# RC1 Known Limitations (template — preencher na S4)

1. ML advisory não wired no indicador — host manual.
2. UltronComparisonTracker — overlap NR pode subestimar (C9 doc-only).
3. ONNX Runtime dependency em Core mesmo sem ML ativo.
4. `SimpleJsonReader` regex — suficiente para manifests controlados.
5. Datasets pré-RC1 incompatíveis com `scoreProfileHash` / direction fix.

---

# Template — Fechamento de Sprint

```markdown
## Sprint N — Closure Report

**Data:** YYYY-MM-DD
**Branch/tag:**

### Arquivos alterados
- ...

### Justificativa técnica
- ...

### Testes executados
- [ ] dotnet test UltronV1.Tests -c Release (N/N pass)
- [ ] pytest (N/N pass) [se aplicável]
- [ ] Replay manual: ...

### Riscos remanescentes
- ...

### Checklist auditoria atualizado
| ID | ☑/☐ |
```

---

# Objetivo final

Ao concluir S4, o projeto deve estar **pronto para coleta oficial Replay** conforme Ultron Validation Protocol v1:

- `UltronV1Paper` + dataset engine produzindo `opportunities.jsonl` + `outcomes.jsonl` íntegros
- Versionamento rastreável (`ultronVersion`, `scoreProfileHash`, feature schema único)
- Testes C# + Python verdes
- Deploy reproducível via scripts
- Zero alteração de lógica de trading

**Próximo passo após RC1:** iniciar estudo pre-registrado `study_protocol_UVP-2026-001` e coleta ≥40 dias replay.

---

*Documento oficial RC1 Backlog v1 — derivado da Auditoria Técnica. Alterações requerem bump de versão deste documento.*
