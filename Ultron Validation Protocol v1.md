# Ultron Validation Protocol v1

**Status:** Regra oficial do projeto  
**Escopo:** Ultron V1 — MNQ 09-26  
**Premissa:** Arquitetura congelada. Nenhuma alteração em Core, Score, Order Flow, ML ou Strategy pode ser promovida sem conformidade com este protocolo.  
**Filosofia:** *Assume failure until proven otherwise.* Toda vantagem observada é tratada como suspeita até ser refutada estatisticamente.

---

## 0. Mandato do Quant Research Lead

O papel deste protocolo não é confirmar que o Ultron funciona. É **tentar provar que não funciona** — e só permitir promoção quando a hipótese nula (H₀: *Ultron ≤ baseline*) for rejeitada com rigor.

**Baseline primária:** NightRanger operacional (mesmo instrumento, mesmo timeframe, mesma janela temporal).  
**Baseline secundária:** Ultron versão anterior (N vs N−1).  
**Baseline terciária:** Ultron sem ML (Score + Gates + OF only).

Qualquer conclusão baseada exclusivamente em replay sintético, dados sintéticos Python ou amostra < mínimos deste documento é **inválida para promoção**.

---

## 1. Revisão crítica da arquitetura (perspectiva adversarial)

### 1.1 Pontos estruturais de risco

| Área | Risco | Severidade | Por quê |
|------|-------|------------|---------|
| **Origem comum NR → Ultron** | Selection / confirmation bias | 🔴 Alta | Pesos de score (`W_Trend`, `W_Pullback`, `ScoreMinEntry=55`) foram calibrados a partir da filosofia NR. Ultron não é independente — compartilha DNA decisional. Superar NR em replay pode ser artefato de micro-ajustes, não de edge novo. |
| **Order Flow first-class** | Lookahead / fill bias | 🔴 Alta | Features OF (`Delta`, `Sweep`, `Absorption`) são computadas no fechamento da barra com ticks agregados. Em replay, tick order é determinístico; ao vivo, microestrutura difere. Vantagem OF pode não transferir. |
| **Dataset `opportunities.jsonl`** | Data leakage | 🔴 Alta | Se `outcomes.jsonl` for linkado com informação pós-barra (MFE/MAE/R) e usado para retunar gates/score/ML na mesma janela, há vazamento. Labels devem ser estritamente posteriores ao timestamp do sinal. |
| **Parameter Sweep (Fase 4.5)** | Multiple testing / overfitting | 🔴 Alta | Grid em Score × RVOL × Delta × ADX testa centenas de combinações. Sem correção (Bonferroni, Deflated Sharpe), o "melhor" conjunto é esperado por acaso. |
| **MlAdvisor advisory** | Survivorship / attribution | 🟡 Média | ML ajusta score pós-decisão. Medir "Ultron com ML" vs "NR" mistura camadas. Edge do ML deve ser isolado: `Ultron+ML` vs `Ultron−ML` na mesma amostra. |
| **UltronComparisonTracker** | Measurement bias | 🔴 Alta | NR detectado via `DrawObjects` tag `NR_SIGNAL_{barIndex}`. Pode falhar silenciosamente se objetos não forem visíveis ao host Ultron. Ultron registra sinal **Accepted**; NR registra seta **desenhada** (pós-filtros GEX/Gap/FVG). Comparar "Accepted" vs "Drawn" não é equivalência de decisão. |
| **Join temporal ±6 min** | Alignment bias | 🟡 Média | Comparator Python associa sinais por proximidade temporal. Falsos positivos de "Both" ou "UltronOnly" são possíveis em barras voláteis. |
| **Outcomes não wired (Fase 5 pendente)** | Proxy bias | 🔴 Alta | Sem `outcomes.jsonl` real, validação usa PnL do NR como proxy ou labels sintéticos. **Qualquer conclusão sobre Ultron PnL hoje é inválida.** |
| **Instrumento único MNQ 09-26** | Regime overfitting | 🟡 Média | Resultados válidos apenas para contrato/periodo testado. Rollover e mudança de microestrutura invalidam generalização. |
| **Tier system (A/B/C)** | Threshold mining | 🟡 Média | `TierAThreshold=65`, `TierBThreshold=55` são parâmetros livres. Otimizar tier cutoff ex-post infla métricas. |
| **ML Training (LightGBM)** | Overfitting + leakage | 🔴 Alta | 40 features, 200 estimators, split temporal 80/20. Com <200 trades labeled, AUC/PF OOS são instáveis. SHAP/importance em amostra pequena confirma ruído, não causalidade. |
| **Market Replay vs Live** | Execution gap | 🔴 Alta | Replay não modela slippage, fila, partial fill, latência NT8, rejeição de ordem. PF replay ≠ PF live. |
| **RecordAllBars=true** | Multiple comparisons | 🟡 Média | Dataset grava todas as barras. Análises que filtram ex-post "onde funcionou" introduzem selection bias se não pre-registradas. |

### 1.2 O que NÃO pode ser afirmado hoje

Com o estado atual do projeto, **estritamente proibido** afirmar:

1. "Ultron supera NightRanger" — sem outcomes Ultron reais + comparação pareada estatisticamente significativa.
2. "ML agrega valor" — sem A/B `EnableMlAdvisor` on/off em paper/live com mesma amostra.
3. "Parâmetros são robustos" — sem walk-forward multi-regime e correção por multiple testing.
4. "Pronto para Strategy" — Fase 5 executará capital; validação quantitativa prévia é mandatória.

### 1.3 Hipóteses nulas formais

| ID | H₀ | Rejeição exige |
|----|-----|----------------|
| H0-1 | PF(Ultron) ≤ PF(NR) | p < 0.05, bootstrap CI exclui 0, OOS confirmado |
| H0-2 | E[R\|Ultron] ≤ E[R\|NR] | Teste pareado ou Mann-Whitney em R-multiples |
| H0-3 | MaxDD(Ultron) ≥ MaxDD(NR) | Ultron DD menor em ≥70% janelas WF, significativo |
| H0-4 | ML delta ≤ 0 | A/B: score ajustado não melhora PF OOS |
| H0-5 | Edge estável no tempo | PF OOS / PF IS ≥ 0.70 em ≥3 folds consecutivos |

---

## 2. Respostas às 10 perguntas (framework + critérios)

### 2.1 O Ultron realmente supera o NightRanger?

**Resposta honesta hoje:** *Desconhecido. Evidência insuficiente.*

**Protocolo para responder:**

1. Coletar **≥60 sessões de replay** (ver Seção 4) com ambos indicadores no mesmo chart MNQ 5m, `Calculate=OnEachTick`.
2. Fontes obrigatórias:
   - `opportunities.jsonl` + `outcomes.jsonl` (Ultron, pós-Fase 5)
   - `NightRanger_trades_*.jsonl` (NR fills reais ou paper)
   - `UltronV1_NR_Comparison.csv` (overlap independente)
3. Métrica primária: **Profit Factor pareado** por janela temporal (não global).
4. Métrica secundária: Expectancy em R, Max Drawdown, Calmar, Win Rate (secundário — pode enganar).
5. Teste estatístico:
   - Bootstrap pareado (10.000 réplicas) da diferença de PF por janela.
   - H₀: mediana(ΔPF) ≤ 0. Rejeitar se p < 0.05 **e** CI₉₅(ΔPF) > 0.
6. **Só afirmar superioridade se:** passar em ≥70% janelas walk-forward **e** teste global significativo **e** efeito ≥ +10% PF relativo.

---

### 2.2 Em quais condições ele é melhor?

**Segmentação obrigatória (pré-registrada, sem fishing):**

| Dimensão | Buckets |
|----------|---------|
| Regime | TREND / TRANSITION / CHOP (`regime` field) |
| Tier | A / B / C |
| Archetype | Pullback / Breakout / Continuation |
| Order Flow | OF passed vs failed (counterfactual) |
| RVOL | <0.44 / 0.44–0.8 / >0.8 |
| Sessão | RTH open / midday / close |
| Direção | Long / Short |
| ADX | <20 / 20–30 / >30 |

**Critério "melhor":** ΔPF > 0 com n ≥ 30 trades no bucket **e** CI₉₅(ΔPF) > 0 no bucket.

**Hipótese adversarial:** Ultron só "vence" em `Tier A + TREND + OF passed` — ou seja, reduz trades e concentra nos casos fáceis. Reportar **trade count por bucket**; menos trades com PF alto não é necessariamente superioridade operacional.

---

### 2.3 Em quais condições ele é pior?

**Obrigatório reportar buckets onde ΔPF < 0 (mesmo critério n ≥ 30).**

Suspeitas a priori (testar primeiro):

- CHOP / ER baixo — gates podem divergir do NR de formas não intencionais.
- Fresh breakout — `FreshBreakoutMinScore=35` vs NR pode aceitar mais falsos breakouts.
- Short em gap up / Long em gap down — NR tem filtros Gap/GEX que Ultron não replica.
- Barras com RVOL shadow (`shadow.rvolLowWouldBlock=true`) — Ultron aceita, NR shadow-only.
- Sinais `UltronOnly` vs `Both` — se UltronOnly tem PF << NROnly, o "edge exclusivo" é negativo.

**Regra:** Se qualquer bucket pré-registrado tem ΔPF < −15% com n ≥ 30, **bloquear promoção** até explicação causal documentada.

---

### 2.4 O ganho é estatisticamente significativo?

**Bateria de testes (todos reportados, nenhum cherry-picked):**

| Teste | Uso |
|-------|-----|
| Bootstrap CI (10k) em ΔPF, ΔExpectancy | Intervalo de confiança |
| Permutation test (shuffle labels/directions) | p-value empírico |
| Mann-Whitney U | Distribuições de R-multiple não-normais |
| Deflated Sharpe Ratio (Bailey & López de Prado) | Corrigir multiple testing |
| Benjamini-Hochberg FDR | Múltiplos buckets testados |

**Significância mínima:**

- p < 0.05 (two-tailed) no teste primário (ΔPF global OOS)
- FDR q < 0.10 nos buckets secundários
- Effect size: Cohen's d ≥ 0.3 ou ΔPF relativo ≥ 10%

**Insignificante se:** p > 0.10 **ou** CI₉₅ cruza zero **ou** efeito < 5% PF.

---

### 2.5 Existe overfitting?

**Indicadores de overfitting (qualquer um = alerta vermelho):**

| Indicador | Limiar de alerta | Limiar de rejeição |
|-----------|------------------|---------------------|
| PF_OOS / PF_IS | < 0.70 | < 0.50 |
| Walk-forward consistency | < 60% janelas positivas | < 40% |
| Parameter sweep robustness | < 40% grid within 80% best | < 25% |
| ML AUC train − AUC test | > 0.10 | > 0.15 |
| Score delta mean (Both) vs OOS | IS tuning visible | — |
| Degradation live vs replay | > 20% PF drop | > 35% PF drop |

**Testes obrigatórios:**

1. Walk-forward com embargo (gap de 1 sessão entre train/test).
2. Parameter sweep com **hold-out final** (20% dias nunca usados em tuning).
3. ML: nested temporal CV; reportar gap IS/OOS AUC e PF simulado.
4. White's Reality Check ou SPA test se >10 variantes testadas.

---

### 2.6 O modelo de Machine Learning realmente agrega valor?

**Resposta honesta hoje:** *Não demonstrado. ML é advisory-only e outcomes não estão linkados.*

**Protocolo A/B isolado:**

```
Grupo A: Ultron, EnableMlAdvisor=false
Grupo B: Ultron, EnableMlAdvisor=true, modelo fixo (manifest versionado)
```

Mesma amostra, mesmas barras, mesmos gates/score base.

**Métricas ML-specific:**

| Métrica | Mínimo para considerar valor |
|---------|------------------------------|
| ΔPF (B − A) OOS | > +5% relativo, p < 0.05 |
| ΔExpectancy OOS | > 0, CI₉₅ > 0 |
| Calibration (Brier score) | B < baseline sem ML |
| Advisory attribution | Boost trades PF > Penalty trades PF |
| Latency inference ONNX | < 5ms (operacional, não estatístico) |

**H₀-ML:** ML não altera distribuição de R-multiple vs baseline.

**Rejeitar ML se:** ΔPF ≤ 0 OOS **ou** overfit gap AUC > 0.15 **ou** penalty trades destruem mais valor que boost trades criam.

**Nota crítica:** ML nunca veta — logo, não pode reduzir drawdown por exclusão. Só pode re-ranquear. Valor do ML é **marginal por definição arquitetural**.

---

### 2.7 O ganho continua em dados completamente fora da amostra?

**Definição de OOS estrito:**

| Split | % | Uso |
|-------|---|-----|
| Train / Tune | 50% | Desenvolvimento histórico (primeira metade cronológica) |
| Validation | 25% | Seleção de parâmetros, early stopping |
| Test (hold-out) | 25% | **Uma única avaliação final** — touch = contaminação |

**Regras:**

- Hold-out test **nunca** entra em parameter sweep, ML training ou threshold tuning.
- Se hold-out falhar, todo tuning anterior é invalidado — recomeçar com novo split.
- OOS temporal mínimo: **15 dias de sessão** ou **100 trades** (o que vier primeiro).

**Pass criteria OOS:**

- ΔPF(Ultron − NR) > 0 no hold-out
- p < 0.10 (teste único, limiar mais relaxado, mas efeito ≥ 10%)
- MaxDD(Ultron) ≤ MaxDD(NR) × 1.10

---

### 2.8 Qual o tamanho mínimo da amostra?

**Derivado estatístico (MNQ intraday, PF target):**

Para detectar ΔPF = +15% com power 80%, α = 0.05, assumindo σ(R) ≈ 1.2R por trade:

| Nível | Trades mínimos | Dias replay mínimos* |
|-------|----------------|----------------------|
| Exploratório (hipótese) | 80 | 20 |
| Confirmatório (promoção interna) | 150 | 40 |
| Produção (capital real) | 300 | 80 |
| ML training labeled | 200 wins+losses | 60 |

\*Assumindo ~3–5 trades/dia em MNQ 5m com filtros ativos.

**Regras absolutas:**

- n < 80 trades → **proibir conclusões de superioridade**
- n < 30 por bucket → **proibir conclusões segmentadas**
- < 10 dias de replay → **inválido para qualquer decisão**
- 1 dia positivo → **anecdota, descartar**

**Power check:** Antes de cada estudo, calcular power ex-ante. Se power < 70%, estender coleta — não interpretar resultado negativo como "Ultron não funciona" nem positivo como "funciona".

---

### 2.9 Quais métricas acompanhar continuamente?

**Dashboard operacional (diário/semanal):**

#### Tier 1 — Decisão (obrigatório)

| Métrica | Frequência | Alerta |
|---------|------------|--------|
| Profit Factor (rolling 30 trades) | Diário | < 1.0 por 30 trades |
| Expectancy (R) rolling | Diário | < 0 por 30 trades |
| Max Drawdown (rolling 60 trades) | Diário | > 2.5× mediana histórica |
| Win Rate rolling | Diário | Informativo apenas |
| Trade count vs baseline | Semanal | < 50% ou > 200% do NR |
| Ultron−NR ΔPF rolling | Semanal | < 0 por 3 semanas |

#### Tier 2 — Qualidade de sinal

| Métrica | Alerta |
|---------|--------|
| Overlap Both / Total | < 30% (sistemas divergiram demais) |
| UltronOnly PF | < 1.0 |
| Conflict rate | > 10% |
| Tier A share | > 50% (possível threshold drift) |
| OF confirmation fail rate | > 40% |
| Gate rejection rate shift | > 2σ vs baseline |

#### Tier 3 — ML (se ativo)

| Métrica | Alerta |
|---------|--------|
| Mean \|scoreDelta\| | > 6 pts (ML dominando) |
| Boost trade PF vs Penalty trade PF | Penalty > Boost |
| Inference errors / fallback rate | > 1% |
| Feature schema mismatch | Qualquer ocorrência |

#### Tier 4 — Integridade de dados

| Métrica | Alerta |
|---------|--------|
| outcomesLinked rate | < 90% |
| Timestamp monotonicity breaks | > 0 |
| Version mismatch (schema/score/config) | Qualquer ocorrência |
| Comparison CSV vs JSONL divergence | > 5% |

---

### 2.10 Quando rejeitar uma nova versão do Ultron?

**Rejeição automática (qualquer condição):**

1. Hold-out test: ΔPF ≤ 0 ou p > 0.10
2. PF_OOS / PF_IS < 0.50
3. MaxDD pior que NR em > 50% janelas WF
4. Overfit risk = HIGH no Stability Report
5. n < mínimos da Seção 2.8
6. Bucket pré-registrado crítico (CHOP, counter-gap) com ΔPF < −15%
7. ML A/B: ΔPF ≤ 0
8. Degradação live vs replay > 35% PF
9. Qualquer evidência de data leakage na auditoria
10. Version manifest incompleto (reprodutibilidade falhou)

**Rejeição por comité (2 de 3 critérios):**

- Walk-forward win rate < 55%
- Robustness score < 40%
- Live paper underperformance vs NR por 4 semanas
- Effect size < 5% PF
- Trade count reduction > 60% vs NR (over-filtering)

---

## 3. Protocolo profissional de validação

### 3.1 Fases do ciclo de vida (modelo quant desk)

```
┌─────────────┐    ┌──────────────┐    ┌─────────────┐    ┌──────────────┐
│  RESEARCH   │ →  │  VALIDATION  │ →  │   PAPER     │ →  │  PRODUCTION  │
│  (offline)  │    │  (replay)    │    │  (sim live) │    │  (capital)   │
└─────────────┘    └──────────────┘    └─────────────┘    └──────────────┘
       │                   │                   │                   │
   Sem promoção       Gate 4.5 v1         Gate Paper v1       Gate Live v1
```

Nenhum salto de fase sem gate documentado.

---

### 3.2 Coleta de dados — especificação mínima

#### Replay environment (obrigatório)

| Parâmetro | Valor |
|-----------|-------|
| Instrumento | MNQ 09-26 (rollover documentado) |
| Timeframe primário | 5 min |
| Calculate | OnEachTick |
| Período mínimo total | **80 dias de sessão** (≈4 meses) |
| Período mínimo por estudo | **40 dias** |
| Sessões excluídas | Half-days, FOMC (documentar), rollover week |
| Data feed | Mesmo provider para Ultron e NR |
| Slippage model (paper) | ≥ 1 tick entry + 1 tick exit |

#### Indicadores ativos durante coleta

| Flag | Valor |
|------|-------|
| EnableDatasetEngine | true |
| EnableComparisonMode | true |
| EnableExplainMode | true |
| RecordAllBars | true |
| EnableMlAdvisor | conforme grupo A/B |

#### Artefatos exigidos por sessão

```
UltronV1/dataset/sessions/{sessionId}/
  opportunities.jsonl
  outcomes.jsonl          ← obrigatório pós-Fase 5
  manifest.json

NightRangerLogs/
  NightRanger_trades_{date}_TF5.jsonl
  NightRanger_blocked_{date}_TF5.jsonl

UltronV1/
  UltronV1_NR_Comparison.csv
  validation_runs/{runId}/
    validation_report.html
    validation_summary.json
    protocol_checklist.json   ← pass/fail por critério
```

---

### 3.3 Divisão temporal (sem shuffle)

```
|←──────────── TRAIN 50% ────────────→|←─ VAL 25% ─→|←─ TEST 25% ─→|
     tuning permitido                  seleção        UMA avaliação
     walk-forward interno              early stop     final apenas
```

**Embargo:** 1 sessão completa entre train e validation, validation e test.

**Proibido:**

- Shuffle de trades ou barras
- K-fold random
- Normalização usando estatísticas do test set
- Retreinar ML após ver resultado do test

---

### 3.4 Walk-Forward Analysis

| Parâmetro | Valor |
|-----------|-------|
| Train window | 20 dias sessão |
| Test window | 5 dias sessão |
| Step | 5 dias sessão |
| Mínimo de janelas | 8 |
| Métrica por janela | ΔPF, ΔExpectancy, ΔMaxDD, overlap |

**Pass:** Ultron vence (ΔPF > 0) em ≥ **70%** das janelas **e** mediana(ΔPF) > 0.

**Fail:** ≤ 50% janelas positivas **ou** mediana(ΔPF) < −5%.

---

### 3.5 Monte Carlo Simulation

| Parâmetro | Valor |
|-----------|-------|
| Simulações | 10.000 |
| Método | Bootstrap sequencial (preservar autocorrelação) + shuffle alternativo |
| Métricas | PF, MaxDD, Total Return, Win Rate |
| Output | p5, p50, p95, P(sequência lucrativa) |

**Pass Ultron vs NR:**

- CI₉₅(PF_Ultron) > CI₉₅(PF_NR) inferior (non-overlapping p50)
- P(MaxDD > limite) < 5% para limite pré-definido (ex: −15R)

**Fail:** CI overlap total **e** P(Ultron lucrativo) < 60%.

---

### 3.6 Testes de robustez

#### 3.6.1 Parameter perturbation (±1 step)

Perturbar cada threshold base:

- ScoreMinEntry ±5
- MinRvol ±0.05
- AdxMin ±3
- TierBThreshold ±5

**Pass:** mediana(|ΔPF|) < 20% do PF baseline.

#### 3.6.2 Cost stress

Aplicar +1, +2, +3 ticks de slippage total por trade.

**Pass:** PF > 1.0 com +2 ticks slippage.

#### 3.6.3 Subsample stability

50% random subsample (estratificado por mês), 1000 réplicas.

**Pass:** P(PF > 1.0) > 80%.

#### 3.6.4 Regime leave-one-out

Remover um regime por vez (TREND/CHOP/TRANSITION).

**Pass:** ΔPF vs NR > 0 em ≥ 2 de 3 regimes restantes.

---

### 3.7 ML-specific validation (adicional)

| Etapa | Critério |
|-------|----------|
| Feature validation | 40 features, schema v1, NaN < 5% |
| Temporal CV | AUC std < 0.08 across folds |
| Walk-forward ML | AUC_OOS > 0.52 (better than random) |
| SHAP stability | Top-5 features consistent across folds |
| A/B advisory | ΔPF > 0 OOS |
| Manifest audit | 6 artefatos presentes, autoPromote=false |
| Promotion | **Manual only** — comité assina checklist |

---

## 4. Critérios de promoção e rollback

### 4.1 Níveis de promoção

| Nível | Nome | Pré-requisito |
|-------|------|---------------|
| L0 | Research | Código mergeado, zero capital |
| L1 | Validated Replay | Gate 4.5 completo, checklist 100% pass |
| L2 | Paper Live | L1 + 20 dias paper, ΔPF vs NR > 0 |
| L3 | Production | L2 + 40 dias paper, comité aprova |

### 4.2 Checklist de promoção (L0 → L1)

Todos os itens **obrigatórios**:

- [ ] n ≥ 150 trades pareados ou comparáveis
- [ ] ≥ 40 dias de replay documentados
- [ ] Hold-out test pass (Seção 2.7)
- [ ] Walk-forward pass (Seção 3.4)
- [ ] Monte Carlo pass (Seção 3.5)
- [ ] Robustez pass (Seção 3.6)
- [ ] Overfit indicators green (Seção 2.5)
- [ ] Nenhum bucket crítico fail (Seção 2.3)
- [ ] Data leakage audit pass
- [ ] `validation_summary.json` arquivado com runId
- [ ] Version manifest: ultron + score + config + featureSchema
- [ ] Relatório HTML assinado (data + versão)

### 4.3 Critérios de rollback (Production → Paper ou Off)

**Imediato (automático):**

| Trigger | Ação |
|---------|------|
| Rolling PF < 0.8 por 20 trades | Suspend entries |
| MaxDD > 3× histórico mediano | Suspend + investigação |
| 5 perdas consecutivas > 2R cada | Suspend |
| Inference/schema error | Disable ML, fallback L0 |
| Data feed anomaly | Halt all |

**Investigação (48h):**

| Trigger | Ação |
|---------|------|
| ΔPF vs NR < 0 por 4 semanas | Rollback versão N−1 |
| Overlap Both < 20% | Recalibrar ou rollback |
| Live PF < Replay PF × 0.65 | Execution audit |

**Rollback procedure:**

1. Restaurar manifest versão N−1 (model + config)
2. Desabilitar ML se ML foi causa raiz
3. Re-run validation mínimo (10 dias) antes de re-promover
4. Post-mortem documentado em `validation_runs/{runId}/rollback.md`

---

## 5. Processo de auditoria anti-viés

### 5.1 Pre-registration (antes de coletar)

Documento `study_protocol_{id}.md` contendo:

- Hipótese H₁ específica
- Métrica primária (uma só)
- Métricas secundárias
- Segmentações pré-definidas (máx. 8 buckets)
- Tamanho amostral alvo
- Critérios pass/fail
- Data de início/fim da coleta

**Alterações pós-coleta exigem novo study_id.**

### 5.2 Data leakage checklist

- [ ] Features computadas apenas com dados ≤ bar close
- [ ] Outcomes timestamp > signal timestamp
- [ ] ML train não inclui test/hold-out sessions
- [ ] Parameter sweep não usa hold-out
- [ ] NR trades usados como label Ultron documentados como proxy
- [ ] Sem filtro ex-post "remover dias ruins"

### 5.3 Replay vs Live reconciliation

Mensalmente:

| Métrica replay | Métrica live | Tolerância |
|----------------|--------------|------------|
| Signal count | Signal count | ±15% |
| Mean score | Mean score | ±3 pts |
| PF | PF | ±25% relativo |
| Avg slippage | modeled slippage | documentar delta |

Se tolerância excedida → invalidar replay conclusions para promoção.

---

## 6. Workflow operacional

### 6.1 Nova versão (Core / Score / OF / ML / Strategy)

```
1. Bump version (ultron / score / config / featureSchema)
2. Pre-register study_protocol
3. Coletar dados (mínimos Seção 3.2)
4. Executar validate.py + checklist manual
5. Quant Lead review (devil's advocate)
6. Comité: APPROVE | REJECT | EXTEND COLLECTION
7. Se APPROVE → registrar em model_registry / version manifest
8. Se REJECT → documentar causa, sem merge operacional
```

### 6.2 Ferramentas existentes (Fase 4 + 4.5)

| Ferramenta | Papel no protocolo |
|------------|-------------------|
| `UltronV1.Validation/validate.py` | Orquestrador — comparator, sweep, WF, MC, HTML |
| `UltronV1.Training/train.py` | ML training — nunca auto-promote |
| `registry.promote_model()` | Manual only, pós-checklist ML |
| `UltronComparisonTracker` | Overlap — **validar detecção NR antes de confiar** |
| `DatasetEngine` | Fonte primária de verdade para Ultron |

### 6.3 Lacunas conhecidas (must-fix antes de L1)

| Lacuna | Impacto | Mitigação |
|--------|---------|-----------|
| outcomes.jsonl vazio | Sem PnL Ultron nativo | Completar Fase 5 ou usar paper fills Ultron |
| NR detection via DrawObjects | Overlap subestimado | Export JSONL com NR score explícito |
| Parameter sweep sem Bonferroni | Falso positivo | Aplicar Deflated Sharpe / BH correction |
| Synthetic data mode | Demo only | Proibir para promoção |

---

## 7. Governança

### 7.1 Papéis

| Papel | Responsabilidade |
|-------|------------------|
| Quant Research Lead | Devil's advocate, assina checklist, pode veto |
| Operator | Coleta replay/paper, não interpreta resultados |
| Comité (≥2 pessoas ou 2 análises independentes) | Aprova L2/L3 |

### 7.2 Princípios invioláveis

1. **Arquitetura congelada** — validação não justifica refactor oportunista.
2. **ML nunca veta** — não esperar redução de DD por exclusão ML.
3. **Sem auto-promoção** — humano + checklist sempre.
4. **Hold-out sagrado** — uma touch, estudo invalidado.
5. **Negative results são resultados** — publicar falhas internamente.
6. **NR é baseline, não inimigo** — Ultron deve provar valor incremental.

### 7.3 Versionamento deste protocolo

| Versão | Data | Mudança |
|--------|------|---------|
| v1 | 2026-06-25 | Emissão inicial |

Alterações em thresholds de promoção exigem `Ultron Validation Protocol v2` com changelog.

---

## 8. Resumo executivo — veredicto atual

| Pergunta | Status v1 |
|----------|-----------|
| 1. Ultron supera NR? | **Não comprovado** — outcomes Ultron ausentes, comparação NR incompleta |
| 2. Onde é melhor? | **Indeterminado** — segmentação requer n ≥ 30/bucket |
| 3. Onde é pior? | **Suspeita:** CHOP, counter-gap, UltronOnly — testar |
| 4. Significância estatística? | **Impossível afirmar** — amostra insuficiente |
| 5. Overfitting? | **Risco alto** — pesos NR-aligned + sweep grid + ML 40 features |
| 6. ML agrega valor? | **Não demonstrado** — A/B não executado |
| 7. OOS? | **Não testado** em hold-out real |
| 8. Amostra mínima? | **150 trades / 40 dias** confirmatório; **300/80** produção |
| 9. Métricas contínuas? | Definidas Seção 2.9 |
| 10. Rejeitar quando? | Definido Seção 2.10 e 4.3 |

**Conclusão do Quant Research Lead:**

> O Ultron V1 possui infraestrutura sólida de coleta e validação (Dataset Engine, Validation Framework, ML Platform), mas **nenhuma evidência estatisticamente robusta** de superioridade sobre o NightRanger. A arquitetura compartilha DNA decisional com o NR, o que torna a barra de prova mais alta, não mais baixa. **Recomendação: HALT promoção operacional até Gate L1 completo com dados reais pós-Fase 5.**

---

## 9. Apêndice A — Template checklist JSON

```json
{
  "protocolVersion": "1.0",
  "studyId": "UVP-2026-001",
  "ultronVersion": "1.0.0",
  "scoreVersion": 1,
  "configVersion": "default",
  "collectionStart": "YYYY-MM-DD",
  "collectionEnd": "YYYY-MM-DD",
  "tradeCount": 0,
  "sessionDays": 0,
  "gates": {
    "minTrades": { "required": 150, "actual": 0, "pass": false },
    "minDays": { "required": 40, "actual": 0, "pass": false },
    "holdOutTest": { "pass": false, "deltaPF": null, "pValue": null },
    "walkForward": { "pass": false, "winRate": null },
    "monteCarlo": { "pass": false },
    "robustness": { "pass": false },
    "overfit": { "pass": false, "pfIsOosRatio": null },
    "mlAbTest": { "pass": false, "deltaPF": null },
    "dataLeakageAudit": { "pass": false },
    "criticalBuckets": { "pass": false }
  },
  "verdict": "REJECT",
  "approvedBy": null,
  "approvedAt": null
}
```

---

## 10. Apêndice B — Referências metodológicas

- Bailey, D. H., & López de Prado, M. (2014). *The Deflated Sharpe Ratio.*
- Aronson, D. (2006). *Evidence-Based Technical Analysis.*
- López de Prado, M. (2018). *Advances in Financial Machine Learning* — Ch. 7–12 (CV, backtest overfitting).
- White, H. (2000). *A Reality Check for Data Snooping.*
- NinjaTrader Market Replay limitations — documentação oficial NT8.

---

*Documento oficial. Supersedes qualquer discussão informal sobre promoção de versões. Próxima revisão: após primeiro Gate L1 completo ou acumular 150 trades reais.*
