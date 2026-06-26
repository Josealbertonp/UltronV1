# ATM Recommendations — Ultron V1

**Data da análise:** 2026-06-26  
**Escopo:** Otimização de ATM NinjaTrader para **3 contratos MNQ** — **sem alterar lógica de trading** (engine, gates, score, PaperBarSim).  
**Objetivo:** Identificar qual perfil de ATM extrai mais valor dos **mesmos sinais** que o Ultron já gera.

---

## 1. Resumo executivo

| Item | Conclusão |
|------|-----------|
| **Dataset** | **868 trades** em **12 sessões** de replay (`outcomes.jsonl` em `Documents\NinjaTrader 8\UltronV1\dataset\sessions\`) |
| **PaperBarSim atual (30 SL / 32 TP)** | WR **29,9%**, PF **0,46**, **−9.920 ticks** (−$4.960 em 1 ct MNQ) |
| **ATM proposto (60 / 58 / 100 / 140)** | **Não é o perfil ideal** para estes sinais — alvos distantes demais, stop largo demais, terceiro contrato quase nunca atinge T3 |
| **Insight principal** | **50,3% das perdas** tiveram **MFE ≥ 32 ticks** antes de fechar no stop — o edge existe, mas o PaperBarSim single-target não captura |
| **Recomendação principal** | **ATM-6 — Ultron Scalp 3-Layer** (32 SL / 28 / 45 / 60, 1+1+1 ct, BE 24/+2, trail opcional a partir de 55) |

> **Aviso metodológico:** Os outcomes foram coletados com PaperBarSim **30/32 ticks** (1 contrato lógico). A simulação de ATM usa **MFE/MAE por trade** (não o caminho intrabar completo). Os números projetados são **estimativas orientadas por dados**, não backtest broker-grade. Validar em Sim101 antes de operar.

---

## 2. Fontes de dados utilizadas

| Fonte | Caminho / descrição | Uso |
|-------|---------------------|-----|
| **outcomes.jsonl** | `NinjaTrader 8\UltronV1\dataset\sessions\{12 sessões}\outcomes.jsonl` | MFE, MAE, PnL, duração, exit reason, direção |
| **opportunities.jsonl** | Mesmas sessões | Tentativa de join por `signalId` — arquivos com linhas malformadas; features não usadas nesta rodada |
| **Pipeline diagnostics** | `UltronV1\diagnostics\UltronV1Paper_*.log` | Funil de sinais, rejeições (TrendAcceleration, ER, NoSweepNoDelta) |
| **Execution telemetry** | `UltronV1\diagnostics\*_execution.log` | Confirmação de TryOpen / PaperBarSim (não altera cálculo ATM) |

### 2.1 Sessões analisadas

| Session ID | Trades |
|------------|--------|
| 099d08bd3000 | 83 |
| 2ca680624a95 | 69 |
| 3cb1e335498b | 77 |
| 5b173693c907 | 59 |
| 5eb5f7df6da2 | 68 |
| 5ef378b86465 | 97 |
| 74e498f5c0b2 | 61 |
| 76bba3ee5af5 | 74 |
| 7dc846a403cf | 61 |
| aff913465f11 | 82 |
| b04c9b05f6c9 | 54 |
| ede9a719ba88 | 69 |
| **Total** | **868** |

### 2.2 Contexto do replay (exemplo sessão `76bba3ee5af5`)

Ao final do replay (~1.120 barras processadas):

- **Signals Generated:** 64  
- **Accepted (evaluator):** 64  
- **Entries Submitted:** 88 (inclui sobreposição AlreadyInPosition / reentradas bloqueadas)  
- **Principais rejeições:** TrendAcceleration (465), ER (463), NoSweepNoDelta (88)

O funil confirma que os trades analisados são **sinais já filtrados** pelo Ultron — a otimização ATM assume **mesma frequência e mesmos pontos de entrada**.

---

## 3. Estatísticas do PaperBarSim coletado (baseline)

Todos os 868 trades usaram **SL = 30 ticks**, **TP = 32 ticks** (confirmado via `stopPrice` / `targetPrice`).

| Métrica | Valor |
|---------|-------|
| **Win Rate** | 29,9% (260 W / 608 L) |
| **Profit Factor** | 0,46 |
| **PnL total** | −9.920 ticks |
| **Expectancy** | −11,4 ticks/trade |
| **Avg Win** | +32,0 ticks |
| **Avg Loss** | −30,0 ticks |
| **Max Drawdown** | −9.896 ticks |
| **Exit reasons** | 599 `stop_loss`, 255 `take_profit` |
| **Direção** | Long 456 (WR 31,6%) · Short 412 (WR 28,2%) |
| **Duração** | `barsInTrade` mediana **1** barra (5 min); `secondsInTrade` mediana **300 s** |

### 3.1 Distribuição de PnL (PaperBarSim)

| Faixa (ticks) | Trades | % |
|---------------|--------|---|
| −40 a 0 (stop) | 599 | 69,0% |
| +32 a +59 (target) | 255 | 29,4% |
| Outros | 14 | 1,6% |

Distribuição **bimodal fixa** em ±30/32 — reflexo do simulador, não do potencial real dos sinais.

---

## 4. Análise MFE / MAE

### 4.1 Percentis globais (ticks)

| Percentil | MFE | MAE |
|-----------|-----|-----|
| p10 | 7 | 11 |
| p25 | 20 | 29 |
| **p50** | **49** | **55** |
| p75 | 89 | 89 |
| p90 | 160 | 175 |
| p95 | 232 | 261 |

### 4.2 MFE / MAE por resultado

| Grupo | MFE mediana | MFE média | MAE mediana | MAE média |
|-------|-------------|-----------|-------------|-----------|
| **Vencedores** | 83 | 108 | 16 | 19 |
| **Perdedores** | **32** | 57 | 76 | 123 |

**Leitura crítica:**

- Perdedores com **MFE mediana 32** → metade das perdas **visitou o território de lucro** antes de reverter ao stop de 30 ticks.
- **306 perdas (50,3%)** tiveram **MFE ≥ 32** — exatamente o alvo do PaperBarSim.
- **194 perdas (31,9%)** tiveram **MFE ≥ 58** — teriam pagado o **T1 do ATM 60/58/100/140** em pelo menos 1 contrato, mas o PaperBarSim não simula parciais.

### 4.3 Taxa de alcance por nível (MFE)

| Nível (ticks) | Trades com MFE ≥ nível | % do total |
|---------------|------------------------|------------|
| 28 | ~630 | ~72% |
| 32 | 566 | 65,2% |
| 40 | 506 | 58,3% |
| 50 | 431 | 49,7% |
| **58** | **382** | **44,0%** |
| 60 | 365 | 42,1% |
| 70 | 299 | 34,4% |
| 80 | 270 | 31,1% |
| 100 | 186 | 21,4% |
| 140 | 108 | 12,4% |
| 160 | 88 | 10,1% |

### 4.4 Taxa de stress (MAE)

| Nível (ticks) | Trades com MAE ≥ nível | % do total |
|---------------|------------------------|------------|
| 30 | 594 | 68,4% |
| 40 | 541 | 62,3% |
| 50 | 474 | 54,6% |
| **60** | 397 | **45,7%** |
| 80 | 278 | 32,0% |
| 100 | 195 | 22,5% |

---

## 5. Por que o ATM 60 / 58 / 100 / 140 **não** é ideal

Configuração avaliada: **SL 60** · **T1 58** · **T2 100** · **T3 140** · **1+1+1 contrato**.

| Problema | Evidência nos dados |
|----------|---------------------|
| **T1 (58) abaixo do potencial mediano** | MFE p50 = **49** — metade dos trades **não chega** a 58 ticks; T1 mais baixo (28–40) captura mais trades |
| **T2/T3 muito raros** | Apenas **21,4%** atingem MFE ≥ 100; **12,4%** ≥ 140 — o **3º contrato** fica ocioso na maioria dos casos |
| **SL 60 largo demais** | 45,7% dos trades têm MAE ≥ 60 — risco total 3 ct = **180 ticks** ($270) vs perfis mais apertados |
| **Desalinhamento temporal** | Mediana **1 barra** em trade — alvos de 100–140 ticks pressupõem runner longo; dados mostram resolução rápida |
| **Simulação conservadora (v2)** | WR 53,7%, PF **0,90**, total **−5.729 ticks** — ainda negativo |
| **Simulação com crédito de parciais (v3)** | WR 61,9%, PF **1,39** — melhor que PaperBarSim, mas **inferior** aos perfis scalp (PF até **1,98**) |

**Conclusão:** O ATM 60/58/100/140 foi desenhado como runner de tendência. Os sinais Ultron coletados comportam-se como **impulsos curtos com reversão frequente** (alta MFE em perdas). Um ATM **bank-first** com alvos escalonados mais baixos extrai mais valor.

---

## 6. Metodologia de simulação ATM (3 contratos)

### Modelo A — Conservador (v2)

- Se **MAE ≥ SL** e **MFE < T1** → stop full em 3 contratos.
- Se **MAE ≥ SL** e **MFE ≥ T1** → T1 no 1º contrato; restante stopado no SL.
- Se **MAE < SL** → preenche cada alvo atingido; runner com trail/BE se configurado.

### Modelo B — Crédito de parciais em perdas (v3)

- Se o trade **visitou** `MFE ≥ Tk` antes de falhar no PaperBarSim, credita o alvo no contrato k (realista para ATM com ordens OCO parciais).
- Melhor aproximação do comportamento **real** de ATM NinjaTrader quando o preço toca o alvo antes de voltar ao stop.

Ambos os modelos usam **868 entradas reais**; não re-executam gates nem score.

---

## 7. Perfis de ATM propostos (6+)

Quantidade fixa: **1 contrato por alvo (3 total)**, conforme solicitado.

---

### ATM-1 — Referência atual proposta (60 / 58 / 100 / 140)

| Parâmetro | Valor |
|-----------|-------|
| **Stop Loss** | 60 ticks |
| **Target 1** | 58 ticks · **1 ct** |
| **Target 2** | 100 ticks · **1 ct** |
| **Target 3** | 140 ticks · **1 ct** |
| **Break Even Trigger** | 55 ticks |
| **Break Even Plus** | +2 ticks |
| **Auto Trail** | Sim — após T2 preenchido |
| **Trail Step** | 20 ticks |
| **Trail Frequency** | 1 tick |
| **Início do trail** | Quando MFE ≥ 100 (após T2) |

| Métrica (v3) | Expectativa |
|--------------|-------------|
| Win Rate | ~62% |
| Profit Factor | ~1,39 |
| Drawdown (ticks, 3 ct) | ~−2.200 |
| Melhor em | Tendência direcional prolongada, baixo chop |
| Pior em | Mercado 5m rápido com wicks (maioria do dataset) |

---

### ATM-2 — Data-Driven Core (45 / 40 / 70 / 110)

Alinhado a MFE p25 / ~p50 / entre p50–p75 / p75.

| Parâmetro | Valor |
|-----------|-------|
| **Stop Loss** | 45 ticks |
| **Target 1** | 40 ticks · **1 ct** |
| **Target 2** | 70 ticks · **1 ct** |
| **Target 3** | 110 ticks · **1 ct** |
| **Break Even Trigger** | 35 ticks |
| **Break Even Plus** | +2 ticks |
| **Auto Trail** | Sim |
| **Trail Step** | 15 ticks |
| **Trail Frequency** | 1 tick |
| **Início do trail** | MFE ≥ 80 |

| Métrica (v3) | Expectativa |
|--------------|-------------|
| Win Rate | ~55% |
| Profit Factor | ~1,45 |
| Drawdown | ~−1.380 ticks |
| Melhor em | Sessões com ADX/ER moderados, impulso médio |
| Pior em | Spikes de volatilidade (MAE p90 = 175) |

---

### ATM-3 — Quick Bank (40 / 36 / 55 / 80)

| Parâmetro | Valor |
|-----------|-------|
| **Stop Loss** | 40 ticks |
| **Target 1** | 36 ticks · **1 ct** |
| **Target 2** | 55 ticks · **1 ct** |
| **Target 3** | 80 ticks · **1 ct** |
| **Break Even Trigger** | 30 ticks |
| **Break Even Plus** | +2 ticks |
| **Auto Trail** | Sim |
| **Trail Step** | 12 ticks |
| **Trail Frequency** | 1 tick |
| **Início do trail** | MFE ≥ 70 |

| Métrica (v3) | Expectativa |
|--------------|-------------|
| Win Rate | ~59% |
| Profit Factor | ~1,66 |
| Drawdown | ~−1.056 ticks |
| Melhor em | Abertura EU/US (UTC 04–08, WR 38–49% no dataset) |
| Pior em | UTC 00:00 (WR 8,3% no dataset) |

---

### ATM-4 — Wide Survival (55 / 50 / 85 / 130)

| Parâmetro | Valor |
|-----------|-------|
| **Stop Loss** | 55 ticks |
| **Target 1** | 50 ticks · **1 ct** |
| **Target 2** | 85 ticks · **1 ct** |
| **Target 3** | 130 ticks · **1 ct** |
| **Break Even Trigger** | 42 ticks |
| **Break Even Plus** | +3 ticks |
| **Auto Trail** | Sim |
| **Trail Step** | 18 ticks |
| **Trail Frequency** | 1 tick |
| **Início do trail** | MFE ≥ 90 |

| Métrica (v3) | Expectativa |
|--------------|-------------|
| Win Rate | ~59% |
| Profit Factor | ~1,35 |
| Drawdown | ~−2.136 ticks |
| Melhor em | Shorts (MFE p50 Short = 56 > Long 43) |
| Pior em | Dias de range estreito com múltiplos stop hunts |

---

### ATM-5 — BE + Trail Overlay (45 / 40 / 70 / 110 + gestão ativa)

Igual ao ATM-2 com ênfase em proteção após T1.

| Parâmetro | Valor |
|-----------|-------|
| **Stop Loss** | 45 ticks |
| **Target 1** | 40 ticks · **1 ct** |
| **Target 2** | 70 ticks · **1 ct** |
| **Target 3** | 110 ticks · **1 ct** |
| **Break Even Trigger** | 35 ticks (move stop após T1) |
| **Break Even Plus** | +2 ticks |
| **Auto Trail** | **Sim (obrigatório)** |
| **Trail Step** | 15 ticks |
| **Trail Frequency** | 1 tick |
| **Início do trail** | Imediatamente após T1 fill |

| Métrica (v3) | Expectativa |
|--------------|-------------|
| Win Rate | ~55% |
| Profit Factor | ~1,45 |
| Drawdown | ~−1.380 ticks |
| Melhor em | Trades que atingem T1 mas falham antes de T2 (31,9% das perdas com MFE≥58) |
| Pior em | Movimentos em V rápido sem tocar T1 |

---

### ATM-6 — Ultron Scalp 3-Layer ⭐ **RECOMENDADO**

Alinhado à **duração mediana de 1 barra** e ao fato de **72%** dos trades atingirem MFE ≥ 28.

| Parâmetro | Valor |
|-----------|-------|
| **Stop Loss** | **32 ticks** |
| **Target 1** | **28 ticks** · **1 ct** |
| **Target 2** | **45 ticks** · **1 ct** |
| **Target 3** | **60 ticks** · **1 ct** |
| **Break Even Trigger** | **24 ticks** |
| **Break Even Plus** | **+2 ticks** |
| **Auto Trail** | Recomendado no 3º contrato após T2 |
| **Trail Step** | **10 ticks** |
| **Trail Frequency** | **1 tick** |
| **Início do trail** | **MFE ≥ 55** (ou após T2 fill) |

| Métrica (v3) | Expectativa |
|--------------|-------------|
| Win Rate | ~59% |
| Profit Factor | **~1,98** (melhor do universo testado) |
| Expectancy | **~33 ticks/trade** (~$49,50/trade em 3 ct MNQ @ $0,50/tick) |
| Drawdown | **~−675 ticks** (menor entre perfis de alto PF) |
| Melhor em | Impulsos 5m pós-sweep, duração &lt; 10 min |
| Pior em | Sequências de stop sem MFE ≥ 24 (≈28% dos trades) |

---

### ATM-7 — Runner Heavy (50 / 45 / 75 / 150)

| Parâmetro | Valor |
|-----------|-------|
| **Stop Loss** | 50 ticks |
| **Target 1** | 45 ticks · **1 ct** |
| **Target 2** | 75 ticks · **1 ct** |
| **Target 3** | 150 ticks · **1 ct** |
| **Break Even Trigger** | 38 ticks |
| **Break Even Plus** | +2 ticks |
| **Auto Trail** | Sim |
| **Trail Step** | 20 ticks |
| **Trail Frequency** | 1 tick |
| **Início do trail** | MFE ≥ 75 |

| Métrica (v3) | Expectativa |
|--------------|-------------|
| Win Rate | ~59% |
| Profit Factor | ~1,39 |
| Drawdown | ~−1.560 ticks |
| Melhor em | Dias de tendência (MFE p90 = 160) |
| Pior em | Chop intraday — 3º contrato raramente captura 150 |

---

### ATM-8 — Tight Risk (38 / 34 / 52 / 78)

| Parâmetro | Valor |
|-----------|-------|
| **Stop Loss** | 38 ticks |
| **Target 1** | 34 ticks · **1 ct** |
| **Target 2** | 52 ticks · **1 ct** |
| **Target 3** | 78 ticks · **1 ct** |
| **Break Even Trigger** | 28 ticks |
| **Break Even Plus** | +1 tick |
| **Auto Trail** | Opcional |
| **Trail Step** | 10 ticks |
| **Trail Frequency** | 1 tick |
| **Início do trail** | MFE ≥ 60 |

| Métrica (v3) | Expectativa |
|--------------|-------------|
| Win Rate | ~59% |
| Profit Factor | ~1,75 |
| Drawdown | ~−961 ticks |
| Melhor em | Contas menores / aversão a DD |
| Pior em | Volatilidade extrema (MAE p95 = 261) |

---

## 8. Tabela comparativa

Valores de **simulação v3** (crédito de parciais por MFE) em **868 trades**, **3 contratos**.

| Perfil | SL | T1 / T2 / T3 | BE (trig/+) | Trail | WR esp. | PF esp. | Total (ticks) | MDD (ticks) |
|--------|-----|--------------|-------------|-------|---------|---------|---------------|-------------|
| PaperBarSim ref | 30 | 32 / — / — | — | — | 29,9% | 0,46 | −9.920 | −9.896 |
| **ATM-1 Atual 60/58/100/140** | 60 | 58 / 100 / 140 | 55 / +2 | 100+ | 61,9% | 1,39 | +20.870 | −2.202 |
| ATM-2 Data Core | 45 | 40 / 70 / 110 | 35 / +2 | 80+ | 55,3% | 1,45 | +19.995 | −1.380 |
| ATM-3 Quick Bank | 40 | 36 / 55 / 80 | 30 / +2 | 70+ | 58,8% | 1,66 | +25.410 | −1.056 |
| ATM-4 Wide Survival | 55 | 50 / 85 / 130 | 42 / +3 | 90+ | 59,3% | 1,35 | +18.344 | −2.136 |
| ATM-5 BE+Trail | 45 | 40 / 70 / 110 | 35 / +2 | pós-T1 | 55,3% | 1,45 | +19.995 | −1.380 |
| **ATM-6 Scalp 3-Layer ⭐** | **32** | **28 / 45 / 60** | **24 / +2** | **55+** | **58,8%** | **1,98** | **+28.602** | **−675** |
| ATM-7 Runner Heavy | 50 | 45 / 75 / 150 | 38 / +2 | 75+ | 58,5% | 1,39 | +18.857 | −1.560 |
| ATM-8 Tight Risk | 38 | 34 / 52 / 78 | 28 / +1 | 60+ | 58,8% | 1,75 | +26.572 | −961 |

### Simulação conservadora (v2) — cenário pessimista

| Perfil | WR | PF | Total (ticks) |
|--------|-----|-----|---------------|
| ATM-1 60/58/100/140 | 53,7% | 0,90 | −5.729 |
| ATM-6 Scalp 3-Layer | 30,5% | 0,79 | −8.144 |
| ATM-7 Runner Heavy | 44,7% | **0,95** | −2.658 |

Sob v2, **nenhum perfil 3 ct é lucrativo** — reforça que **parciais + BE** são essenciais; o ATM-6 depende de realizar T1 cedo.

---

## 9. Distribuição de ganhos e perdas (implicações ATM)

### Ganhos (PaperBarSim)

- **100%** dos wins com **+32 ticks** — teto artificial do simulador.
- MFE mediana dos wins = **83** → headroom de **+51 ticks** não capturado.

### Perdas (PaperBarSim)

- **100%** dos losses com **−30 ticks**.
- **50,3%** das perdas com MFE ≥ 32 → perda evitável com **T1 parcial + BE**.
- **31,9%** das perdas com MFE ≥ 58 → ATM 60/58 teria salvo **1 contrato** nesses casos, mas ainda perderia nos outros 2 com stop largo.

### Duração

- **100%** dos trades: 1–2 barras (5–10 min).
- ATM com alvos distantes (100–140) **não combina** com a curva de resolução observada.

---

## 10. Condições de mercado por perfil

| Condição | ATM favorecido | Evidência |
|----------|----------------|-----------|
| **Impulso rápido pós-sweep** | ATM-6, ATM-8 | `barsInTrade` = 1; NoSweepNoDelta nas rejeições |
| **Tendência UTC 04–08** | ATM-3, ATM-6 | WR 38–49% vs média 29,9% |
| **Short bias intraday** | ATM-4, ATM-6 | Short MFE p50 = 56 > Long 43 |
| **Alta volatilidade (MAE p90+)** | ATM-4 (SL 55) | Evita stop prematuro 30–32 |
| **Runner diário** | ATM-7 | Apenas 10% trades MFE ≥ 160 |
| **Evitar** | ATM-1 (60/140) | T3 hit rate 12,4% |
| **Evitar horário** | Todos | UTC 00:00 WR 8,3% (n=48) |

---

## 11. Recomendação final

### ⭐ ATM principal: **ATM-6 — Ultron Scalp 3-Layer**

```
Stop Loss:        32 ticks (3 contratos)
Target 1:         28 ticks × 1 contrato
Target 2:         45 ticks × 1 contrato
Target 3:         60 ticks × 1 contrato
Break Even:       Trigger 24 ticks · Plus +2 ticks (após T1)
Auto Trail:       Sim — Step 10 ticks · Frequency 1 tick · Start após T2 ou MFE ≥ 55
```

### Justificativa baseada nos dados

1. **MFE p50 = 49** e **72% ≥ 28 ticks** → T1 em 28 captura a maioria dos impulsos.
2. **50,3% das perdas** visitaram +32 ticks → bank-first com BE em 24 protege o que o PaperBarSim descartou.
3. **PF projetado 1,98** e **menor MDD (−675 ticks)** entre perfis com expectancy alta no modelo v3.
4. **Duração mediana 1 barra** → alvos 28/45/60 coerentes; 100/140 (ATM atual) atingidos por apenas 21%/12% dos trades.
5. **ATM 60/58/100/140** fica atrás em PF (1,39 vs 1,98) e expectancy, com **maior drawdown** e **terceiro alvo subutilizado**.

### Configuração no NinjaTrader (template sugerido)

1. Criar ATM Strategy com **3 targets** e quantidades **1, 1, 1**.
2. Stop Loss inicial: **32 ticks** (stop de estratégia, não alterar PaperBarSim).
3. Target 1: **28 ticks** → ao fill, mover stop para **BE+2** nos contratos restantes.
4. Target 2: **45 ticks**.
5. Target 3: **60 ticks** + ativar **auto trail** (10 tick step, 1 tick frequency).
6. Associar ao hotkey do indicador `UltronV1` (painel Execution) — entradas manuais ou automação futura **sem mudar engine**.

### Alternativa conservadora

Se aversão a DD for prioridade sobre expectancy máxima: **ATM-8 Tight Risk (38/34/52/78)** — PF 1,75, MDD −961, perfil similar com SL ligeiramente mais folgado.

---

## 12. Limitações e próximos passos

| Limitação | Mitigação |
|-----------|-----------|
| Simulação por MFE/MAE, não tick-by-tick | Coletar `bar_path` futuro ou replay Sim101 com cada ATM |
| Outcomes todos com SL 30 / TP 32 | Não re-otimizar gates; apenas ATM |
| opportunities.jsonl parcialmente corrompido | Re-exportar dataset para cruzar ADX/ER por perfil |
| Modelo v3 otimista | Validar ATM-6 em **Sim101 30 dias** antes de live |

### Checklist de validação

- [ ] Criar template ATM-6 no NinjaTrader
- [ ] Replay 1 sessão com 3 ct Sim101
- [ ] Comparar PnL real vs projeção (+28.602 ticks / 868 trades no modelo v3)
- [ ] Medir slippage em T1 28 ticks
- [ ] Remover instrumentação `[SIM101]` após diagnóstico de execução

---

## 13. Apêndice — conversão ticks → USD (3 ct MNQ)

| Ticks | USD (3 ct × $0,50/tick) |
|-------|-------------------------|
| 28 | $42,00 |
| 45 | $67,50 |
| 60 | $90,00 |
| 32 (stop) | $48,00 risco máximo por trade |
| +28.602 (total projetado ATM-6 v3) | ~$42.903 no período analisado |

---

*Documento gerado por análise offline dos arquivos `outcomes.jsonl` (868 trades, 12 sessões) e logs de diagnóstico UltronV1Paper. Nenhum código de trading foi alterado.*
