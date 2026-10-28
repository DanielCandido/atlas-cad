# ATLAS CAD

[Português (Brasil)](README.pt-BR.md)

ATLAS CAD is a hub of AutoCAD libraries and tools. The platform brings together
specialized automations that reduce repetitive work for engineers, designers,
and drafters, with catalog discovery, documentation, licensing, installation,
and updates centralized under one account.

SteelFlow is the ecosystem's first reference library. It converts profile axes
represented by LINE, ARC, or LWPOLYLINE entities into catalog-backed MLINE
geometry. The commercial and technical architecture is designed to support
other independent libraries, including solutions that do not rely on MLINE or
MLN catalogs.

## Product vision

- **A hub, not a single plugin:** each library addresses a specific part of an
  AutoCAD workflow.
- **Centralized discovery:** users find, compare, and subscribe to libraries
  through the web marketplace.
- **One account:** subscriptions, billing, downloads, devices, and licenses are
  managed in the same customer portal.
- **Guided experience:** each library can provide its own documentation,
  installer, uninstaller, versions, and compatibility information.
- **Extensible architecture:** new libraries must coexist without replacing the
  files, licenses, or settings of other installed solutions.

## Current status

- SteelFlow is the first published library and the current reference case.
- M0–M5 were accepted in AutoCAD 2026.1.2+.
- M6 builds the net48, .NET 8, and .NET 10 payloads in a unified installer.
- AutoCAD 2022–2025 runtime acceptance remains pending on authorized
  installations; build availability is not the same as runtime acceptance.
- The web layer already supports a multi-library catalog, documentation,
  checkout, subscriptions, and administration; full build-pipeline
  generalization is described in the automation plan.

## Developer prerequisites

- .NET SDK 10
- Node.js 20+
- AutoCAD 2026.1.2+ and its managed API assemblies for the adapter/release build
- Private MLN/SteelFlow fixtures and signing/catalog keys in ignored locations

Run managed tests:

```powershell
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj
dotnet run --project tests/Atlas.Licensing.Smoke/Atlas.Licensing.Smoke.csproj
```

Build the AutoCAD 2026 development release:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-AtlasRelease.ps1 -Version 1.0.0 `
  -PublicLicenseApiUrl https://api.seudominio.com.br `
  -CertificateThumbprint <AUTHENTICODE_CERTIFICATE_THUMBPRINT>
```

See [M6 release documentation](docs/M6_RELEASE.md), the
[implementation plan](docs/IMPLEMENTATION_PLAN.md), the
[SteelFlow command reference](docs/STEELFLOW_COMMANDS.md), the
[SteelFlow build-to-EXE guide](docs/STEELFLOW_BUILD_TO_EXE.md), the
[multi-library build automation plan](docs/LIBRARY_BUILD_AUTOMATION_PLAN.md), and
[security boundaries](docs/security.md). For the current local test deployment,
follow [Cloudflare Tunnel setup](docs/CLOUDFLARE_TUNNEL_LOCAL.md). The unified
guided installer and browser pairing flow are documented in
[INSTALLER_FLOW.md](docs/INSTALLER_FLOW.md). For database inspection from
Windows, follow the [DBeaver through Podman SSH guide](docs/DBEAVER_PODMAN.md).

Never commit or distribute private LSP/MLN source, plaintext catalog JSON,
activation state, server signing keys, or catalog key rings.
