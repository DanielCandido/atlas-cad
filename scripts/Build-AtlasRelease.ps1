[CmdletBinding()]
param(
    [string]$Version = '1.0.0',
    [string]$AutodeskAutoCADRoot = 'F:\Softwares\AutoCad2026\AutoCAD 2026',
    [string]$CatalogPackagePath = '.tmp\m5-build\steel.atlascatalog',
    [string]$CatalogKeyRingPath = 'infra\secrets\catalog-keys.json',
    [string]$SteelFlowSourcePath = 'fixtures\private\m3\STEELFLOW.m3.lsp',
    [string]$OutputRoot = '.tmp\release',
    [string]$SigningPublicKeyPath = 'infra\secrets\server-signing-public.json',
    [Parameter(Mandatory=$true)] [string]$PublicLicenseApiUrl,
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
function Invoke-DotNet([string[]]$Arguments, [string]$FailureMessage) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) { throw $FailureMessage }
}

$autoCadRoot = FullPath $AutodeskAutoCADRoot
$catalog = FullPath $CatalogPackagePath
$catalogKeyRing = FullPath $CatalogKeyRingPath
$steelFlowSource = FullPath $SteelFlowSourcePath
$output = FullPath $OutputRoot
$signingPublicKey = FullPath $SigningPublicKeyPath
$runtimeConfiguration = Join-Path $repositoryRoot '.tmp\release-runtime-configuration.json'
if (-not $PublicLicenseApiUrl.StartsWith('https://', [StringComparison]::OrdinalIgnoreCase)) { throw 'PublicLicenseApiUrl must use HTTPS.' }
$runtimeJson = @{ licenseApiBaseUrl = $PublicLicenseApiUrl.TrimEnd('/') } | ConvertTo-Json -Compress
[IO.File]::WriteAllText($runtimeConfiguration, $runtimeJson, (New-Object Text.UTF8Encoding($false)))
$workspacePrefix = $repositoryRoot.TrimEnd('\') + '\'
if (-not $output.StartsWith($workspacePrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Release output must remain inside the repository workspace.' }
foreach ($required in @($catalog, $catalogKeyRing, $steelFlowSource, $signingPublicKey, (Join-Path $autoCadRoot 'accoreconsole.exe'))) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required release input is missing: $required" }
}
$catalogBytes = [IO.File]::ReadAllBytes($catalog)
if ($catalogBytes.Length -lt 17 -or [Text.Encoding]::ASCII.GetString($catalogBytes, 0, 8) -ne 'ATLCAT01') {
    throw 'Protected catalog envelope is invalid.'
}
$headerLength = ([int]$catalogBytes[8] -shl 24) -bor ([int]$catalogBytes[9] -shl 16) -bor ([int]$catalogBytes[10] -shl 8) -bor [int]$catalogBytes[11]
if ($headerLength -le 0 -or 16 + $headerLength -gt $catalogBytes.Length) { throw 'Protected catalog header is invalid.' }
$catalogHeader = [Text.Encoding]::UTF8.GetString($catalogBytes, 16, $headerLength) | ConvertFrom-Json
$keyRing = Get-Content -LiteralPath $catalogKeyRing -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($catalogHeader.keyId) -or $catalogHeader.keyId -notin @($keyRing.keys.keyId)) {
    throw "Catalog key '$($catalogHeader.keyId)' is not available in the server catalog key ring."
}
if ($catalogHeader.keyId -ne $keyRing.currentKeyId) {
    throw "Catalog key '$($catalogHeader.keyId)' is not the current server catalog key '$($keyRing.currentKeyId)'."
}

$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot '.tmp\dotnet-cli-home'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'
$modernProject = Join-Path $repositoryRoot 'cad\autocad-2025-2026\Atlas.AutoCAD.Adapter.csproj'
$legacyProject = Join-Path $repositoryRoot 'cad\autocad-2022-2024\Atlas.AutoCAD.Adapter.csproj'
$machineIdProject = Join-Path $repositoryRoot 'apps\machine-id\Atlas.MachineId.csproj'
$uninstallerProject = Join-Path $repositoryRoot 'apps\uninstaller\Atlas.Uninstaller.csproj'
$commonProperties = @("-p:Version=$Version", "-p:AtlasSigningPublicKeyPath=$signingPublicKey", "-p:AtlasRuntimeConfigurationPath=$runtimeConfiguration")
Invoke-DotNet -Arguments (@('build', $legacyProject, '-c', 'Release') + $commonProperties) -FailureMessage 'AutoCAD 2022-2024 adapter build failed.'
Invoke-DotNet -Arguments (@('build', $modernProject, '-c', 'Release', '-p:AtlasAutoCadRuntime=net8') + $commonProperties) -FailureMessage 'AutoCAD 2025-2026 Update 1.1 adapter build failed.'
Invoke-DotNet -Arguments (@('build', $modernProject, '-c', 'Release', '-p:AtlasAutoCadRuntime=net10') + $commonProperties) -FailureMessage 'AutoCAD 2026 Update 1.2+ adapter build failed.'
Invoke-DotNet -Arguments @('publish', $machineIdProject, '-c', 'Release', "-p:Version=$Version") -FailureMessage 'Machine identifier release build failed.'
Invoke-DotNet -Arguments @('build', $uninstallerProject, '-c', 'Release', "-p:Version=$Version") -FailureMessage 'Uninstaller release build failed.'
$machineIdExecutable = Join-Path $repositoryRoot 'apps\machine-id\bin\Release\net10.0-windows\win-x64\publish\AtlasCad-Identificador.exe'
$uninstallerExecutable = Join-Path $repositoryRoot 'apps\uninstaller\bin\Release\net48\Desinstalar-AtlasCad.exe'

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

$variants = @(
    @{ Slug='autocad-2022-2024'; Description='ATLAS CAD for AutoCAD 2022, 2023 and 2024'; SeriesMin='R24.1'; SeriesMax='R24.3'; ContentFolder='2022-2024'; AdapterFile='Atlas.AutoCAD.Adapter.2022.dll'; AdapterOutput=(Join-Path $repositoryRoot 'cad\autocad-2022-2024\bin\Release\net48') },
    @{ Slug='autocad-2025-2026-1-1'; Description='ATLAS CAD for AutoCAD 2025 and AutoCAD 2026 through Update 1.1'; SeriesMin='R25.0'; SeriesMax='R25.1'; ContentFolder='2025-2026'; AdapterFile='Atlas.AutoCAD.Adapter.2026.dll'; AdapterOutput=(Join-Path $repositoryRoot 'cad\autocad-2025-2026\bin\Release\net8.0-windows'); CopyCore=$true },
    @{ Slug='autocad-2026-1-2-plus'; Description='ATLAS CAD for AutoCAD 2026 Update 1.2 or later'; SeriesMin='R25.1'; SeriesMax='R25.1'; ContentFolder='2026-1.2-plus'; AdapterFile='Atlas.AutoCAD.Adapter.2026.dll'; AdapterOutput=(Join-Path $repositoryRoot 'cad\autocad-2025-2026\bin\Release\net10.0-windows'); CopyCore=$true }
)

New-Item -ItemType Directory -Force -Path $output | Out-Null
$template = Get-Content -LiteralPath (Join-Path $repositoryRoot 'installer\windows\templates\PackageContents.xml') -Raw
$signTool = $null
if ($CertificateThumbprint) {
    $signTool = (Get-Command signtool.exe -ErrorAction SilentlyContinue).Source
    if (-not $signTool) { throw 'signtool.exe was not found in PATH.' }
    & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $machineIdExecutable $uninstallerExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Executable Authenticode signing failed.' }
} elseif ($RequireCodeSigning) { throw 'RequireCodeSigning was set but no CertificateThumbprint was supplied.' }

foreach ($variant in $variants) {
    $releaseRoot = Join-Path $output ("AtlasCad-$Version-" + $variant.Slug + '-win64')
    if (Test-Path -LiteralPath $releaseRoot) { Remove-Item -LiteralPath $releaseRoot -Recurse -Force }
    $bundle = Join-Path $releaseRoot 'AtlasCad.bundle'
    $content = Join-Path $bundle ('Contents\Windows\' + $variant.ContentFolder)
    New-Item -ItemType Directory -Force -Path $content | Out-Null
    $adapterSource = Join-Path $variant.AdapterOutput $variant.AdapterFile
    Copy-Item -LiteralPath $adapterSource -Destination $content
    if ($variant.CopyCore) { Copy-Item -LiteralPath (Join-Path $variant.AdapterOutput 'Atlas.Cad.Core.dll') -Destination $content }
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'cad\legacy-lisp\atlas-bridge.lsp') -Destination $content
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'installer\windows\atlas-loader.lsp') -Destination $content
    Copy-Item -LiteralPath $compiledFas -Destination $content
    Copy-Item -LiteralPath $catalog -Destination (Join-Path $content 'steel.atlascatalog')

    $versionBytes = [Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes("AtlasCad:${Version}:$($variant.Slug)"))
    $guidBytes = New-Object byte[] 16
    [Array]::Copy($versionBytes, $guidBytes, 16)
    $productCode = '{' + (New-Object Guid (,$guidBytes)).ToString().ToUpperInvariant() + '}'
    $manifest = $template.Replace('__VERSION__', $Version).Replace('__PRODUCT_CODE__', $productCode).
        Replace('__DESCRIPTION__', $variant.Description).Replace('__SERIES_MIN__', $variant.SeriesMin).
        Replace('__SERIES_MAX__', $variant.SeriesMax).Replace('__CONTENT_FOLDER__', $variant.ContentFolder).
        Replace('__ADAPTER_FILE__', $variant.AdapterFile)
    [IO.File]::WriteAllText((Join-Path $bundle 'PackageContents.xml'), $manifest, (New-Object Text.UTF8Encoding($false)))

    $adapter = Join-Path $content $variant.AdapterFile
    if ($signTool) {
        & $signTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $adapter
        if ($LASTEXITCODE -ne 0) { throw "Adapter Authenticode signing failed for $($variant.Slug)." }
        & $signTool verify /pa $adapter
        if ($LASTEXITCODE -ne 0) { throw "Adapter Authenticode verification failed for $($variant.Slug)." }
    }

    $hashLines = Get-ChildItem -LiteralPath $bundle -Recurse -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($bundle.Length + 1).Replace('\', '/')
        '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash, $relative
    }
    [IO.File]::WriteAllLines((Join-Path $bundle 'SHA256SUMS.txt'), $hashLines, (New-Object Text.UTF8Encoding($false)))
    & (Join-Path $repositoryRoot 'scripts\Test-AtlasBundle.ps1') -BundlePath $bundle -RequireCodeSigning:$RequireCodeSigning
    if ($LASTEXITCODE -ne 0) { throw "Bundle validation failed for $($variant.Slug)." }

    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Install-AtlasCad.ps1') -Destination $releaseRoot
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Uninstall-AtlasCad.ps1') -Destination $releaseRoot
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'scripts\Test-AtlasBundle.ps1') -Destination $releaseRoot
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'installer\windows\Instalar-AtlasCad.cmd') -Destination $releaseRoot
    Copy-Item -LiteralPath $machineIdExecutable -Destination $releaseRoot
    Copy-Item -LiteralPath $uninstallerExecutable -Destination $releaseRoot

    $archive = Join-Path $output ("AtlasCad-$Version-" + $variant.Slug + '-win64.zip')
    if (Test-Path -LiteralPath $archive) { Remove-Item -LiteralPath $archive -Force }
    Compress-Archive -Path (Get-ChildItem -LiteralPath $releaseRoot).FullName -DestinationPath $archive -CompressionLevel Optimal
    Copy-Item -LiteralPath $archive -Destination (Join-Path $output ("AtlasCad-" + $variant.Slug + '-win64.zip')) -Force
    Write-Host "Release archive: $archive"
}

Copy-Item -LiteralPath $machineIdExecutable -Destination (Join-Path $output 'AtlasCad-Identificador.exe') -Force
Copy-Item -LiteralPath (Join-Path $output "AtlasCad-$Version-autocad-2026-1-2-plus-win64.zip") -Destination (Join-Path $output 'AtlasCad-latest-win64.zip') -Force
Write-Host ($(if ($CertificateThumbprint) { 'Authenticode: signed' } else { 'Authenticode: unsigned development build' }))
