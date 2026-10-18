[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [ValidateSet('CurrentUser', 'AllUsers')] [string]$Scope = 'CurrentUser',
    [switch]$PurgeUserData
)

$ErrorActionPreference = 'Stop'
if (Get-Process acad -ErrorAction SilentlyContinue) { throw 'Close AutoCAD before uninstalling ATLAS CAD.' }
$base = if ($Scope -eq 'AllUsers') {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::CommonApplicationData)) 'Autodesk\ApplicationPlugins'
} else {
    Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)) 'Autodesk\ApplicationPlugins'
}
$base = [IO.Path]::GetFullPath($base)
$target = [IO.Path]::GetFullPath((Join-Path $base 'AtlasCad.bundle'))
$expected = $base.TrimEnd('\') + '\AtlasCad.bundle'
if (-not $target.Equals($expected, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected uninstall target.' }
if (Test-Path -LiteralPath $target) {
    if ($PSCmdlet.ShouldProcess($target, 'Remove ATLAS CAD application bundle')) {
        Remove-Item -LiteralPath $target -Recurse -Force
        Write-Host "Removed application bundle: $target"
    }
} else { Write-Host 'ATLAS CAD application bundle is not installed for this scope.' }

if ($PurgeUserData) {
    $dataRoot = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'AtlasCad'))
    $expectedData = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'AtlasCad'))
    if (-not $dataRoot.Equals($expectedData, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected user-data target.' }
    if ((Test-Path -LiteralPath $dataRoot) -and $PSCmdlet.ShouldProcess($dataRoot, 'Permanently remove ATLAS CAD license, device cache, catalog and diagnostics')) {
        Remove-Item -LiteralPath $dataRoot -Recurse -Force
        Write-Host 'Removed local ATLAS CAD user data. This operation is not recoverable from the workstation.'
    }
} else { Write-Host 'License/device state and cached catalog were preserved. Use -PurgeUserData to remove them explicitly.' }
