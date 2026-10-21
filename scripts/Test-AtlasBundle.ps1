[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)] [string]$BundlePath,
    [switch]$RequireCodeSigning
)

$ErrorActionPreference = 'Stop'
$bundle = (Resolve-Path -LiteralPath $BundlePath).Path
if (-not $bundle.EndsWith('.bundle', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Bundle directory must use the .bundle extension.'
}
$manifestPath = Join-Path $bundle 'PackageContents.xml'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'PackageContents.xml is missing.' }
[xml]$manifest = Get-Content -LiteralPath $manifestPath -Raw
$package = $manifest.ApplicationPackage
if ($package.AutodeskProduct -ne 'AutoCAD' -or $package.ProductType -ne 'Application') { throw 'Manifest product metadata is invalid.' }
$runtime = $package.Components.RuntimeRequirements
$supportedRanges = @('R24.1|R24.3', 'R25.0|R25.1', 'R25.1|R25.1')
$range = ([string]$runtime.SeriesMin) + '|' + ([string]$runtime.SeriesMax)
if ($runtime.OS -ne 'Win64' -or $range -notin $supportedRanges) {
    throw "Unsupported AutoCAD runtime range: $range"
}

$rootPrefix = $bundle.TrimEnd('\') + '\'
foreach ($entry in @($package.Components.ComponentEntry)) {
    $relative = [string]$entry.ModuleName
    if (-not $relative.StartsWith('./')) { throw "Unsafe module path in manifest: $relative" }
    $resolved = [IO.Path]::GetFullPath((Join-Path $bundle $relative.Substring(2)))
    if (-not $resolved.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "Manifest module is missing or escapes the bundle: $relative"
    }
}

$files = @(Get-ChildItem -LiteralPath $bundle -Recurse -File)
$forbidden = @($files | Where-Object {
    $_.Extension -in @('.mln', '.mlnx', '.pk8') -or
    $_.Name -like '*.atlascatalog.json' -or
    $_.Name -like 'STEELFLOW*.lsp' -or
    $_.Name -eq 'catalog-keys.json' -or
    $_.Name -in @('acmgd.dll', 'acdbmgd.dll', 'accoremgd.dll')
})
if ($forbidden.Count -gt 0) { throw ('Forbidden release files: ' + (($forbidden.Name | Sort-Object) -join ', ')) }

$netEntry = @($package.Components.ComponentEntry | Where-Object { $_.AppType -eq '.Net' })
if ($netEntry.Count -ne 1) { throw 'Manifest must contain exactly one .NET adapter.' }
$adapterRelative = [string]$netEntry[0].ModuleName
$adapter = [IO.Path]::GetFullPath((Join-Path $bundle $adapterRelative.Substring(2)))
$content = Split-Path -Parent $adapter
$catalog = Join-Path $content 'steelflow.atlascatalog'
if (-not (Test-Path (Join-Path $content 'STEELFLOW.fas'))) { throw 'Compiled STEELFLOW.fas is missing.' }
if (-not (Test-Path $adapter) -or -not (Test-Path $catalog)) { throw 'Adapter or encrypted catalog is missing.' }
$catalogText = [Text.Encoding]::GetEncoding(28591).GetString([IO.File]::ReadAllBytes($catalog))
if ($catalogText.Contains('W150X13') -or $catalogText.Contains('HP-200X53C')) { throw 'Encrypted catalog leaks representative profile names.' }

$hashPath = Join-Path $bundle 'SHA256SUMS.txt'
if (-not (Test-Path $hashPath)) { throw 'SHA256SUMS.txt is missing.' }
foreach ($line in Get-Content -LiteralPath $hashPath) {
    if ($line -notmatch '^([A-F0-9]{64})  (.+)$') { throw "Invalid hash line: $line" }
    $target = [IO.Path]::GetFullPath((Join-Path $bundle $Matches[2]))
    if (-not $target.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Hash path escapes bundle.' }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $Matches[1]) { throw "Hash mismatch: $($Matches[2])" }
}

if ($RequireCodeSigning) {
    $signature = Get-AuthenticodeSignature -LiteralPath $adapter
    if ($signature.Status -ne 'Valid') { throw "Adapter signature is not valid: $($signature.Status)" }
}
Write-Host "OK: bundle validated at $bundle"
