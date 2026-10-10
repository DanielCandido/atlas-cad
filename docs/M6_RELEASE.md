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
`SHA256SUMS.txt`, validates the bundle and creates a ZIP. The validator rejects
MLN/MLNX, private LSP, plaintext catalogs, private keys, Autodesk API DLLs,
missing manifest modules, path escapes, hash mismatches and representative
profile names inside the encrypted package.

The development artifact is unsigned. A production build must provide an
Authenticode certificate thumbprint and `-RequireCodeSigning`; the build then
uses `signtool sign` with SHA-256 and an RFC 3161 timestamp and verifies the
result before packaging. The signing private key is never a repository input.

## Install, upgrade and recovery

`Install-AtlasCad.ps1` requires AutoCAD to be closed, validates the source,
copies to a staging bundle, validates again, and atomically swaps it into place.
An existing bundle is retained as a temporary rollback copy until the swap
succeeds. Downgrades require `-AllowDowngrade`.

The distributable ZIP places `Instalar-AtlasCad.cmd`, the installer, validator,
uninstaller and `AtlasCad.bundle` at its root. A normal user can extract the ZIP
and double-click the CMD launcher; administrative privileges are not required
for the default current-user installation.

`Uninstall-AtlasCad.ps1` removes only the exact bundle directory. User license,
device key, protected catalog and diagnostics are preserved by default.
`-PurgeUserData` requires explicit confirmation and permanently removes that
local state.

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
