# CURSOR_SESSION_HANDOFF — Ultron RC2 (Jun 2026)

> Handoff gerado antes de formatação do PC. Preserva contexto da sessão Cursor que será perdido após reinstalação.

**Repositório:** `https://github.com/Josealbertonp/UltronV1.git`  
**Workspace local:** `c:\Users\alber\Downloads\nightranger v4.2.12\UltronV1`  
**Transcript Cursor (referência):** agent-transcript `24406bc0-aa2d-4f01-8cbc-de1af2b7edd1`

---

## Restrições permanentes do projeto

**NÃO alterar lógica operacional:**

- UltronEngine, SignalEvaluator, FeatureEngine, TradeCycleTracker, PaperBarSim
- ATM, Gates, Score, Stops, Targets, Indicadores, Strategies

**Todo ML/treino/auditoria = pipeline offline apenas** (UltronML, UltronResearch, UltronTraining).

---

## Commits importantes nesta sessão

| Commit | Conteúdo |
|--------|----------|
| `25f8580` | RC2 ML infra, NT8 integration, champion v1 pipeline, JSONL repair |
| *(próximo)* | RC2 Phase 5: derived features, leak-free eval, audits, reports |

---

## O que foi feito (cronologia)

### 1. Fix UltronV1Paper (NinjaTrader)

- Strategy desmarcava "Habilitado" por exceção em `OnStateChange` ao carregar ONNX
- Correções em `NinjaTrader/UltronV1Paper.cs`: ML init resiliente, diagnóstico não derruba strategy
- Script deploy: `scripts/Register-NtUltronMlReferences.ps1`

### 2. Primeiro treino Champion (offline)

- Script: `scripts/run_champion_training.py`
- **1.632 trades** de **19 sessões** com outcomes (join 100%)
- **16M+ linhas** em `opportunities.jsonl` — maioria Rejected, não são trades
- JSON malformado nos opportunities (`",,"` e `}"`) → parser tolerante em `UltronML/ultronml/io/jsonl.py`
- Champion v1: **CatBoost**, ROC test **0.50**, threshold 0.55 (tunado no test — enviesado)
- Artefatos: `UltronTraining/trained_models/champion_candidate/`
- Relatório: `ML_CHAMPION_REPORT.md`

### 3. Auditoria de treino

- Relatório: `TRAINING_AUDIT_REPORT.md`
- Conclusão: sem bug aritmético; PF/WR “melhorados” eram artefato de subset pequeno + threshold no test
- Dados brutos contagem: `UltronTraining/_audit_output.json` (local, não commitado)

### 4. RC2 Fase 5 — Feature Engineering

- Script: `scripts/run_phase5_pipeline.py`
- **34 features derivadas** (`ultrontraining/data/derived_features.py`)
- Auditoria: `FEATURE_AUDIT_REPORT.md`
- Metodologia sem vazamento: `UltronTraining/docs/PHASE5_METHODOLOGY.md`
- Threshold escolhido **somente na validação** (`TrainingConfig.threshold_on_validation=True`)
- Champion v2: **LightGBM**, ROC test **0.507**, **não promovido**
- Artefatos: `UltronTraining/trained_models/champion_candidate_v2/`
- Relatório: `RC2_PHASE5_FEATURE_ENGINEERING_REPORT.md`

---

## Dados — O QUE BACKUPAR antes de formatar

### Crítico (~33 GB) — NÃO está no Git

```
C:\Users\alber\Documents\NinjaTrader 8\UltronV1\dataset\
  sessions\*\opportunities.jsonl
  sessions\*\outcomes.jsonl
```

- ~35 sessões (crescendo com testes de coleta)
- **1.632 outcomes** usados no treino; resto são oportunidades sem fechamento

### Importante (~2–5 MB) — gitignored

```
UltronTraining/trained_models/champion_candidate/
UltronTraining/trained_models/champion_candidate_v2/
```

### Regenerável (opcional backup)

```
UltronML/datasets/dataset_v6.parquet
UltronML/datasets/dataset_v7_phase5.parquet
```

---

## Comandos úteis pós-reinstalação

```powershell
# Clone
git clone https://github.com/Josealbertonp/UltronV1.git

# Python deps
pip install -e UltronML -e UltronTraining -e UltronResearch
pip install catboost xgboost lightgbm shap skl2onnx onnx pandas pyarrow scikit-learn

# Treino champion (full — ~6 min join JSONL)
python scripts/run_champion_training.py

# Treino champion (reuse parquet)
python scripts/run_champion_training.py --skip-dataset

# Phase 5 pipeline (~20 s)
python scripts/run_phase5_pipeline.py

# Deploy NT8
.\scripts\Register-NtUltronMlReferences.ps1
.\scripts\deploy_nt8.ps1
```

---

## Números-chave (dataset atual)

| Métrica | Valor |
|---------|-------|
| Sessões NT | ~35 |
| Outcomes (trades fechados) | 1.632 |
| Winners / Losers | 488 / 1.144 |
| Período | 2026-06-10 → 2026-06-22 |
| Linhas opportunities | ~16M |
| Accepted sem outcome | ~1M+ |

---

## Resultados ML (honestos, Phase 5)

| Modelo | ROC test | Shadow? |
|--------|----------|---------|
| Champion v1 (CatBoost) | 0.502 | Não |
| Champion v2 (LightGBM) | 0.507 | Não |

**Critérios Shadow (Phase 5):** ROC ≥ 0.60, Brier melhor, calibração OK, walk-forward estável, sem vazamento.

**Próximo passo natural:** continuar coleta paper → meta **>3.000 outcomes** antes de novo treino.

---

## Arquivos-chave por pasta

| Pasta | Papel |
|-------|-------|
| `NinjaTrader/` | Strategies/indicators NT8 |
| `UltronML/` | Dataset builder, JSONL, export parquet |
| `UltronTraining/` | Treino, calibração, SHAP, Phase 5 |
| `UltronResearch/` | Análise, shadow, filter research |
| `scripts/` | `run_champion_training.py`, `run_phase5_pipeline.py`, deploy |

---

## Relatórios gerados (ler primeiro após reinstalar)

1. `TRAINING_AUDIT_REPORT.md` — por que 1.632 vs milhões
2. `FEATURE_AUDIT_REPORT.md` — features úteis vs inúteis
3. `RC2_PHASE5_FEATURE_ENGINEERING_REPORT.md` — Fase 5 completa
4. `ML_CHAMPION_REPORT.md` — champion v1
5. `UltronTraining/docs/PHASE5_METHODOLOGY.md` — fluxo sem vazamento

---

## Problemas conhecidos / fixes aplicados

- **JSONL corrupto:** `parse_jsonl_line()` + `repair_json_line()` em `ultronml/io/jsonl.py`
- **Walk-forward classe única:** skip fold em `walk_forward.py`
- **SHAP dims:** fix em `shap_analysis.py`
- **Encoding Windows:** evitar `→` em prints; usar `PYTHONIOENCODING=utf-8`
- **ONNX LightGBM:** export via onnxmltools ou fallback em `onnx_export.py`
- **Threshold leakage:** corrigido Phase 5 — tunar só em validation

---

## Estado Git ao gerar este handoff

- Branch: `main`
- Remote: `origin` → GitHub Josealbertonp/UltronV1
- Pendente commit: Phase 5 + relatórios + este arquivo

---

*Gerado: 2026-06-26 — sessão Cursor Ultron RC2 ML*
