# Atlas device licensing

The AutoCAD adapter owns a non-exportable ECDSA P-256 device key in the current
Windows user's CNG key store. It signs activation and renewal proofs, validates
the server-signed entitlement locally, and signs its local state. Protected
catalog/style/draw operations fail closed.

Runtime commands:

- `ATLAS_LICENSE_ACTIVATE`: activate with a one-time server code.
- `ATLAS_LICENSE_STATUS`: validate the local entitlement and show expiry.
- `ATLAS_LICENSE_RENEW`: renew the 72-hour entitlement online.
- `SFACTIVATE`: STEELFLOW alias for `ATLAS_LICENSE_ACTIVATE`.

`ATLAS_LICENSE_API` selects the server URL. The default is
`http://127.0.0.1:5080/` for local M4 testing. Production must use HTTPS and a
production signing key/public trust anchor.
