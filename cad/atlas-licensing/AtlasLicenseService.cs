using System;
using System.IO;
using System.Security.Cryptography;
using Atlas.Licensing;

namespace Atlas.AutoCAD.Adapter;

internal sealed class AtlasLicenseService
{
    private readonly DeviceKeyStore _keys = new DeviceKeyStore();
    private readonly SecureLicenseStore _store = new SecureLicenseStore();
    private long _lastRenewalAttemptUnix;

    public EntitlementClaims Activate(string activationCode)
    {
        if (string.IsNullOrWhiteSpace(activationCode))
            throw new LicenseOperationException("activation_code_missing", "Activation code is required.");
        using var deviceKey = _keys.OpenOrCreate();
        var parameters = deviceKey.ExportParameters(false);
        var request = new ActivationRequest
        {
            ProtocolVersion = LicensingProtocol.CurrentVersion,
            ActivationCode = activationCode.Trim(),
            DevicePublicKeyX = LicenseCrypto.Base64UrlEncode(parameters.Q.X!),
            DevicePublicKeyY = LicenseCrypto.Base64UrlEncode(parameters.Q.Y!)
        };
        request.Proof = LicenseCrypto.Base64UrlEncode(deviceKey.SignData(
            LicenseCrypto.ActivationProofBytes(request), HashAlgorithmName.SHA256));
        using var api = new LicenseApiClient();
        var response = api.Activate(request);
        var claims = ValidateToken(response.EntitlementToken, deviceKey);
        Save(response, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), deviceKey);
        return claims;
    }

    public EntitlementClaims Renew()
    {
        using var deviceKey = _keys.OpenOrCreate();
        var state = _store.Read(deviceKey) ?? throw new LicenseOperationException("license_missing", "License is not activated.");
        var nonceBytes = new byte[24];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(nonceBytes);
        var request = new RenewalRequest
        {
            ProtocolVersion = LicensingProtocol.CurrentVersion,
            ActivationId = state.ActivationId,
            Nonce = LicenseCrypto.Base64UrlEncode(nonceBytes)
        };
        request.Proof = LicenseCrypto.Base64UrlEncode(deviceKey.SignData(
            LicenseCrypto.RenewalProofBytes(request), HashAlgorithmName.SHA256));
        using var api = new LicenseApiClient();
        var response = api.Renew(request);
        var claims = ValidateToken(response.EntitlementToken, deviceKey);
        Save(response, Math.Max(state.LastSeenUnix, DateTimeOffset.UtcNow.ToUnixTimeSeconds()), deviceKey, state.CatalogKeys);
        return claims;
    }

    public byte[] GetCatalogKey(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId)) throw new LicenseOperationException("catalog_key_id_invalid", "Catalog key ID is required.");
        var claims = RequireValid();
        using var deviceKey = _keys.OpenOrCreate();
        var state = _store.Read(deviceKey) ?? throw new LicenseOperationException("license_missing", "License is not activated.");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var cached = state.CatalogKeys.Find(item => string.Equals(item.KeyId, keyId, StringComparison.Ordinal));
        if (cached != null && cached.ExpiresAtUnix > now)
        {
            try
            {
                var key = WindowsDataProtection.Unprotect(LicenseCrypto.Base64UrlDecode(cached.ProtectedKey));
                if (key.Length != 32) throw new CryptographicException("Catalog key has an invalid length.");
                return key;
            }
            catch (Exception exception) when (exception is CryptographicException || exception is FormatException || exception is System.ComponentModel.Win32Exception)
            {
                throw new LicenseOperationException("catalog_key_cache_invalid", "Cached catalog key cannot be opened on this device.");
            }
        }

        var nonce = new byte[24];
        using (var random = RandomNumberGenerator.Create()) random.GetBytes(nonce);
        var request = new CatalogKeyRequest
        {
            ProtocolVersion = LicensingProtocol.CurrentVersion,
            ActivationId = state.ActivationId,
            KeyId = keyId,
            Nonce = LicenseCrypto.Base64UrlEncode(nonce)
        };
        request.Proof = LicenseCrypto.Base64UrlEncode(deviceKey.SignData(
            LicenseCrypto.CatalogKeyProofBytes(request), HashAlgorithmName.SHA256));
        using var api = new LicenseApiClient();
        var response = api.GetCatalogKey(request);
        if (response.ProtocolVersion != LicensingProtocol.CurrentVersion ||
            !string.Equals(response.KeyId, keyId, StringComparison.Ordinal) || response.ExpiresAtUnix <= now)
            throw new LicenseOperationException("catalog_key_response_invalid", "Catalog key response is invalid.");
        byte[] released;
        try { released = LicenseCrypto.Base64UrlDecode(response.KeyMaterial); }
        catch (FormatException) { throw new LicenseOperationException("catalog_key_response_invalid", "Catalog key response is invalid."); }
        if (released.Length != 32) throw new LicenseOperationException("catalog_key_response_invalid", "Catalog key response is invalid.");
        state.CatalogKeys.RemoveAll(item => string.Equals(item.KeyId, keyId, StringComparison.Ordinal));
        state.CatalogKeys.Add(new ProtectedCatalogKey
        {
            KeyId = keyId,
            ProtectedKey = LicenseCrypto.Base64UrlEncode(WindowsDataProtection.Protect(released)),
            ExpiresAtUnix = Math.Min(response.ExpiresAtUnix, claims.ExpiresAtUnix)
        });
        _store.Write(state, deviceKey);
        return released;
    }

    public EntitlementClaims RequireValid()
    {
        try
        {
            using var deviceKey = _keys.OpenOrCreate();
            var state = _store.Read(deviceKey) ?? throw new LicenseOperationException("license_missing", "License is not activated.");
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (now + LicensingProtocol.MaximumClockSkewMinutes * 60L < state.LastSeenUnix)
                throw new LicenseOperationException("clock_rollback", "System clock moved backwards.");
            EntitlementClaims claims;
            try { claims = ValidateToken(state.EntitlementToken, deviceKey); }
            catch (LicenseOperationException exception) when (exception.Code == "token_expired")
            {
                try { return Renew(); }
                catch (LicenseOperationException) { throw exception; }
            }
            if (now > state.LastSeenUnix)
            {
                state.LastSeenUnix = now;
                _store.Write(state, deviceKey);
            }
            if (claims.ExpiresAtUnix - now <= LicensingProtocol.RenewalThresholdHours * 60L * 60L &&
                now - _lastRenewalAttemptUnix >= 15L * 60L)
            {
                _lastRenewalAttemptUnix = now;
                try { return Renew(); }
                catch (LicenseOperationException) { /* Keep the still-valid offline entitlement. */ }
            }
            return claims;
        }
        catch (LicenseOperationException) { throw; }
        catch (Exception exception) when (exception is CryptographicException || exception is System.Runtime.Serialization.SerializationException || exception is IOException)
        {
            throw new LicenseOperationException("license_state_invalid", "Local license state is invalid.");
        }
    }

    public string StatusCode()
    {
        try { RequireValid(); return "valid"; }
        catch (LicenseOperationException exception) { return exception.Code; }
    }

    private static EntitlementClaims ValidateToken(string token, ECDsa deviceKey)
    {
        using var serverKey = LicenseTrust.CreateVerificationKey();
        var result = LicenseCrypto.ValidateEntitlement(token, serverKey, LicensingProtocol.ProductId,
            DeviceKeyStore.Fingerprint(deviceKey), DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (!result.Success || result.Claims == null)
            throw new LicenseOperationException(result.ErrorCode, "Entitlement is not valid.");
        if (!string.Equals(result.Claims.KeyId, LicenseTrust.KeyId, StringComparison.Ordinal))
            throw new LicenseOperationException("token_key_unknown", "Entitlement signing key is unknown.");
        return result.Claims;
    }

    private void Save(EntitlementResponse response, long lastSeenUnix, ECDsa deviceKey, System.Collections.Generic.List<ProtectedCatalogKey>? catalogKeys = null) => _store.Write(new LocalLicenseState
    {
        ActivationId = response.ActivationId,
        EntitlementToken = response.EntitlementToken,
        LastSeenUnix = lastSeenUnix,
        CatalogKeys = catalogKeys ?? new System.Collections.Generic.List<ProtectedCatalogKey>()
    }, deviceKey);
}
