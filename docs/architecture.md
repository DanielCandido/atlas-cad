# Architecture V1

## Runtime path

`STEELFLOW.m4.lsp` keeps the established selection/profile workflow and sends
opaque request IDs to the version-specific AutoCAD adapter. The adapter owns
catalog lookup, exact MLINE-style creation, MLINE creation and the M4 license
gate. A failed bridge or license check preserves the source geometry.

The AutoCAD 2026 adapter currently targets `net10.0-windows`, matching the
installed AutoCAD 2026.1.2 runtime. The 2022-2024 adapter remains `net48` and is
pending the M6 installed-version matrix.

## Licensing path

The ASP.NET Core API issues ECDSA P-256 signed 72-hour entitlements. A Windows
CNG ECDSA P-256 key is generated per device and kept non-exportable. Activation
and renewal require a proof signed by that device key; the plugin validates the
server signature, protocol, issuer, product, module, device fingerprint and
expiry locally.

The M4 API uses an atomic JSON repository for single-node development. A
transactional PostgreSQL repository, managed signing keys, audit events, HTTPS
deployment and signing-key rotation are production prerequisites.

## Remaining milestones

- M5: completed and accepted in AutoCAD 2026 with AES-256-GCM packaging,
  proof-bound server key release and a DPAPI-protected 72-hour device cache.
- M6: Autodesk bundle, signing, upgrades and AutoCAD 2022-2026 matrix.
- Web account, checkout and customer self-service remain outside M4.
