[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repositoryRoot

try {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot '.git'))) {
        throw 'Metadados Git ausentes; não é possível verificar arquivos rastreados.'
    }

    & git rev-parse --is-inside-work-tree 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw 'Metadados Git ausentes; não é possível verificar arquivos rastreados.'
    }

    $pathspecs = @(
        'docs/knowledge/**',
        'fixtures/private/**',
        'tests/autocad/baseline/private/**',
        '*.dwg',
        '*.dxf',
        '*.lsp',
        '*.fas',
        '*.vlx',
        '*.bak',
        '*.mln',
        '*.mlnx',
        '*.atlascatalog.json'
    )

    $allowedPublicAssets = @(
        'cad/legacy-lisp/atlas-bridge.lsp',
        'tests/Atlas.Core.Tests/Fixtures/styles-single.mln',
        'tests/Atlas.Core.Tests/Fixtures/styles-multiple.mln'
    )
    $tracked = @(& git ls-files -- $pathspecs) |
        Where-Object { $allowedPublicAssets -notcontains ($_ -replace '\\', '/') }
    if ($LASTEXITCODE -ne 0) {
        throw 'git ls-files falhou durante a verificação.'
    }

    if ($tracked.Count -gt 0) {
        Write-Error ("Ativos CAD privados estão rastreados pelo Git:`n - " + ($tracked -join "`n - "))
        exit 1
    }

    Write-Host 'OK: nenhum ativo CAD privado conhecido está rastreado pelo Git.'
}
finally {
    Pop-Location
}
