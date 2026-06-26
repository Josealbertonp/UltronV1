# RC1 Checklist — Ultron V1

**Version (documented):** `1.0.0-rc1`  
**Branch reference:** `feature/rc1-sprint4` @ `ca4aade`

Related: [DEPLOY.md](DEPLOY.md) · [ARCHITECTURE.md](ARCHITECTURE.md) · [RC1_KNOWN_LIMITATIONS.md](RC1_KNOWN_LIMITATIONS.md) · [Ultron Validation Protocol v1.md](Ultron%20Validation%20Protocol%20v1.md)

Use this checklist before declaring RC1 ready and before starting **official Replay collection**.

---

## 1. Build & CI local

- [ ] .NET 8 SDK installed
- [ ] Python 3.10+ installed and on PATH
- [ ] Git repository clean or schema drift understood
- [ ] `.\scripts\build.ps1` exits **0**

### build.ps1 steps (automated)

- [ ] **ExportFeatureSchema** completed
- [ ] **dotnet build** `UltronV1.sln` `-c Release` succeeded
- [ ] **dotnet test** 80/80 passed
- [ ] **pytest** 18/18 passed
- [ ] **Schema without drift** — `git diff --exit-code schema/` passed

---

## 2. Tests (standalone)

- [ ] `.\scripts\test_all.ps1` exits **0**
- [ ] C# tests: **80/80**
- [ ] Python tests: **18/18**
- [ ] Combined: **98/98**

---

## 3. Schema integrity

- [ ] `schema/feature_names.json` matches `FeatureSchema.cs` (40 features)
- [ ] `dotnet test` — `FeatureSchemaConsistencyTests` pass
- [ ] Python `test_schema.py` pass
- [ ] No uncommitted changes under `schema/` after build

---

## 4. Deploy NinjaTrader 8 (manual)

- [ ] `UltronV1.Core.dll` copied to `Documents\NinjaTrader 8\bin\Custom\`
- [ ] `NinjaTrader/Shared/*.cs` copied to Custom (e.g. `Custom\Ultron\Shared\`)
- [ ] `UltronV1.cs` → `Custom\Indicators\`
- [ ] `UltronV1Paper.cs` → `Custom\Strategies\`
- [ ] NinjaScript **F5** compile succeeded
- [ ] `UltronV1.Core.dll` reference present in NinjaScript Editor

**Note:** `scripts/deploy_nt8.ps1` does not exist yet — deploy is manual ([DEPLOY.md](DEPLOY.md)).

---

## 5. Replay smoke test

- [ ] Chart: **MNQ 09-26**, 5-minute, **On each tick**
- [ ] Account: Sim101 or Paper (**not live**)
- [ ] **UltronV1Paper** strategy applied
- [ ] Replay session runs without exceptions
- [ ] Output Window shows Ultron logs (no contract error)
- [ ] `SubmitSimOrders` = **false** (default)

---

## 6. Dataset collection

- [ ] Session folder created under `Documents\NinjaTrader 8\UltronV1\dataset\sessions\{sessionId}\`
- [ ] `manifest.json` present with version fields
- [ ] `opportunities.jsonl` non-empty after Replay
- [ ] `outcomes.jsonl` present when signals accepted (Paper Strategy)
- [ ] Outcome rows have `executionMode: PaperBarSim`
- [ ] `signalId` joins between opportunities and outcomes

---

## 7. Validation (Python)

- [ ] `pip install -r UltronV1.Validation/requirements.txt` succeeded
- [ ] `pytest` Validation-related tests pass (via test_python.ps1)
- [ ] Optional: `python validate.py --synthetic --output ./reports` exits 0
- [ ] Optional: `validate.py` with real `--ultron-dataset` path after smoke Replay

---

## 8. Documentation (S4-NS)

- [ ] [ARCHITECTURE.md](ARCHITECTURE.md) present
- [ ] [DEPLOY.md](DEPLOY.md) present
- [ ] [CHANGELOG.md](CHANGELOG.md) present
- [ ] [RC1_CHECKLIST.md](RC1_CHECKLIST.md) present (this file)
- [ ] [RC1_KNOWN_LIMITATIONS.md](RC1_KNOWN_LIMITATIONS.md) present

---

## 9. Audit backlog (RC1 scope)

| ID | Item | Sprint | Done |
|----|------|--------|------|
| C1 | DatasetRecordBuilder / EvaluationSnapshot | S1 | ☑ |
| C2 | Single feature schema source | S1 | ☑ |
| C3 | ScoreProfileHash | S1 | ☑ |
| C4 | NT8 Shared infrastructure | S2 | ☑ |
| C5 | Python ultron_data package | S2 | ☑ |
| C6 | ONNX / I/O observability | S1 | ☑ |
| C7 | PaperBarSim authoritative path | S2 | ☑ |
| C8 | Python pytest suite | S3 | ☑ |
| I6 | C# FeatureEngine + integration tests | S3 | ☑ |
| S4-DC | Dead code cleanup | S4 | ☑ |
| S4-BUILD | build.ps1 / test_all.ps1 | S4 | ☑ |
| S4-NS | RC1 documentation | S4 | ☑ |
| S4-DEPLOY | deploy_nt8.ps1 | S4 | ☐ |
| S4-VER | Version tag v1.0.0-rc1 | S4 | ☐ |

---

## 10. Criteria to **start official collection**

Per [Ultron Validation Protocol v1.md](Ultron%20Validation%20Protocol%20v1.md):

- [ ] All sections 1–8 above completed (except optional validate.py on real data)
- [ ] Operator has read [RC1_KNOWN_LIMITATIONS.md](RC1_KNOWN_LIMITATIONS.md)
- [ ] Study protocol pre-registered (if required by your process)
- [ ] Replay environment configured (MNQ 09-26, PaperBarSim, dataset path)
- [ ] Collection targets understood (e.g. ≥150 linked trades, ≥40 session days)

---

## 11. Criteria to **release RC1**

- [ ] All section 1–10 checkboxes complete
- [ ] No open critical issues in trading/dataset pipeline
- [ ] `build.ps1` green on clean clone
- [ ] S4-VER complete: `UltronVersions` = `1.0.0-rc1`, Git tag `v1.0.0-rc1`
- [ ] Tag `rc1-sprint4` (optional milestone)
- [ ] Known limitations documented and accepted by release owner

---

*Checklist aligned with branch `feature/rc1-sprint4` @ `ca4aade`.*
