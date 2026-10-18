[CmdletBinding()]
param(
    [string]$BundlePath = (Join-Path $PSScriptRoot 'AtlasCad.bundle'),
    [ValidateSet('CurrentUser', 'AllUsers')] [string]$Scope = 'CurrentUser',
    [switch]$AllowDowngrade
)

$ErrorActionPreference = 'Stop'
if (Get-Process acad -ErrorAction SilentlyContinue) {
    throw 'O AutoCAD esta aberto. Salve seus desenhos, feche todas as janelas do AutoCAD e execute o instalador novamente.'
}
$source = (Resolve-Path -LiteralPath $BundlePath).Path
if (-not $source.EndsWith('AtlasCad.bundle', [StringComparison]::OrdinalIgnoreCase)) { throw 'Expected an AtlasCad.bundle source directory.' }
$validator = Join-Path $PSScriptRoot 'Test-AtlasBundle.ps1'
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) {
    $repositoryRoot = Split-Path -Parent $PSScriptRoot
    $validator = Join-Path $repositoryRoot 'scripts\Test-AtlasBundle.ps1'
}
if (-not (Test-Path -LiteralPath $validator -PathType Leaf)) { throw 'Bundle validator was not found.' }
& $validator -BundlePath $source

$base = if ($Scope -eq 'AllUsers') {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)) 'Autodesk\ApplicationPlugins'
} else {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)) 'Autodesk\ApplicationPlugins'
}
$base = [IO.Path]::GetFullPath($base)
New-Item -ItemType Directory -Force -Path $base | Out-Null
$target = [IO.Path]::GetFullPath((Join-Path $base 'AtlasCad.bundle'))
$prefix = $base.TrimEnd('\') + '\'
if (-not $target.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Install target escaped ApplicationPlugins.' }
[xml]$newManifest = Get-Content -LiteralPath (Join-Path $source 'PackageContents.xml') -Raw
$newVersion = [Version]$newManifest.ApplicationPackage.AppVersion
if (Test-Path -LiteralPath $target) {
    [xml]$oldManifest = Get-Content -LiteralPath (Join-Path $target 'PackageContents.xml') -Raw
    $oldVersion = [Version]$oldManifest.ApplicationPackage.AppVersion
    if ($newVersion -lt $oldVersion -and -not $AllowDowngrade) {
        throw "Refusing downgrade from $oldVersion to $newVersion. Use -AllowDowngrade explicitly."
    }
}

$staging = [IO.Path]::GetFullPath((Join-Path $base ('AtlasCad-installing-' + [Guid]::NewGuid().ToString('N') + '.bundle')))
$backup = [IO.Path]::GetFullPath((Join-Path $base ('AtlasCad-backup-' + [Guid]::NewGuid().ToString('N') + '.bundle')))
if (-not $staging.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or
    -not $backup.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Staging path escaped ApplicationPlugins.' }
try {
    Copy-Item -LiteralPath $source -Destination $staging -Recurse
    & $validator -BundlePath $staging
    if (Test-Path -LiteralPath $target) { Move-Item -LiteralPath $target -Destination $backup }
    Move-Item -LiteralPath $staging -Destination $target
    if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Recurse -Force }
} catch {
    if (-not (Test-Path -LiteralPath $target) -and (Test-Path -LiteralPath $backup)) {
        Move-Item -LiteralPath $backup -Destination $target
    }
    throw
} finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
Write-Host "ATLAS CAD $newVersion installed for $Scope at $target"
Write-Host 'Start AutoCAD and run ATLAS_DIAGNOSTICS.'
