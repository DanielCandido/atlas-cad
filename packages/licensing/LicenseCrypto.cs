using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Licensing;

public static class LicenseCrypto
{
    public static string IssueEntitlement(EntitlementClaims claims, ECDsa signingKey)
    {
        if (claims == null) throw new ArgumentNullException(nameof(claims));
        if (signingKey == null) throw new ArgumentNullException(nameof(signingKey));
        ValidateClaimsShape(claims);

        var payload = Serialize(claims, typeof(EntitlementClaims));
        var encodedPayload = Base64UrlEncode(payload);
        var signedText = LicensingProtocol.TokenPrefix + "." + encodedPayload;
        var signature = signingKey.SignData(Encoding.ASCII.GetBytes(signedText), HashAlgorithmName.SHA256);
        return signedText + "." + Base64UrlEncode(signature);
    }

    public static EntitlementValidationResult ValidateEntitlement(
        string? token,
        ECDsa verificationKey,
        string expectedProduct,
        string expectedDeviceKeySha256,
        long nowUnix)
    {
        if (string.IsNullOrWhiteSpace(token)) return EntitlementValidationResult.Invalid("license_missing");
        if (verificationKey == null) throw new ArgumentNullException(nameof(verificationKey));
        var parts = token.Split('.');
        if (parts.Length != 3 || !string.Equals(parts[0], LicensingProtocol.TokenPrefix, StringComparison.Ordinal))
            return EntitlementValidationResult.Invalid("token_format_invalid");
        try
        {
            var signedText = parts[0] + "." + parts[1];
            var signature = Base64UrlDecode(parts[2]);
            if (!verificationKey.VerifyData(Encoding.ASCII.GetBytes(signedText), signature, HashAlgorithmName.SHA256))
                return EntitlementValidationResult.Invalid("token_signature_invalid");

            var claims = (EntitlementClaims)Deserialize(Base64UrlDecode(parts[1]), typeof(EntitlementClaims));
            ValidateClaimsShape(claims);
            if (claims.ProtocolVersion != LicensingProtocol.CurrentVersion)
                return EntitlementValidationResult.Invalid("token_protocol_unsupported");
            if (!string.Equals(claims.Issuer, LicensingProtocol.Issuer, StringComparison.Ordinal) ||
                !string.Equals(claims.ProductId, expectedProduct, StringComparison.Ordinal))
                return EntitlementValidationResult.Invalid("token_product_invalid");
            if (!string.Equals(claims.DeviceKeySha256, expectedDeviceKeySha256, StringComparison.Ordinal))
                return EntitlementValidationResult.Invalid("token_device_mismatch");
            var skew = LicensingProtocol.MaximumClockSkewMinutes * 60L;
            if (claims.IssuedAtUnix > nowUnix + skew)
                return EntitlementValidationResult.Invalid("token_not_yet_valid");
            if (claims.ExpiresAtUnix <= nowUnix)
                return EntitlementValidationResult.Invalid("token_expired");
            if (!claims.Modules.Contains("mline"))
                return EntitlementValidationResult.Invalid("token_module_missing");
            return EntitlementValidationResult.Valid(claims);
        }
        catch (Exception exception) when (
            exception is FormatException ||
            exception is SerializationException ||
            exception is CryptographicException ||
            exception is InvalidDataException)
        {
            return EntitlementValidationResult.Invalid("token_invalid");
        }
    }

    public static byte[] ActivationProofBytes(ActivationRequest request) => Encoding.UTF8.GetBytes(
        "activate\n" + request.ProtocolVersion + "\n" + request.ActivationCode.Trim().ToUpperInvariant() +
        "\n" + request.DevicePublicKeyX + "\n" + request.DevicePublicKeyY);

    public static byte[] RenewalProofBytes(RenewalRequest request) => Encoding.UTF8.GetBytes(
        "renew\n" + request.ProtocolVersion + "\n" + request.ActivationId + "\n" + request.Nonce);

    public static byte[] CatalogKeyProofBytes(CatalogKeyRequest request) => Encoding.UTF8.GetBytes(
        "catalog-key\n" + request.ProtocolVersion + "\n" + request.ActivationId + "\n" + request.KeyId + "\n" + request.Nonce);

    public static byte[] InstallerPairingCreateProofBytes(InstallerPairingCreateRequest request) => Encoding.UTF8.GetBytes(
        "installer-pairing-create\n" + request.ProtocolVersion + "\n" + request.DevicePublicKeyX + "\n" +
        request.DevicePublicKeyY + "\n" + request.Nonce + "\n" + string.Join("|", request.DetectedVersions));

    public static byte[] InstallerPairingProofBytes(string operation, string pairingId, InstallerPairingProofRequest request) => Encoding.UTF8.GetBytes(
        "installer-pairing-" + operation + "\n" + request.ProtocolVersion + "\n" + pairingId + "\n" + request.Nonce);

    public static ECParameters PublicParameters(string x, string y) => new ECParameters
    {
        Curve = ECCurve.NamedCurves.nistP256,
        Q = new ECPoint { X = Base64UrlDecode(x), Y = Base64UrlDecode(y) }
    };

    public static string DeviceFingerprint(ECParameters publicParameters)
    {
        if (publicParameters.Q.X == null || publicParameters.Q.Y == null ||
            publicParameters.Q.X.Length != 32 || publicParameters.Q.Y.Length != 32)
            throw new CryptographicException("Device key must be ECDSA P-256.");
        var value = new byte[64];
        Buffer.BlockCopy(publicParameters.Q.X, 0, value, 0, 32);
        Buffer.BlockCopy(publicParameters.Q.Y, 0, value, 32, 32);
        using var sha = SHA256.Create();
        return Base64UrlEncode(sha.ComputeHash(value));
    }

    public static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        switch (normalized.Length % 4)
        {
            case 2: normalized += "=="; break;
            case 3: normalized += "="; break;
            case 1: throw new FormatException("Invalid Base64Url value.");
        }
        return Convert.FromBase64String(normalized);
    }

    public static byte[] Serialize(object value, Type type)
    {
        using var stream = new MemoryStream();
        new DataContractJsonSerializer(type).WriteObject(stream, value);
        return stream.ToArray();
    }

    public static object Deserialize(byte[] value, Type type)
    {
        using var stream = new MemoryStream(value, writable: false);
        return new DataContractJsonSerializer(type).ReadObject(stream) ??
            throw new SerializationException("JSON payload is empty.");
    }

    private static void ValidateClaimsShape(EntitlementClaims claims)
    {
        if (claims.ProtocolVersion <= 0 || claims.Generation <= 0 ||
            string.IsNullOrWhiteSpace(claims.Issuer) || string.IsNullOrWhiteSpace(claims.KeyId) ||
            string.IsNullOrWhiteSpace(claims.ProductId) || string.IsNullOrWhiteSpace(claims.LicenseId) ||
            string.IsNullOrWhiteSpace(claims.ActivationId) || string.IsNullOrWhiteSpace(claims.DeviceKeySha256) ||
            claims.ExpiresAtUnix <= claims.IssuedAtUnix || claims.Modules == null || claims.Modules.Count == 0)
            throw new SerializationException("Entitlement claims are invalid.");
    }
}
