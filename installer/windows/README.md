# Windows / Autodesk bundle

[Português (Brasil)](README.pt-BR.md)

M6 produces an Autodesk Application Bundle for the locally validated AutoCAD
2026.1.2+ runtime (`R25.1`, Win64, .NET 10). The release contains:

- version-specific C# adapter and shared core;
- public bridge/loader LSP files;
- proprietary STEELFLOW compiled as FAS by AutoCAD Core Console;
- authenticated encrypted `steelflow.atlascatalog`;
- `PackageContents.xml` and SHA-256 inventory.

It never includes private STEELFLOW LSP, MLN/MLNX, plaintext catalog JSON,
catalog/signing keys or Autodesk assemblies.

The customer-facing release produces `Instalador-AtlasCad.exe`. This
signed per-user wizard embeds all supported payloads, detects installed AutoCAD
versions, opens browser-based account pairing and prepares the device license
and protected catalog without copying a machine identifier. See the
[installer flow](../../docs/INSTALLER_FLOW.md).

Build a development release:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-AtlasRelease.ps1 `
  -Version 1.0.0 `
  -PublicLicenseApiUrl https://api.seudominio.com.br `
  -CertificateThumbprint <AUTHENTICODE_CERTIFICATE_THUMBPRINT>
```

Production uses the same command. Signing and signature verification are always
mandatory in the current release script:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Build-AtlasRelease.ps1 `
  -Version 1.0.0 `
  -PublicLicenseApiUrl https://api.seudominio.com.br `
  -CertificateThumbprint <AUTHENTICODE_CERTIFICATE_THUMBPRINT>
```

For development, an extracted bundle can still be installed directly after
closing AutoCAD:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Install-AtlasCad.ps1 `
  -BundlePath .tmp/installer-payload/autocad-2026-1-2-plus/AtlasCad.bundle
```

The commercial path is the signed unified installer. It deliberately refuses
to continue while `acad.exe` is running, so it never closes unsaved AutoCAD
work.

Uninstall while preserving activation and catalog cache:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/Uninstall-AtlasCad.ps1
```

`-PurgeUserData` is deliberately separate and destructive: it removes the
device/license state, catalog cache and diagnostics under `%LOCALAPPDATA%\AtlasCad`.
