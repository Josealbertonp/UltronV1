# Register Ultron ML dependencies for NinjaTrader 8.
# Run with NinjaTrader CLOSED.
$ErrorActionPreference = 'Stop'

$custom = Join-Path $env:USERPROFILE 'Documents\NinjaTrader 8\bin\Custom'
$config = Join-Path $env:USERPROFILE 'Documents\NinjaTrader 8\Config.xml'
$csproj = Join-Path $custom 'NinjaTrader.Custom.csproj'
$ultronMlSrc = Join-Path (Split-Path $PSScriptRoot -Parent) 'UltronML\UltronML.Core\bin\Release\net48\UltronML.Core.dll'
$ultronMlDst = Join-Path $custom 'UltronML.Core.dll'

# Build net48 DLL if missing
if (-not (Test-Path $ultronMlSrc)) {
    $proj = Join-Path (Split-Path $PSScriptRoot -Parent) 'UltronML\UltronML.Core\UltronML.Core.csproj'
    dotnet build $proj -c Release -f net48 | Out-Host
}

Copy-Item $ultronMlSrc $ultronMlDst -Force -ErrorAction SilentlyContinue
if (-not (Test-Path $ultronMlDst) -or ((Get-Item $ultronMlSrc).LastWriteTimeUtc -gt (Get-Item $ultronMlDst).LastWriteTimeUtc)) {
    $staged = "$ultronMlDst.new"
    Copy-Item $ultronMlSrc $staged -Force
    Write-Host "NT has UltronML.Core.dll locked — staged as UltronML.Core.dll.new (replace after closing NT)"
} else {
    Write-Host 'UltronML.Core.dll (net48) -> Custom'
}

$requiredRefs = @(
    '*MyDocuments*\NinjaTrader 8\bin\Custom\UltronV1.Core.dll',
    '*MyDocuments*\NinjaTrader 8\bin\Custom\UltronML.Core.dll',
    '*MyDocuments*\NinjaTrader 8\bin\Custom\Microsoft.ML.OnnxRuntime.dll',
    '*MyDocuments*\NinjaTrader 8\bin\Custom\System.Memory.dll',
    '*MyDocuments*\NinjaTrader 8\bin\Custom\System.Buffers.dll'
)

[xml]$xml = Get-Content $config
$refs = @($xml.NinjaTrader.References.ArrayOfString.string)
foreach ($r in $requiredRefs) {
    if ($refs -notcontains $r) {
        $new = $xml.CreateElement('string')
        $new.InnerText = $r
        $null = $xml.NinjaTrader.References.ArrayOfString.AppendChild($new)
        Write-Host "Config.xml + $r"
    }
}
# Remove netstandard.dll reference if present (causes CS0433 in NT)
$netstd = '*MyDocuments*\NinjaTrader 8\bin\Custom\netstandard.dll'
$node = $xml.NinjaTrader.References.ArrayOfString.string | Where-Object { $_ -eq $netstd }
if ($node) { $null = $xml.NinjaTrader.References.ArrayOfString.RemoveChild($node); $xml.Save($config); Write-Host 'Removed netstandard.dll from Config.xml' }

$xml.Save($config)
if (Test-Path $csproj) {
    $lines = Get-Content $csproj
    $filtered = $lines | Where-Object {
        $_ -notmatch 'Reference Include="netstandard"' -and
        $_ -notmatch 'netstandard\.dll' -and
        $_ -notmatch 'Compile Include="obj\\' -and
        $_ -notmatch 'Compile Include="Ultron\\UltronML\\'
    }
    if ($filtered.Count -ne $lines.Count) {
        Set-Content -Path $csproj -Value $filtered -Encoding UTF8
        Write-Host 'Cleaned NinjaTrader.Custom.csproj (netstandard, obj\, Ultron\UltronML\)'
    }
}

Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $custom 'netstandard.dll')
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue (Join-Path $custom 'obj')
$inlineMl = Join-Path $custom 'Ultron\UltronML'
if (Test-Path $inlineMl) {
    Remove-Item -Recurse -Force $inlineMl
    Write-Host "Removed inline ML sources: $inlineMl (use UltronML.Core.dll only)"
}
Write-Host 'Removed netstandard.dll and obj\ if present'

Write-Host ''
Write-Host 'In NT8 Referencias (with NT closed, then open):'
Write-Host '  ADD:    UltronML.Core.dll'
Write-Host '  ADD:    Microsoft.ML.OnnxRuntime.dll'
Write-Host '  ADD:    System.Memory.dll'
Write-Host '  ADD:    System.Buffers.dll'
Write-Host '  REMOVE: netstandard.dll (if listed)'
Write-Host '  DO NOT copy Ultron\UltronML\*.cs into Custom — ML lives in UltronML.Core.dll only'
Write-Host 'Then F5.'
