# M5 - Protected catalog

## Package

The distributed `steel.atlascatalog` envelope uses AES-256-GCM with a fresh
96-bit nonce for each build and a 128-bit authentication tag. Its authenticated
header contains package/product/catalog versions, catalog ID, key ID, algorithm,
nonce, plaintext SHA-256 and creation time. The normalized JSON is ciphertext
and is never installed on disk in plaintext.
After a protected package is installed successfully, the adapter removes the
known M2/M3 plaintext cache at `catalog\v1\steel.atlascatalog.json`.

The adapter rejects unsupported versions/products/algorithms, malformed lengths,
wrong keys, changed headers, changed ciphertext, changed tags and a plaintext
hash mismatch. Decrypted bytes exist only in process memory long enough to parse
and validate the style definitions, then their buffer is cleared.

## Key release and offline behavior

Catalog keys live in a private, versioned server key ring selected through
`ATLAS_CATALOG_KEYS_PATH`. `POST /api/v1/catalog-keys` requires an active,
non-revoked activation refreshed within 72 hours plus a fresh nonce and an
ECDSA proof from that device. Replays are rejected.

The client protects released key material with Windows DPAPI for the current
user and machine, records its expiry in the device-signed license state, and
never stores plaintext key material. It can open the package offline only while
both the entitlement and cached key grant remain valid. Rotation consists of
adding a new server key ID and rebuilding the package with that ID; older keys
remain only as long as old packages must be supported.

Production transport must be HTTPS because key release is confidential at the
transport boundary. The local development API uses HTTP only on loopback.

## Security boundary and limitations

The distributed artifact contains no raw MLN or normalized JSON and no universal
decryption key. Still, authorized client software must eventually decrypt styles
in memory and can be reverse engineered. Styles already written into a DWG are
also inspectable. M5 raises the cost of copying the catalog and prevents offline
use without a recent device entitlement; it does not promise DRM-proof secrecy.

The current M5 implementation is validated on the AutoCAD 2026/.NET 10 adapter.
The net48 AutoCAD 2022-2024 M5 crypto runtime remains part of the M6 version
matrix and is not claimed here.
