# RC1 Known Limitations — Ultron V1

**Version (documented):** `1.0.0-rc1`

This document lists what RC1 **does not** do or **does not guarantee**. Read before Replay collection, ML training, or Validation conclusions.

Related: [ARCHITECTURE.md](ARCHITECTURE.md) · [DEPLOY.md](DEPLOY.md) · [RC1_CHECKLIST.md](RC1_CHECKLIST.md) · [Ultron Validation Protocol v1.md](Ultron%20Validation%20Protocol%20v1.md)

---

## Trading & execution

### ML remains advisory only

- `MlAdvisor` in Core can boost/penalty score but **never vetoes** signals.
- The NT8 **Indicator and Paper Strategy do not wire ML** in RC1 — ONNX inference requires manual host integration.
- Training pipeline exists (`UltronV1.Training/`) but promotion to live advisory is out of RC1 scope.

### Replay is not Live

- Market Replay uses deterministic tick order and simplified fills.
- Slippage, queue position, partial fills, latency, and rejections are **not** modeled.
- Performance in Replay **does not** predict live performance.

### Sim101 is visual only

- `SubmitSimOrders = true` on `UltronV1Paper` draws orders on Sim101 for chart feedback.
- Sim101 fills **do not** write to `outcomes.jsonl` and **must not** be used for ML or Validation.
- Log when enabled: `Sim101 visual only — outcome source PaperBarSim`.

### PaperBarSim is the official outcome source

- All authoritative outcomes come from `TradeCycleTracker` in Core (`executionMode = PaperBarSim`).
- Rules: entry at bar close, no same-bar exit, stop-before-target on same bar, max-bars exit.
- See [Fase5-OutcomeCollection.md](Fase5-OutcomeCollection.md).

---

## Dataset & metadata

### OutcomeLinked is not persisted

- `OpportunityRecord.OutcomeLinked` exists and is serialized as `outcomeLinked` in `opportunities.jsonl`.
- `EnrichOutcome` **does not** set this field to `true` on the stored opportunity row.
- Python loaders (`ultron_data`) **do not read** `outcomeLinked` — join uses `signalId` + separate `outcomes.jsonl`.
- Fixing persistence is a **metadata improvement** for RC2, not a RC1 behavior change.

### Duplicate outcomes are allowed

- Test `DuplicateOutcome_CurrentRc1Behavior_IsNotIdempotent` documents RC1 behavior.
- Calling `EnrichOutcome` twice for the same `signalId` appends **two** outcome rows.
- Deduplication is **not** implemented in RC1.

### Pre-RC1 datasets may be incompatible

- Records before Sprint 1 (C1/C3) may lack correct rejection metadata or `scoreProfileHash`.
- Do not mix pre-RC1 and RC1 sessions in the same training run without filtering.

---

## Core & performance

### Footprint is accumulated but not used

- `MnqTickProcessor.UpdateFootprint` maintains `_footprintBuy` / `_footprintSell` arrays (~1600 slots).
- `BuildState()` **does not read** footprint data — features exported to dataset are unchanged.
- Disabling footprint is deferred to **RC2** (requires golden tick proof).

### Lazy BuildState deferred

- `BuildState()` runs on every tick today (I4 in backlog).
- CPU optimization deferred to RC2 — no RC1 behavior benefit.

### Performance tuning deferred

- Manifest flush batching (I8), allocation reductions (I3), and similar work are **post-RC1**.

---

## Architecture refactors deferred

### RuleRegistry deferred

- Extracting rules from `UltronConfig` / evaluators (I2, RF1) risks behavioral drift.
- **Not planned for RC1.**

### Split UltronConfig deferred

- Large config object remains monolithic (I1).
- **Not planned for RC1.**

---

## NinjaTrader & comparison

### UltronComparisonTracker limitations

- NR detection relies on chart draw objects (`NR_SIGNAL_{barIndex}`).
- Overlap with Ultron **Accepted** vs NR **drawn** signals is not decision-equivalent.
- Comparator may under/over-estimate overlap (C9 — doc only in RC1; **no tracker fix**).

### deploy_nt8.ps1

- Automated NT8 deploy: `scripts/deploy_nt8.ps1` copies **8** Ultron-owned files (DLL, Indicator, `UltronComparisonTracker.cs`, Strategy, 4 Shared).
- Manual steps remain in [DEPLOY.md](DEPLOY.md).

---

## Dependencies & tooling

### ONNX runtime in Core without active ML

- `UltronV1.Core` references ONNX even when ML is disabled.
- Failures are logged (C6) and inference falls back — but dependency remains.

### SimpleJsonReader for manifests

- Regex-based JSON reader sufficient for controlled manifest files only.
- Not a general-purpose JSON parser.

### Assembly version

- Code reports `UltronVersions.UltronVersion = "1.0.0-rc1"` since S4-VER (`40f5c81`).
- Git tag `v1.0.0-rc1` is optional and pending operator sign-off.

---

## RC2 roadmap (summary)

| Area | Planned direction |
|------|-------------------|
| **Version** | Git tag `v1.0.0-rc1` (optional, post sign-off) |
| **Dataset** | OutcomeLinked persistence; outcome deduplication |
| **Core perf** | Disable unused footprint; lazy BuildState |
| **ML** | Document + optional wire MlAdvisor in NT8 host |
| **Comparison** | C9 documentation expansion; tracker fix only if approved |
| **Architecture** | RuleRegistry / UltronConfig split — only with behavior proofs |
| **Validation** | Post-collection runs per Validation Protocol v1 |

---

## What RC1 **does** guarantee

- Frozen trading logic (score, gates, order flow algorithms)
- Reproducible build via `scripts/build.ps1`
- 98 automated tests (80 C# + 18 Python)
- Single feature schema pipeline (Core → JSON → Python)
- Honest dataset metadata (S1-C1) and score profile hash (S1-C3)
- PaperBarSim as sole outcome authority for ML/Validation datasets

---

*Limitations doc aligned with branch `feature/rc1-sprint4` @ `4b0a865`.*
