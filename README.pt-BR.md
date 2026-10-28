# ATLAS CAD

[English](README.md)

O ATLAS CAD é um hub de bibliotecas e ferramentas para AutoCAD. A plataforma
reúne automações especializadas que reduzem tarefas repetitivas no dia a dia de
engenheiros, projetistas e desenhistas, com catálogo, documentação,
licenciamento, instalação e atualizações centralizados em uma única conta.

O SteelFlow é a primeira biblioteca de referência do ecossistema. Ele converte
eixos de perfis representados por LINE, ARC ou LWPOLYLINE em geometrias MLINE
baseadas em catálogo. A arquitetura comercial e técnica foi projetada para
receber outras bibliotecas independentes, inclusive soluções que não dependam
de MLINE ou de catálogos MLN.

## Visão do produto

- **Hub, não plugin único:** cada biblioteca resolve uma etapa específica do
  fluxo de trabalho no AutoCAD.
- **Descoberta centralizada:** usuários encontram, comparam e assinam
  bibliotecas pelo marketplace web.
- **Uma conta:** assinaturas, cobranças, downloads, dispositivos e licenças são
  administrados na mesma área do cliente.
- **Experiência guiada:** cada biblioteca pode oferecer documentação própria,
  instalador, desinstalador, versões e compatibilidade declarada.
- **Arquitetura extensível:** novas bibliotecas devem coexistir sem substituir
  arquivos, licenças ou configurações de outras soluções instaladas.

## Situação atual

- O SteelFlow é a primeira biblioteca publicada e o caso de referência atual.
- M0–M5 foram aceitos no AutoCAD 2026.1.2 ou posterior.
- M6 gera os payloads net48, .NET 8 e .NET 10 em um instalador unificado.
- O aceite em runtime do AutoCAD 2022–2025 ainda depende de instalações
  autorizadas; gerar o build não equivale a validar sua execução.
- A camada web já suporta catálogo, documentação, checkout, assinaturas e
  administração de múltiplas bibliotecas; a generalização completa do pipeline
  de build está descrita no plano de automação.

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
