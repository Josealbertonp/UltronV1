# Ultron V1 — Deploy Guide (RC1)

**Version (documented):** `1.0.0-rc1`

Related: [ARCHITECTURE.md](ARCHITECTURE.md) · [RC1_CHECKLIST.md](RC1_CHECKLIST.md) · [Fase5-OutcomeCollection.md](Fase5-OutcomeCollection.md) · [Ultron Validation Protocol v1.md](Ultron%20Validation%20Protocol%20v1.md)

---

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 8.0+ | Build Core, Tests, ExportFeatureSchema |
| Python | 3.10+ | pytest, Training, Validation |
| Git | any | Schema drift check in build.ps1 |
| NinjaTrader 8 | installed | Indicator, Strategy, Replay |

**Note:** `scripts/deploy_nt8.ps1` automates NT8 deploy (see [NinjaTrader 8 — automated deploy](#ninjatrader-8--automated-deploy)). Manual copy steps remain documented below.

---

## Build (full RC1 pipeline)

From repository root:

```powershell
cd UltronV1
.\scripts\build.ps1
```

### Pipeline order (build.ps1)

1. **ExportFeatureSchema** — regenerates JSON from `FeatureSchema.cs`
2. **dotnet build** `UltronV1.sln` `-c Release`
3. **dotnet test** — 80 tests (`--no-build`)
4. **pytest** — 18 tests (via test_python.ps1)
5. **git diff --exit-code schema/** — fails if schema JSON changed but not committed

On schema drift, commit regenerated files:

```powershell
git diff schema/
git add schema/
git commit -m "chore(rc1): sync exported feature schema"
```

---

## Tests only (no full build)

```powershell
.\scripts\test_all.ps1
```

Runs:

1. `dotnet test UltronV1.Tests -c Release`
2. `.\scripts\test_python.ps1` (installs Python deps + pytest)

Use this for quick verification after code changes. For RC1 gate, prefer `build.ps1`.

---

## ExportFeatureSchema (standalone)

```powershell
dotnet run --project tools/ExportFeatureSchema/ExportFeatureSchema.csproj -c Release -- .
```

Optional score profile hash:

```powershell
dotnet run --project tools/ExportFeatureSchema/ExportFeatureSchema.csproj -c Release -- --score-profile-hash
```

Outputs:

- `UltronV1.Core/Features/ultron_mnq_feature_schema_v1.json`
- `schema/ultron_mnq_feature_schema_v1.json`
- `schema/feature_names.json`

---

## NinjaTrader 8 — automated deploy

Build Core first (`.\scripts\build.ps1` or `dotnet build UltronV1.Core -c Release`), then:

```powershell
# Preview (no copy)
.\scripts\deploy_nt8.ps1 -WhatIf -Force

# Deploy (close NinjaTrader first)
.\scripts\deploy_nt8.ps1 -Force
```

Copies **8** Ultron-owned files to `Documents\NinjaTrader 8\bin\Custom\`:

- `UltronV1.Core.dll`
- `Indicators\UltronV1.cs`
- `Indicators\UltronComparisonTracker.cs`
- `Strategies\UltronV1Paper.cs`
- `Ultron\Shared\` (4 `.cs` files)

After deploy: open NinjaScript Editor and press **F5** to compile.

Parameters: `-NinjaTraderPath`, `-DocumentsPath`, `-WhatIf`, `-Force`. Blocks if NinjaTrader is running (except `-WhatIf`).

---

## NinjaTrader 8 — manual deploy

Paths assume default NT8 user folder: `%USERPROFILE%\Documents\NinjaTrader 8\`

### 1. Build Core DLL

Either run `.\scripts\build.ps1` or:

```powershell
dotnet build UltronV1.Core -c Release
```

### 2. Copy UltronV1.Core.dll

**Source:**

```
UltronV1.Core\bin\Release\netstandard2.0\UltronV1.Core.dll
```

**Destination:**

```
Documents\NinjaTrader 8\bin\Custom\UltronV1.Core.dll
```

Replace the file when updating Core. Restart NinjaScript Editor if DLL was locked.

### 3. Install Shared scripts

Copy all files from repo `NinjaTrader/Shared/` to NT8 Custom (create folder if needed):

```
Documents\NinjaTrader 8\bin\Custom\Ultron\Shared\
├── UltronIndicatorBundle.cs
├── UltronNtLogger.cs
├── UltronTickFeed.cs
└── UltronVwapTracker.cs
```

Namespace: `NinjaTrader.NinjaScript.Ultron`. Required by both Indicator and Paper Strategy.

### 4. Install Indicator

**Source:** `NinjaTrader/UltronV1.cs`  
**Destination:** `Documents\NinjaTrader 8\bin\Custom\Indicators\UltronV1.cs`

**Required for Indicator compile:** `NinjaTrader/UltronComparisonTracker.cs` → `Documents\NinjaTrader 8\bin\Custom\Indicators\UltronComparisonTracker.cs` (included in `deploy_nt8.ps1`).

### 5. Install Strategy

**Source:** `NinjaTrader/UltronV1Paper.cs`  
**Destination:** `Documents\NinjaTrader 8\bin\Custom\Strategies\UltronV1Paper.cs`

### 6. Compile NinjaScript

1. Open NinjaScript Editor (New → NinjaScript Editor)
2. **References** → add `UltronV1.Core.dll` if not already referenced
3. Press **F5** (Compile)
4. Fix any path/namespace errors (usually missing Shared files or DLL)

---

## Indicator vs Strategy

| Host | File | Use case |
|------|------|----------|
| **UltronV1** (Indicator) | `UltronV1.cs` | HUD, arrows, explain, optional dataset, NR comparison |
| **UltronV1Paper** (Strategy) | `UltronV1Paper.cs` | Replay/Paper — **outcomes.jsonl** via PaperBarSim |

For Validation Protocol data collection, use **UltronV1Paper** with Dataset Engine enabled.

---

## Chart configuration

| Setting | Value |
|---------|--------|
| Instrument | **MNQ 09-26** (exact contract) |
| Timeframe | 5 minutes |
| Calculate | **On each tick** |
| Account | Sim101 or Paper — **never live capital for RC1** |

Wrong contract → Core disables engine (`MnqContractValidator`).

---

## Replay

1. Deploy NT8 files (above)
2. Open chart MNQ 09-26, 5m
3. Apply **UltronV1Paper** strategy
4. Connect **Sim101** (or paper account)
5. Tools → Historical Data → **Market Replay** (or Playback)
6. Ensure `Submit Sim101 Orders` = **false** (default) unless you want visual fills only

See [PaperBarSim vs Sim101](#paperbarsim-vs-sim101) below.

---

## Dataset collection

Enable dataset on the host:

- **Indicator:** `Enable Dataset Engine = true`
- **Paper Strategy:** dataset is always wired via `JsonLinesDatasetSink`

**Output path:**

```
Documents\NinjaTrader 8\UltronV1\dataset\sessions\{sessionId}\
├── manifest.json
├── opportunities.jsonl
└── outcomes.jsonl        ← Paper Strategy + PaperBarSim only
```

Each opportunity includes: `signalId`, 40 features, gates, orderFlow, score breakdown, versions, decision.

Gate for official collection: see [Ultron Validation Protocol v1.md](Ultron%20Validation%20Protocol%20v1.md) (e.g. ≥150 linked trades, ≥40 session days).

---

## Validation (Python)

### Install

```powershell
cd UltronV1.Validation
pip install -r requirements.txt
```

(`requirements.txt` installs `ultron_data` editable from `../UltronV1.Python`.)

### Synthetic smoke (no NT8 data)

```powershell
python validate.py --synthetic --output ./reports
```

### Real dataset (after Replay collection)

```powershell
python validate.py `
  --ultron-dataset "$env:USERPROFILE\Documents\NinjaTrader 8\UltronV1\dataset" `
  --output ./reports
```

See `UltronV1.Validation/README.md` for comparison CSV and NR log options.

### Automated tests (RC1)

```powershell
.\scripts\test_python.ps1
```

Includes in-memory Validation pipeline tests (no CLI subprocess).

---

## PaperBarSim vs Sim101

| Path | Source | Writes outcomes? |
|------|--------|------------------|
| **PaperBarSim** | `TradeCycleTracker` in Core | **Yes — authoritative** |
| **Sim101** | NT8 simulated account (`SubmitSimOrders = true`) | **No — visual only** |

Default: `SubmitSimOrders = false` on `UltronV1Paper`.

When Sim101 is enabled, strategy logs: `Sim101 visual only — outcome source PaperBarSim`.

**Never** use Sim101 fills for ML or Validation — they can diverge from PaperBarSim (slippage, intrabar fills).

Full rules: [Fase5-OutcomeCollection.md](Fase5-OutcomeCollection.md)

---

## Troubleshooting

| Symptom | Likely cause | Action |
|---------|--------------|--------|
| Engine disabled / red chart | Wrong instrument | Use MNQ 09-26 |
| NinjaScript compile error | Missing Shared or DLL | Copy Shared/*.cs + UltronV1.Core.dll |
| `python` not found | Python not on PATH | Install Python 3.10+; restart shell |
| build.ps1 fails on schema/ | FeatureSchema.cs changed | Commit regenerated `schema/*.json` |
| Empty outcomes.jsonl | Using Indicator only | Use UltronV1Paper Strategy |
| outcomes ≠ chart fills | Sim101 mistaken for source | Keep SubmitSimOrders=false; trust PaperBarSim |
| Duplicate outcomes same signalId | Known RC1 limitation | See [RC1_KNOWN_LIMITATIONS.md](RC1_KNOWN_LIMITATIONS.md) |
| pip `-e ../UltronV1.Python` fails | Wrong working directory | Run pip from Training/Validation dirs per requirements.txt |

---

*Deploy guide aligned with branch `feature/rc1-sprint4` @ `4b0a865`.*
