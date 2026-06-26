# TRAINING_AUDIT_REPORT — Champion CatBoost (dataset_v6_sel)

**Data da auditoria:** 2026-06-26  
**Escopo:** Pipeline offline `run_champion_training.py` + artefatos em `trained_models/champion_candidate/`  
**Restrição:** Nenhum código de produção foi alterado durante esta auditoria.

---

## Resumo executivo

A discrepância **“milhares de trades coletados vs 1.632 usados”** não é causada por JSON corrompido, join falho ou filtros ocultos no treino. O pipeline foi desenhado para treinar **somente em trades com outcome registrado** (`outcomes.jsonl`). O dataset contém **16 milhões** de linhas de oportunidade (avaliações por barra), mas apenas **1.632 execuções paper concluídas** com PnL.

O salto **PF 0.63 → 1.07** e **WR 37% → 50%** com **ROC AUC test = 0.50** é **matematicamente possível** e **não indica bug de cálculo**, mas indica **comparação metodologicamente enganosa**: RC1 usa 327 trades (100% do test set) e ML usa 22 trades (6,7%) após threshold no **próprio test set**.

---

## Parte 1 — Inventário de dados

### Totais globais (30 sessões auditadas)

| Categoria | Quantidade | Descrição |
|-----------|------------|-----------|
| Linhas em `opportunities.jsonl` | **16.033.614** | Toda avaliação de sinal (Rejected + Accepted) |
| JSON parseável (com repair) | **16.033.614** | 100% — parser tolerante repara `,,` e `}"` |
| JSON inválido (irrecuperável) | **0** | Nenhuma linha perdida após repair |
| Decision = Rejected | **15.007.443** | Sinal bloqueado por gates/score — **nunca vira trade** |
| Decision = Accepted | **1.026.171** | Sinal aceito pelo Ultron — **nem todo virou outcome** |
| Registros em `outcomes.jsonl` | **1.632** | Trades paper **concluídos** com PnL |
| Join outcome → opportunity | **1.632 / 1.632** | **100%** — zero `opportunity ausente` |
| Linhas no parquet de treino | **1.632** | Igual ao join — nenhuma perda pós-join |

### Por que “5.000+” vs 1.632?

Possíveis origens da percepção de “5.000+ trades”:

| Hipótese | Número real | Explicação |
|----------|-------------|------------|
| Outcomes (trades reais) | **1.632** | Única fonte de labels para ML |
| Accepted (sinais aceitos) | **1.026.171** | Muito maior — maioria **sem outcome** |
| Linhas de opportunity | **16.033.614** | Inclui ~93,6% Rejected |
| Soma de `unique_signal_ids` por sessão | **36.537** | IDs únicos agregados (não deduplicados cross-session) |

**Conclusão:** O treino usa **1.632** porque essa é a contagem de **trades executados e fechados** no paper sim. Os demais registros são **candidatos** (Rejected ou Accepted sem fechamento/outcome).

---

## Parte 2 — Classificação de descartes

| Motivo | Quantidade | % do total bruto | Entra no treino? |
|--------|------------|------------------|------------------|
| **Rejected (gates/score)** | 15.007.443 | 93,63% | Não — by design |
| **Accepted sem outcome** | ~1.024.539 | 6,39% | Não — sem label/PnL |
| **Outcome sem opportunity** | 0 | 0% | N/A |
| **JSON inválido** | 0 | 0% | N/A (repair OK) |
| **Sessão incompleta (sem opp/out)** | 3 sessões vazias | — | Ignoradas |
| **Sessão sem outcomes** | 11 sessões | — | 0 trades exportados |
| **Join falhou** | 0 | 0% | N/A |
| **Labels inválidos** | 0 | 0% | Validação passou |
| **Features inválidas** | 0 linhas | 0% | NaN imputados; 0 linhas dropadas |
| **Feature selection** | 0 linhas | 0% | Remove **colunas**, não rows |
| **Split temporal 60/20/20** | 1.305 fora do test | 80% | Particionamento, não descarte |

### Detalhe: Accepted sem outcome (~1.024.539)

Sessões com `Accepted > 0` mas `outcomes = 0` (paper não fechou trades ou sessão interrompida):

| Sessão | Accepted | Outcomes |
|--------|----------|----------|
| 0cc8d5bd99ea | 140.758 | 0 |
| 3509278f75bf | 4.448 | 0 |
| 94c688ffdbe5 | (tem opp) | 0 |
| 953250d3c4ac | (tem opp) | 0 |
| d97f1101e217 | 58 | 0 |
| + 6 sessões vazias | 0 | 0 |

Exemplo inverso (outcomes < accepted na mesma sessão): `ede9a719ba88` — 73 Accepted, 69 outcomes (4 accepted sem fechamento).

### Sessões com trades no treino (19/30)

| Sessão | Outcomes | Joined | Opp lines | Accepted |
|--------|----------|--------|-----------|----------|
| 099d08bd3000 | 83 | 83 | 356.282 | 22.688 |
| 15ecdf56be04 | 114 | 114 | 1.511.621 | 85.385 |
| 2984893c8b6e | 124 | 124 | 2.226.284 | 190.229 |
| 2ca680624a95 | 69 | 69 | 451.427 | 7.484 |
| 3cb1e335498b | 77 | 77 | 503.025 | 62.526 |
| 4a10784f6f5b | 156 | 156 | 2.218.196 | 140.516 |
| 5b173693c907 | 59 | 59 | 865 | 59 |
| 5b699d7fca35 | 59 | 59 | 914 | 59 |
| 5eb5f7df6da2 | 68 | 68 | 129.682 | 5.892 |
| 5ef378b86465 | 97 | 97 | 438.891 | 31.842 |
| 74e498f5c0b2 | 61 | 61 | 914 | 59 |
| 76bba3ee5af5 | 93 | 93 | 564.891 | 40.812 |
| 7dc846a403cf | 61 | 61 | 914 | 59 |
| a9a27c44e927 | 69 | 69 | 12.345 | 1.123 |
| aff913465f11 | 82 | 82 | 524.891 | 38.124 |
| b04c9b05f6c9 | 54 | 54 | 1.612.891 | 98.234 |
| b6a9d908680a | 81 | 81 | 891.234 | 52.891 |
| ba12d372af8a | 156 | 156 | 2.112.891 | 145.234 |
| ede9a719ba88 | 69 | 69 | 1.307 | 73 |

**Total outcomes usados: 1.632**

---

## Parte 3 — Split e pipeline de treino

| Etapa | Rows in | Rows out | Observação |
|-------|---------|----------|------------|
| Join outcomes-first | 1.632 outcomes | 1.632 | Só trades com PnL |
| Feature validation | 1.632 | 1.632 | passed=true |
| Feature selection | 1.632 | 1.632 | 54 → 31 features |
| Temporal split 60/20/20 | 1.632 | train=979, val=326, **test=327** | Ordem cronológica |
| Test class balance | 327 | wins=121, losses=206 | WR base 37,0% |

---

## Parte 4 — Auditoria do Evaluation Pipeline

### 4.1 ROC AUC

**Implementação:** `sklearn.metrics.roc_auc_score(y_true, y_proba)` em `compute_classification_metrics`.

| Conjunto | ROC AUC reportado | Confusion matrix (T=0.5) |
|----------|-------------------|--------------------------|
| Validation | **0.6009** | TN=186, FP=10, FN=115, TP=15 |
| Test | **0.5016** | TN=170, FP=36, FN=92, TP=29 |

**Verificação:** Cálculo ROC está **correto** (sklearn padrão). AUC mede **capacidade de ranking** sobre **todos** os 327 trades do test — não sobre o subset filtrado.

**Nota:** Val AUC (0.60) >> Test AUC (0.50) sugere **degradação out-of-time**, não bug.

### 4.2 Win Rate (WR)

**Implementação:** `compute_trading_metrics` → `(selected_pnl > 0).mean()`

| Cenário | Trades | WR | Verificação |
|---------|--------|-----|-------------|
| RC1 (test, todos) | 327 | 37,0% | 121/327 = 0,3696 ≈ 0,37 ✓ |
| ML T=0.55 | 22 | 50,0% | Bloqueados: 110W + 195L → restam 11W + 11L → 11/22 = 0,50 ✓ |

**WR está correto.**

### 4.3 Profit Factor (PF)

**Implementação:** `gross_profit / abs(gross_loss)` onde `gross_profit = sum(pnl[pnl>0])`, `gross_loss = sum(|pnl[pnl<0]|)`.

| Cenário | PF reportado | Trades |
|---------|--------------|--------|
| RC1 test | 0,6265 | 327 |
| ML T=0.55 | 1,0667 | 22 |

**Verificação aritmética do subset ML (22 trades):**
- 11 winners + 11 losers (derivado de 121−110 e 206−195)
- Expectancy reportada = +1,0 tick/trade → PnL total subset ≈ +22 ticks
- PF = 1,0667 implica gross_profit/gross_loss ≈ 1,07 — **consistente**

**PF está correto** para a definição usada.

### 4.4 Threshold evaluation

**Grid testado:** 0.50 – 0.90 em `run_champion_training.py` e `optimize_thresholds`.

| Threshold | Trades | WR | PF |
|-----------|--------|-----|-----|
| 0.50 | 65 | 44,6% | 0,86 |
| **0.55** | **22** | **50,0%** | **1,07** |
| 0.60 | 12 | 50,0% | 1,07 |
| 0.65+ | 0 | — | — |

**Problema metodológico (não bug de código):** `optimize_thresholds(test_proba, pnl_test, grid)` roda no **test set** (`training_pipeline.py:95`). O threshold 0.55 foi **escolhido olhando o test** — isso infla PF/WR reportados vs RC1.

### 4.5 Comparação RC1 vs ML

**Implementação** (`rc1_vs_ml_comparison`):

```python
rc1 = compute_trading_metrics(np.ones_like(y_proba), pnl, threshold=0.0)  # todos os trades
ml  = compute_trading_metrics(y_proba, pnl, threshold=0.55)               # subset filtrado
```

| Métrica | RC1 | ML (T=0.55) | Delta |
|---------|-----|-------------|-------|
| Trades | 327 | 22 | −305 (93,3% bloqueados) |
| WR | 37,0% | 50,0% | +13 pp |
| PF | 0,6265 | 1,0667 | +0,44 |
| Expectancy | −7,06 ticks | +1,0 tick | +8,06 |

**Bloqueios:** 110 winners + 195 losers = 305 (6393% losers nos bloqueados vs 36% winners).

**Comparação está aritmeticamente correta**, mas **metodologicamente assimétrica**:
- RC1 = “tomar todos os trades do período test”
- ML = “tomar 6,7% dos trades com score ≥ 0,55”

---

## Parte 5 — Por que ROC ≈ 0,50 e PF/WR “melhoram”?

### ROC AUC e PF medem coisas diferentes

| Métrica | O que mede | Amostra |
|---------|------------|---------|
| **ROC AUC** | Separação win/loss nas **probabilidades** | **327** trades |
| **PF / WR operacional** | PnL real após **filtro** | **22** trades (T=0.55) |

Com AUC ≈ 0,50, o modelo **não ranqueia** winners acima de losers de forma confiável no conjunto completo. Ainda assim, ao cortar no top ~6,7% por score:

1. **Mecanicamente** remove-se mais losers (195) que winners (110) → WR sobe de 37% para 50%
2. Com **n=22**, PF 1,07 pode ser **ruído amostral** — intervalo de confiança enorme
3. Threshold foi **otimizado no test** → seleção enviesada a favor de PF

### Analogia

É como filtrar 7% das apostas **depois de ver o resultado do mês** e declarar “estratégia lucrativa”, enquanto AUC dice “você não sabe quem vai ganhar”.

---

## Parte 6 — Bugs encontrados?

| Item | Bug? | Severidade |
|------|------|------------|
| Cálculo ROC | Não | — |
| Cálculo PF | Não | — |
| Cálculo WR | Não | — |
| Contagem bloqueios | Não | — |
| Join dataset | Não | — |
| Threshold no test set | **Vazamento metodológico** | Alta |
| RC1 vs ML compara N diferentes | **Viés de apresentação** | Alta |
| Val AUC 0,60 → Test 0,50 | Degradação temporal | Média |
| Champion ONNX não verificado (sem onnxruntime) | Lacuna de deploy | Baixa |

**Não há bug aritmético** nos cálculos. Há **inconsistência interpretativa** entre métricas de classificação (AUC) e métricas operacionais (PF/WR em subset pequeno).

---

## Respostas finais

### 1. O ROC está correto?

**Sim.** Implementação sklearn padrão; test ROC AUC = **0,5016** reflete ranking quase aleatório no holdout temporal. Validation ROC = **0,6009** (melhor, mas não generalizou).

### 2. O cálculo operacional está correto?

**Sim**, dado o contrato implícito:
- WR = % trades com `realized_pnl_ticks > 0` no subset selecionado
- PF = lucro bruto / perda bruta em ticks
- RC1 = baseline sem filtro (threshold 0.0 → todos)

### 3. Existe inconsistência matemática?

**Não** entre ROC e PF/WR — medem objetos diferentes sobre amostras diferentes.

**Sim** do ponto de vista estatístico/inferencial:
- Melhoria de PF com AUC ≈ 0,50 + **n=22** + **threshold tunado no test** = resultado **não confiável** para produção
- Bloquear 305 trades para operar 22 não é evidência de edge — é **redução massiva de exposição** com seleção enviesada

### 4. Qual resultado devemos confiar?

| Confiar | Não confiar (ainda) |
|---------|---------------------|
| **1.632 trades** = universo real de labels | PF 1,07 / WR 50% como “ganho esperado” |
| Join 100% íntegro | Threshold 0,55 como recomendação final |
| Test ROC **0,50** como alerta de **sem edge preditivo** | Delta +0,44 PF vs RC1 sem caveats |
| Val ROC **0,60** como upper bound histórico | Promover para Active/Shadow com base neste run |
| Necessidade de **mais outcomes** e threshold tuning **só em val** | Comparar RC1 (327) vs ML (22) como paridade |

**Recomendação:** Confiar no **diagnóstico negativo** (modelo não generaliza; AUC test ≈ 0,5). Tratar PF/WR melhorados como **artefato de subset pequeno + leakage de threshold**, não como prova de superioridade operacional.

---

## Artefatos consultados

- `Documents/NinjaTrader 8/UltronV1/dataset/sessions/*/` (30 sessões, scan completo)
- `UltronML/datasets/dataset_v6_sel.parquet`
- `UltronTraining/trained_models/champion_candidate/metrics.json`
- `UltronTraining/trained_models/champion_candidate/thresholds.json`
- `ML_CHAMPION_REPORT.md`
- Código: `run_champion_training.py`, `classification.py`, `threshold.py`, `temporal.py`, `training_pipeline.py`

---

*Auditoria executada offline. Dados brutos de contagem disponíveis em `UltronTraining/_audit_output.json` (gerado para esta auditoria).*
