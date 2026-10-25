# Architecture V1

[Português (Brasil)](architecture.pt-BR.md)

## Runtime path

`STEELFLOW.m4.lsp` keeps the established selection/profile workflow and sends
opaque request IDs to the version-specific AutoCAD adapter. The adapter owns
catalog lookup, exact MLINE-style creation, MLINE creation and the M4 license
gate. A failed bridge or license check preserves the source geometry.

The modern adapter targets both `net8.0-windows` and `net10.0-windows`; the
2022-2024 adapter targets `net48`. Runtime acceptance outside the installed
AutoCAD 2026.1.2 environment remains part of the M6 version matrix.

## Licensing path

The ASP.NET Core API issues ECDSA P-256 signed 72-hour entitlements. A Windows
CNG ECDSA P-256 key is generated per device and kept non-exportable. Activation
and renewal require a proof signed by that device key; the plugin validates the
server signature, protocol, issuer, product, module, device fingerprint and
expiry locally.

The production API stores commerce, licenses, device bindings, replay nonces
and webhook audit events in PostgreSQL with transactions and unique
constraints. Deployment also requires encrypted backups, protected signing
keys, HTTPS and a documented key-rotation procedure.

## Remaining milestones

- M5: completed and accepted in AutoCAD 2026 with AES-256-GCM packaging,
  proof-bound server key release and a DPAPI-protected 72-hour device cache.
- M6: Autodesk bundle, signing, upgrades and AutoCAD 2022-2026 matrix.
- The web account, Asaas checkout, customer portal and protected download are
  implemented as the commercial layer around the licensing protocol.
