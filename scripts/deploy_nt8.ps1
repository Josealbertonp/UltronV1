#Requires -Version 5.1
<#
.SYNOPSIS
    Deploy Ultron V1 artifacts to NinjaTrader 8 Custom folder.

.DESCRIPTION
    Copies Ultron-owned files only (Core DLL, Indicator, Strategy, Shared).
    Never deletes user files. Overwrites only known Ultron paths.

.PARAMETER NinjaTraderPath
    NinjaTrader 8 installation directory. Default: C:\Program Files\NinjaTrader 8

.PARAMETER DocumentsPath
    NinjaTrader user documents folder. Default: $env:USERPROFILE\Documents\NinjaTrader 8

.PARAMETER WhatIf
    List actions without copying files.

.PARAMETER Force
    Skip confirmation prompt before copying.
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$NinjaTraderPath = "C:\Program Files\NinjaTrader 8",
    [string]$DocumentsPath = "",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$Stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

function Write-UltronHost {
    param([string]$Message, [ConsoleColor]$Color = [ConsoleColor]::Gray)
    Write-Host $Message -ForegroundColor $Color
}

function Resolve-NinjaTraderDocumentsPath {
    param([string]$Override)

    if ($Override) {
        return (Resolve-Path -LiteralPath $Override -ErrorAction Stop).Path
    }

    $defaultPath = Join-Path $env:USERPROFILE "Documents\NinjaTrader 8"
    if (-not (Test-Path -LiteralPath $defaultPath)) {
        throw "NinjaTrader documents folder not found: $defaultPath. Use -DocumentsPath to specify the correct location."
    }

    return (Resolve-Path -LiteralPath $defaultPath).Path
}

function Test-NinjaTraderRunning {
    $processNames = @("NinjaTrader", "NinjaTrader64")
    foreach ($name in $processNames) {
        if (Get-Process -Name $name -ErrorAction SilentlyContinue) {
            return $true
        }
    }
    return $false
}

function Register-Nt8CompileReference {
    param(
        [string]$DocumentsRoot,
        [string]$CustomRoot
    )

    if ($WhatIfPreference) {
        Write-UltronHost "[Ultron] WhatIf: would register UltronV1.Core.dll in Config.xml and csproj." "DarkYellow"
        return
    }

    $configPath = Join-Path $DocumentsRoot "Config.xml"
    $refToken = Join-Path $CustomRoot "UltronV1.Core.dll"
    $csprojPath = Join-Path $CustomRoot "NinjaTrader.Custom.csproj"
    $dllPath = Join-Path $CustomRoot "UltronV1.Core.dll"

    if (Test-Path -LiteralPath $configPath) {
        $configText = Get-Content -LiteralPath $configPath -Raw
        if ($configText -notmatch 'UltronV1\.Core\.dll') {
            $insert = "      <string>$refToken</string>"
            $anchor = '      <string>*MyDocuments*\NinjaTrader 8\bin\Custom\CABABOM.dll</string>'
            if ($configText.Contains($anchor)) {
                $configText = $configText.Replace($anchor, "$anchor`r`n$insert")
                Set-Content -LiteralPath $configPath -Value $configText -Encoding UTF8 -NoNewline
                Write-UltronHost "[Ultron] Registered UltronV1.Core.dll in Config.xml (NinjaScript compile references)." "Green"
            }
            else {
                Write-UltronHost "[Ultron] WARNING: CABABOM anchor missing in Config.xml; add UltronV1.Core.dll via NinjaScript References." "Yellow"
            }
        }
    }

    if (Test-Path -LiteralPath $csprojPath) {
        $csprojText = Get-Content -LiteralPath $csprojPath -Raw
        if ($csprojText -notmatch 'UltronV1\.Core') {
            $refBlock = @"
    <Reference Include="UltronV1.Core">
      <HintPath>$dllPath</HintPath>
    </Reference>
"@
            $anchor = '    <Reference Include="NinjaTrader.Vendor">'
            if ($csprojText.Contains($anchor)) {
                $csprojText = $csprojText.Replace($anchor, "$refBlock$anchor")
                Set-Content -LiteralPath $csprojPath -Value $csprojText -Encoding UTF8 -NoNewline
                Write-UltronHost "[Ultron] Registered UltronV1.Core in NinjaTrader.Custom.csproj." "Green"
            }
        }
    }
}

function Confirm-Deploy {
    param(
        [int]$FileCount,
        [string]$CustomRoot
    )

    if ($Force -or $WhatIfPreference) {
        return $true
    }

    Write-Host ""
    Write-UltronHost "About to deploy $FileCount Ultron file(s) to:" "Yellow"
    Write-UltronHost "  $CustomRoot"
    $answer = Read-Host "Continue? (Y/N)"
    return ($answer -match '^[Yy]$')
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
$CoreDllSource = Join-Path $RepoRoot "UltronV1.Core\bin\Release\net48\UltronV1.Core.dll"
$NinjaTraderSource = Join-Path $RepoRoot "NinjaTrader"

Write-UltronHost "[Ultron] NT8 deploy starting..." "Cyan"
Write-UltronHost "[Ultron] Repo root: $RepoRoot"

if (-not (Test-Path -LiteralPath $NinjaTraderPath)) {
    Write-UltronHost "[Ultron] ERROR: NinjaTrader installation not found: $NinjaTraderPath" "Red"
    Write-UltronHost "[Ultron] Install NinjaTrader 8 or pass -NinjaTraderPath." "Red"
    exit 1
}

Write-UltronHost "[Ultron] NinjaTrader installation: $NinjaTraderPath"

$resolvedDocuments = Resolve-NinjaTraderDocumentsPath -Override $DocumentsPath
$CustomRoot = Join-Path $resolvedDocuments "bin\Custom"

Write-UltronHost "[Ultron] Documents path: $resolvedDocuments"
Write-UltronHost "[Ultron] Custom folder:  $CustomRoot"

if (-not (Test-Path -LiteralPath $CoreDllSource)) {
    Write-UltronHost "[Ultron] ERROR: UltronV1.Core.dll not found." "Red"
    Write-UltronHost "[Ultron] Expected: $CoreDllSource" "Red"
    Write-UltronHost "[Ultron] Run: dotnet build UltronV1.Core -c Release -f net48" "Red"
    exit 1
}

if (-not $WhatIfPreference -and (Test-NinjaTraderRunning)) {
    Write-UltronHost "" 
    Write-UltronHost "[Ultron] ERROR: NinjaTrader appears to be running." "Red"
    Write-UltronHost "[Ultron] Close NinjaTrader 8 completely before deploying (avoids DLL lock / compile conflicts)." "Red"
    Write-UltronHost "[Ultron] Deploy cancelled. No files were copied." "Red"
    exit 1
}

# Ultron-owned deploy manifest: source relative to repo -> destination relative to bin\Custom
$DeployMap = @(
    @{ Source = $CoreDllSource; Dest = Join-Path $CustomRoot "UltronV1.Core.dll" }
    @{ Source = Join-Path $NinjaTraderSource "UltronV1.cs"; Dest = Join-Path $CustomRoot "Indicators\UltronV1.cs" }
    @{ Source = Join-Path $NinjaTraderSource "UltronComparisonTracker.cs"; Dest = Join-Path $CustomRoot "Indicators\UltronComparisonTracker.cs" }
    @{ Source = Join-Path $NinjaTraderSource "UltronV1Paper.cs"; Dest = Join-Path $CustomRoot "Strategies\UltronV1Paper.cs" }
)

$sharedSourceDir = Join-Path $NinjaTraderSource "Shared"
if (-not (Test-Path -LiteralPath $sharedSourceDir)) {
    throw "Shared source folder not found: $sharedSourceDir"
}

Get-ChildItem -Path $sharedSourceDir -Filter "*.cs" -File | ForEach-Object {
    $DeployMap += @{
        Source = $_.FullName
        Dest   = Join-Path $CustomRoot (Join-Path "Ultron\Shared" $_.Name)
    }
}

foreach ($entry in $DeployMap) {
    if (-not (Test-Path -LiteralPath $entry.Source)) {
        Write-UltronHost "[Ultron] ERROR: Missing source file: $($entry.Source)" "Red"
        exit 1
    }
}

if (-not (Confirm-Deploy -FileCount $DeployMap.Count -CustomRoot $CustomRoot)) {
    Write-UltronHost "[Ultron] Deploy cancelled by user." "Yellow"
    exit 0
}

$Copied = @()

foreach ($entry in $DeployMap) {
    $destDir = Split-Path -Parent $entry.Dest
    $label = "$(Split-Path -Leaf $entry.Source) -> $($entry.Dest)"

    if ($PSCmdlet.ShouldProcess($label, "Copy Ultron file")) {
        if (-not (Test-Path -LiteralPath $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        Copy-Item -LiteralPath $entry.Source -Destination $entry.Dest -Force
        $Copied += [PSCustomObject]@{
            Source = $entry.Source
            Dest   = $entry.Dest
        }
        Write-UltronHost "[Ultron] Copied: $label" "Green"
    }
    else {
        Write-UltronHost "[Ultron] WhatIf: would copy $label" "DarkYellow"
        $Copied += [PSCustomObject]@{
            Source = $entry.Source
            Dest   = $entry.Dest
        }
    }
}

Register-Nt8CompileReference -DocumentsRoot $resolvedDocuments -CustomRoot $CustomRoot

$Stopwatch.Stop()

Write-Host ""
Write-UltronHost "========== Deploy Summary ==========" "Cyan"
Write-UltronHost "Status:  $(if ($WhatIfPreference) { 'WHATIF (no files copied)' } else { 'SUCCESS' })"
Write-UltronHost "Files:   $($Copied.Count)"
Write-UltronHost "Dest:    $CustomRoot"
Write-UltronHost "Elapsed: $($Stopwatch.Elapsed.ToString('mm\:ss\.fff'))"
Write-Host ""
Write-UltronHost "Files:" "Cyan"
foreach ($item in $Copied) {
    Write-UltronHost "  $(Split-Path -Leaf $item.Source) -> $($item.Dest)"
}

if (-not $WhatIfPreference) {
    Write-Host ""
    Write-UltronHost "Next: Open NinjaScript Editor and press F5 to compile." "Yellow"
}
