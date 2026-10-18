# ATLAS CAD

AutoCAD automation plugin that converts LINE/ARC/LWPOLYLINE profile axes into
catalog-backed MLINE geometry. The AutoCAD 2026 path includes device licensing,
a 72-hour offline entitlement and an authenticated encrypted style catalog.

## Current status

- M0-M5 accepted in AutoCAD 2026.1.2+.
- M6 release candidate implemented for AutoCAD `R25.1` / .NET 10.
- AutoCAD 2022-2025 builds remain pending their authorized installations and
  runtime matrix; the current bundle does not claim those versions.

## Developer prerequisites

- .NET SDK 10
- Node.js 20+
- AutoCAD 2026.1.2+ and its managed API assemblies for the adapter/release build
- Private MLN/STEELFLOW fixtures and signing/catalog keys in ignored locations

Run managed tests:

```powershell
dotnet test tests/Atlas.Core.Tests/Atlas.Core.Tests.csproj
dotnet run --project tests/Atlas.Licensing.Smoke/Atlas.Licensing.Smoke.csproj
```

Build the AutoCAD 2026 development release:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-AtlasRelease.ps1 -Version 1.0.0 `
  -PublicLicenseApiUrl https://api.seudominio.com.br
```

See [M6 release documentation](docs/M6_RELEASE.md), the
[implementation plan](docs/IMPLEMENTATION_PLAN.md), and
[security boundaries](docs/security.md). For the current local test deployment,
follow [Cloudflare Tunnel setup](docs/CLOUDFLARE_TUNNEL_LOCAL.md).
The unified guided installer and browser pairing flow are documented in
[INSTALLER_FLOW.md](docs/INSTALLER_FLOW.md).
For database inspection from Windows, follow the
[DBeaver through Podman SSH guide](docs/DBEAVER_PODMAN.md).

Never commit or distribute private LSP/MLN source, plaintext catalog JSON,
activation state, server signing keys or catalog key rings.
