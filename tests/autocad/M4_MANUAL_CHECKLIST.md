# M4 manual checklist - AutoCAD 2026

**Resultado:** aprovado em runtime pelo usuário em 2026-10-09.

Use `.tmp/m4-runtime-v1`. Start the licensing API with the same private key
whose public key is compiled into the adapter, then issue a fresh activation
code with `Atlas.LicenseManager`.

1. Close AutoCAD, replace the old runtime DLL/LSP files, and reopen it.
2. `NETLOAD` `Atlas.AutoCAD.Adapter.2026.dll`, then `APPLOAD`
   `atlas-bridge.lsp` and `STEELFLOW.m4.lsp`.
3. Run `ATLAS_LICENSE_STATUS`; expected: `license_missing` on a clean user.
4. Run `STEELFLOW`; expected: it stops before entity selection.
5. Run `SFACTIVATE`, paste the one-time activation code, then run
   `ATLAS_LICENSE_STATUS`; expected: valid with a date about 72 hours ahead.
6. Run the real `STEELFLOW` LINE/text/profile flow; expected: conversion works.
7. Run `ATLAS_LICENSE_RENEW`; expected: a new valid 72-hour entitlement.
8. Transfer the license on the server and run `ATLAS_LICENSE_RENEW`; expected:
   `activation_revoked`. The cached entitlement remains valid only until expiry.
9. Copy only `%LOCALAPPDATA%\AtlasCad\license\v1` to a different Windows user or
   PC; expected: `license_state_invalid` or `token_device_mismatch`.
10. With an expired/tampered state, protected style and draw requests must fail
    and leave source geometry untouched.

Record the command log. AutoCAD 2022 remains part of the later cross-version
matrix and is not claimed by this checklist.
