# M6 - Packaging and release readiness

## Supported release candidate

The generated `AtlasCad.bundle` is constrained by its manifest to Win64 AutoCAD
series `R25.1`, matching the installed and validated AutoCAD 2026.1.2 runtime.
AutoCAD discovers application bundles below an `Autodesk\ApplicationPlugins`
directory. The per-user installer targets `%APPDATA%\Autodesk\ApplicationPlugins`;
`-Scope AllUsers` targets the corresponding ProgramData directory.

The manifest autoloads the .NET adapter and a small public loader. That loader
loads the public bridge and `STEELFLOW.fas`. The proprietary LSP source is
compiled during release creation by the installed AutoCAD Core Console and is
never copied into the bundle.

## Build and integrity

`scripts/Build-AtlasRelease.ps1` performs a Release build, compiles FAS, creates
the versioned manifest, copies only allow-listed runtime inputs, emits
`SHA256SUMS.txt`, validates the embedded bundles and creates the unified installer. The validator rejects
MLN/MLNX, private LSP, plaintext catalogs, private keys, Autodesk API DLLs,
missing manifest modules, path escapes, hash mismatches and representative
profile names inside the encrypted package.

Every release must provide an Authenticode certificate thumbprint; the build
uses `signtool sign` with SHA-256 and an RFC 3161 timestamp and verifies the
result before packaging. The signing private key is never a repository input.

## Install, upgrade and recovery

The unified installer requires AutoCAD to be closed, validates the embedded
payload, stages each selected bundle and atomically swaps it into place. The
same signed executable is published as `Desinstalar-AtlasCad.exe`; in that mode
it removes all managed ATLAS bundles while preserving user license data.

## Diagnostics and privacy

`ATLAS_DIAGNOSTICS` writes a current-user ACL-protected report under
`%LOCALAPPDATA%\AtlasCad\diagnostics`. It includes plugin/runtime/AutoCAD
versions and status codes. It excludes tokens, catalog keys, activation IDs,
drawing paths and geometry.

## Remaining release gates

- Execute clean install, automatic startup, upgrade and uninstall in AutoCAD
  2026 using `tests/autocad/M6_MANUAL_CHECKLIST.md`.
- Obtain an Authenticode code-signing certificate for a production artifact.
- Build and execute the separate net48/.NET 8 adapters when authorized AutoCAD
  2022-2025 installations and references are available. They are not represented
  as supported by the current `R25.1`-only bundle.
