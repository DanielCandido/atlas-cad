# Security decisions and limits

[Português (Brasil)](security.pt-BR.md)

- Never ship private LSP/MLN source, server signing private keys, admin API keys
  or plaintext activation codes in source control or the client package.
- M4 uses ECDSA P-256 signatures; it has no deterministic key generator, XOR
  protection or universal client secret.
- The per-device private key is a named, non-exportable Windows CNG key. The
  server stores only public key material and a fingerprint.
- Installer pairing uses a ten-minute browser confirmation and fresh ECDSA
  proofs for creation, polling and completion. Approval alone does not revoke a
  previous machine; replacement and activation occur atomically when the new
  device proves possession of its private key. Completion is idempotent for
  safe retry after a lost response.
- Entitlements are bound to product, activation, generation and device and
  expire after 72 hours. Missing, malformed, tampered, copied or expired local
  state is denied by protected C# operations.
- Transfer revokes future renewal of the previous activation. Immediate
  revocation while that machine is offline is impossible; its cached signed
  entitlement remains valid until expiry.
- M4 rate-limits state-changing API endpoints and compares the admin key in
  constant time. Production still requires HTTPS, managed secrets/signing keys,
  durable audit logs and a transactional database.
- M5 encrypts and authenticates the catalog with AES-256-GCM. The client has no
  embedded catalog key; an active device obtains a versioned key from the server
  using a signed proof and caches it with current-user Windows DPAPI.
- GCM nonces are random 96-bit values generated for every package build and the
  header is authenticated as AAD. Reusing a key/nonce pair is forbidden.
- Styles stored inside a DWG and plaintext present in an authorized process
  remain inspectable even after catalog encryption.
- Client code can be reverse engineered. The security boundary is short-lived
  server authority plus device proof, not secrecy of the plugin binary.
