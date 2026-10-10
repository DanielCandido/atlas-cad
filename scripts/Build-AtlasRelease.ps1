[CmdletBinding()]
param(
    [string]$Version = '1.0.0',
    [string]$AutodeskAutoCADRoot = 'F:\Softwares\AutoCad2026\AutoCAD 2026',
    [string]$CatalogPackagePath = '.tmp\m5-build\steel.atlascatalog',
    [string]$SteelFlowSourcePath = 'fixtures\private\m3\STEELFLOW.m3.lsp',
    [string]$OutputRoot = '.tmp\release',
    [string]$CertificateThumbprint,
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [switch]$RequireCodeSigning
)

$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Version must use numeric major.minor.patch format.' }
$repositoryRoot = Split-Path -Parent $PSScriptRoot
function FullPath([string]$value) {
    if ([IO.Path]::IsPathRooted($value)) { return [IO.Path]::GetFullPath($value) }
    return [IO.Path]::GetFullPath((Join-Path $repositoryRoot $value))
}
$autoCadRoot = FullPath $AutodeskAutoCADRoot
$catalog = FullPath $CatalogPackagePath
$steelFlowSource = FullPath $SteelFlowSourcePath
$output = FullPath $OutputRoot
$releaseRoot = Join-Path $output "AtlasCad-$Version-win64"
$bundle = Join-Path $releaseRoot 'AtlasCad.bundle'
$content = Join-Path $bundle 'Contents\Windows\2026'
$workspacePrefix = $repositoryRoot.TrimEnd('\') + '\'
if (-not $releaseRoot.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Release output must remain inside the repository workspace.' }
foreach ($required in @($catalog, $steelFlowSource, (Join-Path $autoCadRoot 'accoreconsole.exe'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required release input is missing: $required" }
}

$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot '.tmp\dotnet-cli-home'
$env:APPDATA = Join-Path $repositoryRoot '.tmp\appdata'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$project = Join-Path $repositoryRoot 'cad\autocad-2025-2026\Atlas.AutoCAD.Adapter.csproj'
$nugetConfig = Join-Path $repositoryRoot '.tmp\NuGet.Config'
& dotnet build $project -c Release "-p:Version=$Version" "-p:AutodeskAutoCADRoot=$autoCadRoot" --configfile $nugetConfig
if ($LASTEXITCODE -ne 0) { throw 'AutoCAD adapter release build failed.' }

if (Test-Path -LiteralPath $releaseRoot) { Remove-Item -LiteralPath $releaseRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $content | Out-Null
$compileRoot = Join-Path $repositoryRoot '.tmp\m6-compile'
New-Item -ItemType Directory -Force -Path $compileRoot | Out-Null
$compiledFas = Join-Path $compileRoot 'STEELFLOW.fas'
if (Test-Path -LiteralPath $compiledFas) { Remove-Item -LiteralPath $compiledFas -Force }
$scriptPath = Join-Path $compileRoot 'compile-steelflow.scr'
$sourceLisp = $steelFlowSource.Replace('\', '/')
$outputFas = $compiledFas.Replace('\', '/')
$scriptLines = @(
    '(vl-load-com)',
    "(setq atlasCompileResult (vlisp-compile 'st `"$sourceLisp`" `"$outputFas`"))",
    '(princ (strcat "\nATLAS_COMPILE_RESULT=" (vl-princ-to-string atlasCompileResult)))',
    '(quit)'
)
[IO.File]::WriteAllLines($scriptPath, $scriptLines, (New-Object Text.UTF8Encoding($false)))
& (Join-Path $autoCadRoot 'accoreconsole.exe') /s $scriptPath /l en-US | Out-Null
if (-not (Test-Path -LiteralPath $compiledFas) -or (Get-Item $compiledFas).Length -le 0) { throw 'STEELFLOW FAS compilation failed.' }

$adapterOutput = Join-Path $repositoryRoot 'cad\autocad-2025-2026\bin\Release\net10.0-windows'
Copy-Item -LiteralPath (Join-Path $adapterOutput 'Atlas.AutoCAD.Adapter.2026.dll') -Destination $content
Copy-Item -LiteralPath (Join-Path $adapterOutput 'Atlas.Cad.Core.dll') -Destination $content
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'cad\legacy-lisp\atlas-bridge.lsp') -Destination $content
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'installer\windows\atlas-loader.lsp') -Destination $content
Copy-Item -LiteralPath $compiledFas -Destination $content
Copy-Item -LiteralPath $catalog -Destination (Join-Path $content 'steel.atlascatalog')

$versionBytes = [Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes("AtlasCad:$Version"))
$guidBytes = New-Object byte[] 16
[Array]::Copy($versionBytes, $guidBytes, 16)
$productCode = '{' + (New-Object Guid (,$guidBytes)).ToString().ToUpperInvariant() + '}'
$template = Get-Content -LiteralPath (Join-Path $repositoryRoot 'installer\windows\templates\PackageContents.xml') -Raw
$manifest = $template.Replace('__VERSION__', $Version).Replace('__PRODUCT_CODE__', $productCode)
[IO.File]::WriteAllText((Join-Path $bundle 'PackageContents.xml'), $manifest, (New-Object Text.UTF8Encoding($false)))

$adapter = Join-Path $content 'Atlas.AutoCAD.Adapter.2026.dll'
if ($CertificateThumbprint) {
    $signTool = (Get-Command signtool.exe -ErrorAction SilentlyContinue).Source
    if (-not $signTool) { throw 'signtool.exe was not found in PATH.' }
    & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $adapter
    if ($LASTEXITCODE -ne 0) { throw 'Authenticode signing failed.' }
    & $signTool verify /pa $adapter
    if ($LASTEXITCODE -ne 0) { throw 'Authenticode verification failed.' }
} elseif ($RequireCodeSigning) { throw 'RequireCodeSigning was set but no CertificateThumbprint was supplied.' }

$hashLines = Get-ChildItem -LiteralPath $bundle -Recurse -File |
    Where-Object { $_.Name -ne 'SHA256SUMS.txt' } |
    Sort-Object FullName |
    ForEach-Object {
        $relative = $_.FullName.Substring($bundle.Length + 1).Replace('\', '/')
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $relative
    }
[IO.File]::WriteAllLines((Join-Path $bundle 'SHA256SUMS.txt'), $hashLines, (New-Object Text.UTF8Encoding($false)))
& (Join-Path $repositoryRoot 'scripts\Test-AtlasBundle.ps1') -BundlePath $bundle -RequireCodeSigning:$RequireCodeSigning
if ($LASTEXITCODE -ne 0) { throw 'Bundle validation failed.' }

Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Install-AtlasCad.ps1') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Uninstall-AtlasCad.ps1') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Test-AtlasBundle.ps1') -Destination $releaseRoot
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'installer\windows\Instalar-AtlasCad.cmd') -Destination $releaseRoot

$archive = Join-Path $output "AtlasCad-$Version-win64.zip"
if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
Compress-Archive -Path (Get-ChildItem -LiteralPath $releaseRoot).FullName -DestinationPath $archive -CompressionLevel Optimal
Write-Host "Release bundle: $bundle"
Write-Host "Release archive: $archive"
Write-Host ($(if ($CertificateThumbprint) { 'Authenticode: signed' } else { 'Authenticode: unsigned development build' }))
