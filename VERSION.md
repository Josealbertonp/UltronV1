# Ultron V1 — Version

| Field | Value |
|-------|--------|
| **Version** | `1.0.0-rc1` |
| **Release** | Release Candidate 1 |
| **Build status** | ✅ Pass (`scripts/build.ps1`) |
| **Test status** | ✅ 80 C# + 18 Python = **98/98** |
| **Release date** | _TBD — pending official RC1 tag_ |
| **Git branch** | `feature/rc1-sprint4` |
| **Commit (pre-S4-VER commit)** | `da89594` |
| **Commit final** | _(S4-VER commit on branch)_ |

## Assembly / package version

| Artifact | Version |
|----------|---------|
| `UltronV1.Core.csproj` `<Version>` | `1.0.0-rc1` |
| `UltronVersions.UltronVersion` | `1.0.0-rc1` |
| `UltronVersions.DefaultConfigVersion` | `1.0.0` (config schema; unchanged) |

Dataset records and manifests embed `ultronVersion: "1.0.0-rc1"` from `UltronVersions`.

## Validation snapshot (S4-VER audit)

| Check | Result |
|-------|--------|
| `dotnet build -c Release` | ✅ |
| ExportFeatureSchema | ✅ |
| `git diff --exit-code schema/` | ✅ |
| `dotnet test` | ✅ 80/80 |
| `pytest` | ✅ 18/18 |
| `scripts/build.ps1` | ✅ exit 0 |
| `scripts/test_all.ps1` | ✅ exit 0 |
| `scripts/deploy_nt8.ps1 -WhatIf` | ✅ 7 files |

See [RELEASE_NOTES.md](RELEASE_NOTES.md) · [RC1_CHECKLIST.md](RC1_CHECKLIST.md)
