# Windows / Autodesk bundle

M6 produces an Autodesk Application Bundle for the locally validated AutoCAD
2026.1.2+ runtime (`R25.1`, Win64, .NET 10). The release contains:

- version-specific C# adapter and shared core;
- public bridge/loader LSP files;
- proprietary STEELFLOW compiled as FAS by AutoCAD Core Console;
- authenticated encrypted `steel.atlascatalog`;
- signed `AtlasCad-Identificador.exe` for self-service device activation;
- `PackageContents.xml` and SHA-256 inventory.

It never includes private STEELFLOW LSP, MLN/MLNX, plaintext catalog JSON,
catalog/signing keys or Autodesk assemblies.

Build a development release:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-AtlasRelease.ps1 `
  -Version 1.0.0 `
  -PublicLicenseApiUrl https://api.seudominio.com.br
```

For production, sign the adapter and make signing mandatory:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-AtlasRelease.ps1 `
  -Version 1.0.0 `
  -PublicLicenseApiUrl https://api.seudominio.com.br `
  -CertificateThumbprint <AUTHENTICODE_CERTIFICATE_THUMBPRINT> `
  -RequireCodeSigning
```

Install per user after closing AutoCAD:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Install-AtlasCad.ps1 `
  -BundlePath .tmp/release/AtlasCad-1.0.0-win64/AtlasCad.bundle
```

The release ZIP also contains `Instalar-AtlasCad.cmd`. After extracting the
archive, double-click it for a validated per-user installation without an
elevated terminal. It deliberately refuses to continue while `acad.exe` is
running, so the installer never closes unsaved AutoCAD work.

Uninstall while preserving activation and catalog cache:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Uninstall-AtlasCad.ps1
```

`-PurgeUserData` is deliberately separate and destructive: it removes the
device/license state, catalog cache and diagnostics under `%LOCALAPPDATA%\AtlasCad`.
