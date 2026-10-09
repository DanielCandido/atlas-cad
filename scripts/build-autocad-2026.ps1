[CmdletBinding()]
param(
    [string]$AutodeskAutoCADRoot = 'F:\Softwares\AutoCad2026\AutoCAD 2026'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'cad\autocad-2025-2026\Atlas.AutoCAD.Adapter.csproj'
$dotnetCliHome = Join-Path $repositoryRoot '.tmp\dotnet-cli-home'
New-Item -ItemType Directory -Force -Path $dotnetCliHome | Out-Null
$env:DOTNET_CLI_HOME = $dotnetCliHome
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_NOLOGO = '1'

foreach ($assembly in @('acmgd.dll', 'acdbmgd.dll', 'accoremgd.dll')) {
    $path = Join-Path $AutodeskAutoCADRoot $assembly
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Required Autodesk assembly was not found: $path"
    }
}

$sdk10 = @(& dotnet --list-sdks) | Where-Object { $_ -match '^10\.' }
if ($sdk10.Count -eq 0) {
    throw 'The .NET 10 SDK is required for AutoCAD 2026.1.2 or later.'
}

& dotnet build $project --nologo "-p:AutodeskAutoCADRoot=$AutodeskAutoCADRoot"
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
