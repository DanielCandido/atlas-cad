# M4 - Device licensing

## Implemented contract

M4 uses ECDSA P-256 at both trust boundaries. The server signs a short-lived
entitlement and the device signs activation/renewal proofs. The device private
key is created as a named, non-exportable Windows CNG key; the API stores only
its public coordinates and fingerprint.

An entitlement contains protocol version, issuer, signing-key ID, product,
license and activation IDs, license generation, device-key fingerprint,
issue/expiry timestamps and enabled modules. The plugin verifies every field
required by the M4 plan before style lookup, style creation or MLINE creation.
The offline window is 72 hours and accepted clock skew is five minutes.

## Remote activation before production hosting

An activation code is not a standalone license file. The device must redeem it
against the API because the returned entitlement is signed for that device's
non-exportable key. Installing the protected catalog also requires the device
to request its catalog key. Consequently, generating a code locally and only
sending the text to a remote user does not work while that user cannot reach
the API.

Until production hosting is available, run the API on the administrator's
machine and expose it temporarily through an authenticated private network or
an HTTPS reverse tunnel. Set `ATLAS_LICENSE_API` on the remote Windows account
to that HTTPS base URL, restart AutoCAD, and keep the endpoint available while
the user runs `SFACTIVATE` and `ATLAS_INSTALL_CATALOG`. Never send the signing
private key, catalog key ring, or admin API key to the customer. The endpoint
must be reachable again before the 72-hour entitlement expires.

A genuinely disconnected workflow requires a separate device-bound
challenge/response feature. It must carry a server-signed entitlement and a
device-protected catalog-key envelope; that feature is not part of M4-M6 and
must not be simulated by distributing server secrets.

Local state lives under `%LOCALAPPDATA%\AtlasCad\license\v1\state.json`, is
ACL-restricted to the current user/system/administrators and is signed by the
device key. Copying that folder to another Windows installation does not copy
the non-exportable private key and validation fails. A backwards-clock check
rejects a clock older than the last validated timestamp.

## API and administration

Required server environment variables:

- `ATLAS_LICENSE_SIGNING_KEY_PATH`: PKCS#8 ECDSA private-key file.
- `ATLAS_LICENSE_ADMIN_KEY`: at least 24 characters, sent only in the
  `X-Atlas-Admin-Key` header for administrative calls.
- `ConnectionStrings__Atlas`: PostgreSQL connection string for durable licensing and commerce state.

Endpoints:

- `POST /api/v1/activations`
- `POST /api/v1/renewals`
- `POST /api/v1/admin/licenses`
- `POST /api/v1/admin/licenses/{licenseId}/transfer`
- `GET /api/v1/signing-key`

The manager can create keys and exercise the flow:

```powershell
dotnet run --project apps/license-manager -- generate-signing-key <private.pk8> <public.json> <key-id>
dotnet run --project apps/license-manager -- issue http://127.0.0.1:5080 <admin-key> <customer>
dotnet run --project apps/license-manager -- transfer http://127.0.0.1:5080 <admin-key> <license-id>
dotnet run --project apps/license-manager -- simulate-device http://127.0.0.1:5080 <activation-code>
```

Activation codes are random one-time values; only their SHA-256 hashes are
stored. Transfer increments the license generation and revokes the old
activation, so its next renewal is rejected. An entitlement already cached on
an offline old device remains usable until its signed expiry. Instant offline
revocation is impossible by design.

## Production persistence

Licenses, activation codes, devices, generations, replay nonces, subscriptions
and webhook audit events are stored in PostgreSQL with transactions and unique
constraints. Production still requires HTTPS, encrypted backups, protected
signing/catalog keys and a documented key-rotation procedure.

The catalog itself remains readable in M4. Authenticated encryption and key
release belong to M5; MLINE styles persisted in a DWG will remain inspectable.
