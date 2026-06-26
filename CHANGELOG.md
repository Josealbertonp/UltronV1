# Changelog — Ultron V1

All notable changes to Ultron V1. RC1 follows **zero behavioral change** policy for trading logic unless explicitly noted.

Format: phases (functional history) + RC1 sprints (engineering history).

---

# 1.0.0-rc1

**Release Candidate 1** — stabilization, tests, docs, release engineering.  
**Documented version:** `1.0.0-rc1`  
**Assembly version in code:** `1.0.0-rc1` (`UltronVersions.UltronVersion`, S4-VER)  
**Branch reference:** `feature/rc1-sprint4` @ `4b0a865`

**Test baseline:** 80 C# + 18 Python = **98 tests**

---

## Phase 0 — Core skeleton

- `UltronV1.Core` — tick ingestion, `MnqTickProcessor`, contract validation (MNQ 09-26)
- `UltronV1` NT8 Indicator skeleton — diagnostics per bar
- xUnit test project foundation

---

## Phase 1 — Features, gates, score

- `FeatureEngine` — 40 features (ER, RVOL, archetypes, VWAP distance, …)
- `HardGateEvaluator` — ADX, RVOL, regime gates
- `ConfluenceScorer` / `SignalEvaluator` — tier A/B/C, long/short scores
- Signal arrows and HUD on Indicator

---

## Phase 2 — Order flow confirmation & audit

- `OrderFlowConfirmationEvaluator`
- Explain mode and score breakdown
- `UltronComparisonTracker` — optional Ultron vs NightRanger visual comparison
- Shadow pipeline flags in dataset records

---

## Phase 3 — Dataset Engine

- JSONL dataset format (`opportunities.jsonl`, `manifest.json`)
- `DatasetEngine`, `DatasetRecordBuilder`, `JsonLinesDatasetSink`
- `RecordAllBars` / accepted signals with full feature vector
- Dataset root: `Documents/NinjaTrader 8/UltronV1/dataset/`

---

## Phase 4 — ML platform

- `UltronV1.Training/` — LightGBM, temporal CV, walk-forward, ONNX export
- `MlAdvisor` / `OnnxInferenceEngine` in Core — **advisory only** (never vetoes signals)
- Model registry, manifests, SHAP summaries
- Training produces `trained_not_promoted` status by design

---

## Phase 4.5 — Validation Framework

- `UltronV1.Validation/` — comparator, parameter sweep, Monte Carlo, walk-forward report
- HTML validation report
- `validate.py` CLI — operates on exported datasets; **does not modify engine logic**

---

## Phase 5 — Outcome collection & paper execution

- `TradeCycleTracker` / **PaperBarSim** — authoritative simulated outcomes
- `UltronV1Paper` NT8 Strategy — Replay / Sim101 / Paper
- `DatasetEngine.EnrichOutcome` → `outcomes.jsonl`
- Default `SubmitSimOrders = false` — Sim101 visual-only

See [Fase5-OutcomeCollection.md](Fase5-OutcomeCollection.md).

---

## Sprint 1 — Data integrity (`rc1-sprint1`)

| ID | Deliverable |
|----|-------------|
| **C1** | `EvaluationSnapshot` — dataset records use honest direction/tier from `SignalEvaluator`; no score-inference on rejected signals |
| **C2** | Single feature schema source — `FeatureSchema.cs` → exported JSON; Python reads `schema/feature_names.json`; removed duplicate feature name lists |
| **C3** | `ScoreProfileHash` — reproducibility metadata in opportunities and manifests |
| **C6** | ONNX / dataset I/O observability — structured logging on inference and sink failures |

**Commit baseline:** `bf8f9db` · Tag: `rc1-sprint1`

---

## Sprint 2 — Shared infrastructure (`rc1-sprint2`)

| ID | Deliverable |
|----|-------------|
| **C4** | `NinjaTrader/Shared/` — `UltronVwapTracker`, `UltronIndicatorBundle`, `UltronNtLogger`, `UltronTickFeed`; Indicator + Paper delegate to Shared |
| **C5** | `UltronV1.Python/ultron_data/` — shared loader, JSONL, schema constants; Training + Validation import via editable install |
| **C7** | PaperBarSim declared authoritative; `SubmitSimOrders` default false; Sim101 visual-only documentation |

**Commit:** `b093511` · Tag: `rc1-sprint2`

---

## Sprint 3 — Tests & quality (`rc1-sprint3`)

| ID | Deliverable |
|----|-------------|
| **C8** | 18 pytest tests — loader, schema, metrics, comparator, Validation pipeline in-memory |
| **I6** | 19 new C# tests — FeatureEngine (I6-A), Outcome pipeline integration (I6-B), Dataset edge cases (I6-C) |
| — | `scripts/test_python.ps1`, `requirements-dev.txt`, golden JSONL fixtures |

**Commit:** `373cead` · Tag: `rc1-sprint3`  
**Total tests:** 98

---

## Sprint 4 — RC1 release engineering (complete)

| ID | Deliverable | Status @ `4b0a865` |
|----|-------------|-------------------|
| **S4-DC** | Dead code removal — `MarketContext`, `FeatureVector.Clone()`, `CurrentOrderFlow`, dead `_vwapResetToday` in Paper | ✅ `72c7604` |
| **S4-BUILD** | `scripts/build.ps1`, `scripts/test_all.ps1` — export → build → test → schema drift gate | ✅ `ca4aade` |
| **S4-NS** | RC1 documentation — ARCHITECTURE, DEPLOY, CHANGELOG, RC1_CHECKLIST, RC1_KNOWN_LIMITATIONS | ✅ `ffb8643` |
| **S4-DEPLOY** | `deploy_nt8.ps1` (8 files incl. `UltronComparisonTracker.cs`) | ✅ `da89594`, manifest fix `4b0a865` |
| **S4-VER** | Version bump `1.0.0-rc1`, RELEASE_NOTES, VERSION.md | ✅ `40f5c81` |

---

## RC1 summary — no trading behavior changes

Across Sprints 1–4 RC1 work:

- Score weights, gates, and signal logic **unchanged**
- Validation Framework algorithms **unchanged**
- Training algorithms **unchanged**
- PaperBarSim rules **unchanged**

Changes are limited to: metadata honesty (C1), schema unification (C2), observability (C6), DRY refactors (C4/C5), tests (C8/I6), dead code (S4-DC), scripts (S4-BUILD), documentation (S4-NS).

---

*Changelog aligned with branch `feature/rc1-sprint4` @ `4b0a865`.*
