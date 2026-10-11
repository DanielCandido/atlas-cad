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

The production API URL is embedded in the signed adapter at release build time.
`ATLAS_LICENSE_API` can override it only for controlled development tests.
Production builds require HTTPS and embed the matching public signing-key
descriptor; private keys are never included in the bundle.
