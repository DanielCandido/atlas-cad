# ATLAS CAD

[English](README.md)

Plugin de automação para AutoCAD que converte eixos de perfis em entidades
LINE, ARC ou LWPOLYLINE para geometrias MLINE baseadas em catálogo. O fluxo do
AutoCAD 2026 inclui licenciamento por dispositivo, autorização offline por 72
horas e catálogo de estilos criptografado e autenticado.

## Situação atual

- M0–M5 aceitos no AutoCAD 2026.1.2 ou posterior.
- M6 gera os payloads net48, .NET 8 e .NET 10 em um instalador unificado.
- O aceite em runtime do AutoCAD 2022–2025 ainda depende de instalações
  autorizadas; gerar o build não equivale a validar sua execução.

## Pré-requisitos de desenvolvimento

- SDK .NET 10;
- Node.js 20 ou posterior;
- AutoCAD 2026.1.2 ou posterior e suas APIs gerenciadas para o build do
  adaptador e da release;
- fixtures privados MLN/SteelFlow e chaves de assinatura/catálogo em locais
  ignorados pelo Git.

Executar os testes gerenciados:

```powershell
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj
dotnet run --project tests/Atlas.Licensing.Smoke/Atlas.Licensing.Smoke.csproj
```

Gerar uma release de desenvolvimento:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-AtlasRelease.ps1 `
  -Version 1.0.0 `
  -PublicLicenseApiUrl https://api.seudominio.com.br `
  -CertificateThumbprint <IMPRESSAO_DIGITAL_DO_CERTIFICADO_AUTHENTICODE>
```

Consulte a [documentação da release M6](docs/M6_RELEASE.pt-BR.md), o
[plano de implementação](docs/IMPLEMENTATION_PLAN.md), a
[referência de comandos do SteelFlow](docs/STEELFLOW_COMMANDS.md), o
[guia do LISP ao instalador EXE](docs/STEELFLOW_BUILD_TO_EXE.md), o
[plano de automação para múltiplas bibliotecas](docs/LIBRARY_BUILD_AUTOMATION_PLAN.md)
e os [limites de segurança](docs/security.pt-BR.md).

Para o ambiente local, consulte
[Cloudflare Tunnel](docs/CLOUDFLARE_TUNNEL_LOCAL.md). O instalador unificado e o
pareamento pelo navegador estão documentados em
[Fluxo do instalador](docs/INSTALLER_FLOW.md). Para acessar o PostgreSQL pelo
Windows, consulte o [guia do DBeaver](docs/DBEAVER_PODMAN.md).

Nunca versione ou distribua fontes LSP/MLN privadas, catálogo JSON em texto
claro, estado de ativação, chaves privadas de assinatura ou key rings de
catálogo.

