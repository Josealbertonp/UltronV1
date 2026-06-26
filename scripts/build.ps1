#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$Solution = Join-Path $RepoRoot "UltronV1.sln"
$ExportProject = Join-Path $RepoRoot "tools\ExportFeatureSchema\ExportFeatureSchema.csproj"
$SchemaDir = Join-Path $RepoRoot "schema"

function Invoke-Step {
    param(
        [string]$Label,
        [scriptblock]$Action
    )

    Write-Host ""
    Write-Host "[Ultron] $Label"
    & $Action
    if ($LASTEXITCODE -ne 0) {
        Write-Error "[Ultron] Failed: $Label (exit code $LASTEXITCODE)"
        exit $LASTEXITCODE
    }
}

function Assert-CommandExists {
    param([string]$Name)

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        Write-Error "[Ultron] Required command not found: $Name"
        exit 1
    }
}

Assert-CommandExists "dotnet"
Assert-CommandExists "python"
Assert-CommandExists "git"

Write-Host "[Ultron] RC1 build pipeline starting..."
Write-Host "[Ultron] Repo root: $RepoRoot"

Invoke-Step "Exporting feature schema from FeatureSchema.cs..." {
    dotnet run --project $ExportProject -c Release -- $RepoRoot
}

Invoke-Step "Building solution (Release)..." {
    dotnet build $Solution -c Release
}

Invoke-Step "Running C# tests (dotnet test -c Release)..." {
    dotnet test (Join-Path $RepoRoot "UltronV1.Tests\UltronV1.Tests.csproj") -c Release --no-build
}

Invoke-Step "Running Python tests (pytest)..." {
    & (Join-Path $PSScriptRoot "test_python.ps1")
}

Write-Host ""
Write-Host "[Ultron] Validating schema/ is committed (git diff --exit-code schema/)..."
Push-Location $RepoRoot
try {
    git diff --exit-code -- schema/
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "[Ultron] ERROR: Feature schema export regenerated files under schema/." -ForegroundColor Red
        Write-Host "[Ultron] The pipeline updated JSON artifacts from FeatureSchema.cs, but they are not committed."
        Write-Host "[Ultron] Review the diff, then commit the regenerated schema files before re-running build.ps1:"
        Write-Host "  git diff schema/"
        Write-Host "  git add schema/"
        Write-Host '  git commit -m "chore(rc1): sync exported feature schema"'
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "[Ultron] RC1 build pipeline completed successfully."
