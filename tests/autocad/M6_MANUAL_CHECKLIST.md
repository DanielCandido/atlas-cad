# M6 manual checklist - AutoCAD 2026.1.2+

Artifact: `.tmp/release/AtlasCad-1.0.0-win64/AtlasCad.bundle`.

## Clean install and automatic load

1. Close every `acad.exe` process.
2. Extract the release ZIP and double-click `Instalar-AtlasCad.cmd` (or run
   `scripts/Install-AtlasCad.ps1` with the bundle path during development).
3. Confirm `%APPDATA%\Autodesk\ApplicationPlugins\AtlasCad.bundle` exists.
4. Start AutoCAD normally; do not run `NETLOAD` or `APPLOAD`.
5. Confirm the command line reports the ATLAS bridge and STEELFLOW v5 loaders.
6. On a truly clean user profile, run `SF_ACTIVATE` and then
   `ATLAS_INSTALL_CATALOG`; confirm 652 authenticated styles. On an upgrade,
   the existing valid device state and protected cache may be reused.
7. Run `ATLAS_DIAGNOSTICS`; confirm version `1.0.0`, the correct AutoCAD/runtime,
   valid license status and protected catalog version/key ID.
8. Run `SF_RELOAD` and the established `W150X13` conversion with `SF`.
9. In a clean DWG, convert `W250X17,9` as PLANTA and confirm that
   `W250X17_9P` is installed and both source axes become MLINE entities.

## Upgrade and rollback safety

1. Close AutoCAD and reinstall the same bundle; installation must be idempotent.
2. Build a higher patch version and install it; diagnostics must show the new
   version without reactivation or catalog plaintext migration.
3. Attempt an older version without `-AllowDowngrade`; it must be refused.
4. Corrupt a disposable source bundle hash and attempt install; validation must
   fail before the installed working bundle changes.

## Uninstall and recovery

1. Close AutoCAD and run `scripts/Uninstall-AtlasCad.ps1 -Confirm:$false`.
2. Start AutoCAD; ATLAS commands must no longer autoload.
3. Reinstall the bundle; the preserved device license/catalog cache should work.
4. Test `-PurgeUserData` only on a disposable activation and confirm the command
   clearly requests destructive confirmation.

Record command output and mark the AutoCAD 2026 row in `VERSION_MATRIX.md`.
