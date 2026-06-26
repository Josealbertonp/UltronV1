#Requires -Version 5.1
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$PythonRoot = Join-Path $RepoRoot "UltronV1.Python"
$TrainingRoot = Join-Path $RepoRoot "UltronV1.Training"
$ValidationRoot = Join-Path $RepoRoot "UltronV1.Validation"

Write-Host "[Ultron] Installing Python packages for tests..."
python -m pip install -e $PythonRoot

Push-Location $TrainingRoot
python -m pip install -r requirements.txt
Pop-Location

Push-Location $ValidationRoot
python -m pip install -r requirements.txt
Pop-Location

python -m pip install -r (Join-Path $PythonRoot "requirements-dev.txt")

Write-Host "[Ultron] Running pytest..."
python -m pytest (Join-Path $PythonRoot "tests") -v
