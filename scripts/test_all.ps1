#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$TestsProject = Join-Path $RepoRoot "UltronV1.Tests\UltronV1.Tests.csproj"

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

Invoke-Step "Running C# tests (dotnet test -c Release)..." {
    dotnet test $TestsProject -c Release
}

Invoke-Step "Running Python tests (pytest)..." {
    & (Join-Path $PSScriptRoot "test_python.ps1")
}

Write-Host ""
Write-Host "[Ultron] All test suites passed (C# + Python)."
