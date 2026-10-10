# Catalog builder

The M2 normalization mode reads private MLN files, applies explicit collision
overrides and writes a validated private JSON intermediate:

```powershell
dotnet run --project packages/catalog-builder -- `
  C:\private\mlines `
  C:\private\steel.atlascatalog.json `
  --overrides C:\private\catalog-overrides.json
```

The `.atlascatalog.json` intermediate contains proprietary properties in
plaintext and must never be distributed. M5 turns it into an authenticated
AES-256-GCM package:

```powershell
dotnet run --project packages/catalog-builder -- protect `
  C:\private\steel.atlascatalog.json `
  C:\build\steel.atlascatalog `
  C:\private\catalog-keys.json `
  1
```

The public header contains versions, product/catalog IDs, key ID, nonce and
hash. The complete header is GCM associated data, so changes to metadata,
ciphertext or tag prevent decryption. The 256-bit key is not included in the
package or client. `ATLAS_INSTALL_CATALOG` accepts only the encrypted package,
validates all 652 decrypted styles in memory and persists only ciphertext.
