# Ultron V1 — Release Notes

## 1.0.0-rc1 (Release Candidate 1)

**Instrument:** MNQ 09-26 · **Primary bar:** 5 minutes · **Calculate:** On each tick  
**Branch:** `feature/rc1-sprint4` · **Baseline commit (S4-DEPLOY):** `da89594`

---

## Executive summary

Ultron V1 RC1 is a **stabilization release** for the MNQ order-flow and confluence engine. No new trading features were added. The RC1 program delivered:

- Honest, reproducible dataset metadata (Sprint 1)
- Shared infrastructure across NinjaTrader hosts and Python pipelines (Sprint 2)
- 98 automated tests and local CI scripts (Sprint 3)
- Dead-code cleanup, build/deploy automation, and operational documentation (Sprint 4)

**Trading logic, score weights, gates, order-flow algorithms, Validation Framework, and Training pipelines remain frozen.**

RC1 is ready for **Replay data collection** per [Ultron Validation Protocol v1.md](Ultron%20Validation%20Protocol%20v1.md), not for live capital deployment.

---

## Final architecture

```
NinjaTrader 8 (Indicator + Paper Strategy + Shared)
        │
        ▼
UltronV1.Core — UltronEngine, FeatureEngine, SignalEvaluator, DatasetEngine, PaperBarSim
        │
        ├── schema/ (exported from FeatureSchema.cs)
        ├── UltronV1.Python/ultron_data
        ├── UltronV1.Training (ML advisory)
        └── UltronV1.Validation (quantitative compare vs NightRanger)
```

Full detail: [ARCHITECTURE.md](ARCHITECTURE.md)

### Key architectural decisions (RC1)

| Decision | Rationale |
|----------|-----------|
| Core without NinjaTrader dependency | Unit-testable engine; single DLL for NT8 |
| `FeatureSchema.cs` as schema source of truth | Eliminates silent feature permutation in ML (C2) |
| `EvaluationSnapshot` for dataset records | Rejected signals record honest metadata (C1) |
| `ScoreProfileHash` in manifests | Reproducibility for Validation Protocol (C3) |
| `NinjaTrader/Shared/` extraction | DRY between Indicator and Paper (C4) |
| `ultron_data` Python package | DRY between Training and Validation (C5) |
| **PaperBarSim authoritative** | Deterministic outcomes for ML/Validation (C7) |
| ML advisory-only, not wired in NT8 RC1 | Frozen behavior; manual host integration |
| Validation in-memory in pytest | Deterministic CI without subprocess CLI |

---

## Functional phases (history)

| Phase | Deliverable |
|-------|-------------|
| **0** | Core tick processor, MNQ contract validation, NT8 indicator skeleton |
| **1** | FeatureEngine (40 features), Hard Gates, Confluence score, signal tiers |
| **2** | Order-flow confirmation, explain mode, NR comparison tracker |
| **3** | Dataset Engine — JSONL opportunities, manifest, shadow flags |
| **4** | ML Training platform — LightGBM, ONNX, MlAdvisor (advisory) |
| **4.5** | Validation Framework — comparator, sweep, Monte Carlo, HTML report |
| **5** | PaperBarSim outcomes, `UltronV1Paper` strategy, `outcomes.jsonl` |

---

## RC1 engineering sprints

### Sprint 1 — Data integrity (`rc1-sprint1` · `bf8f9db`)

| ID | Summary |
|----|---------|
| C1 | Honest dataset metadata via EvaluationSnapshot |
| C2 | Single feature schema — Core → JSON → Python |
| C3 | ScoreProfileHash for reproducibility |
| C6 | Structured logging for ONNX and dataset I/O failures |

### Sprint 2 — Shared infrastructure (`rc1-sprint2` · `b093511`)

| ID | Summary |
|----|---------|
| C4 | NT8 Shared — VWAP, indicators, tick feed, logger |
| C5 | `ultron_data` Python package |
| C7 | PaperBarSim authoritative; Sim101 visual-only default |

### Sprint 3 — Tests & quality (`rc1-sprint3` · `373cead`)

| ID | Summary |
|----|---------|
| C8 | 18 pytest tests (loader, schema, metrics, comparator, pipeline) |
| I6 | 19 new C# tests — FeatureEngine, outcome pipeline, dataset edge cases |

### Sprint 4 — Release engineering (`feature/rc1-sprint4`)

| ID | Commit | Summary |
|----|--------|---------|
| S4-DC | `72c7604` | Remove dead code (MarketContext, Clone, CurrentOrderFlow, dead field) |
| S4-BUILD | `ca4aade` | `build.ps1`, `test_all.ps1` |
| S4-NS | `ffb8643` | ARCHITECTURE, DEPLOY, CHANGELOG, RC1_CHECKLIST, RC1_KNOWN_LIMITATIONS |
| S4-DEPLOY | `da89594` | `deploy_nt8.ps1` |
| S4-VER | _(this commit)_ | Version `1.0.0-rc1`, RELEASE_NOTES, VERSION.md |

---

## Known limitations

See [RC1_KNOWN_LIMITATIONS.md](RC1_KNOWN_LIMITATIONS.md). Highlights:

- ML not wired in NT8 indicator/strategy
- Replay ≠ Live; Sim101 visual-only
- PaperBarSim is the sole outcome authority
- `OutcomeLinked` not persisted; duplicate outcomes allowed
- Footprint accumulated but unused (RC2)
- No automated rollback or installer

---

## RC2 roadmap (summary)

| Area | Direction |
|------|-----------|
| Git tag `v1.0.0-rc1` + optional GitHub release | After operator sign-off |
| `OutcomeLinked` persistence + outcome deduplication | Metadata only |
| Disable unused footprint; lazy BuildState | Performance |
| Wire ML in NT8 host (optional) | With Validation Protocol A/B |
| RuleRegistry / UltronConfig split | Behavior-proof required |
| Official Replay collection + Validation runs | Post-RC1 gate |

---

## Final statistics

| Metric | Value |
|--------|-------|
| C# tests | **80** |
| Python tests | **18** |
| **Total tests** | **98** |
| Sprint 4 commits (S4-DC → S4-DEPLOY) | **4** |
| Core features | **40** |
| RC1 sprint tags | `rc1-sprint1`, `rc1-sprint2`, `rc1-sprint3` |
| Production code behavior changes in RC1 sprints | **0** |

---

## Reproduce build

```powershell
cd UltronV1
.\scripts\build.ps1
```

Pipeline: ExportFeatureSchema → `dotnet build` Release → `dotnet test` → pytest → schema drift check.

Prerequisites: .NET 8 SDK, Python 3.10+, Git.

---

## Reproduce tests

```powershell
.\scripts\test_all.ps1
```

Or individually:

```powershell
dotnet test UltronV1.Tests\UltronV1.Tests.csproj -c Release
.\scripts\test_python.ps1
```

---

## Reproduce deploy (preview)

Build Core first, then:

```powershell
# Preview (no copy)
.\scripts\deploy_nt8.ps1 -WhatIf -Force

# Deploy (close NinjaTrader first)
.\scripts\deploy_nt8.ps1 -Force
```

Copies 7 Ultron-owned files to `Documents\NinjaTrader 8\bin\Custom\`.  
Detail: [DEPLOY.md](DEPLOY.md)

---

## Related documents

- [VERSION.md](VERSION.md)
- [ARCHITECTURE.md](ARCHITECTURE.md)
- [DEPLOY.md](DEPLOY.md)
- [CHANGELOG.md](CHANGELOG.md)
- [RC1_CHECKLIST.md](RC1_CHECKLIST.md)
- [RC1_KNOWN_LIMITATIONS.md](RC1_KNOWN_LIMITATIONS.md)
- [Fase5-OutcomeCollection.md](Fase5-OutcomeCollection.md)

---

*Release notes generated during S4-VER audit on branch `feature/rc1-sprint4`.*
