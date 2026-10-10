using System.Security.Cryptography;
using Atlas.Licensing;

namespace Atlas.Licensing.Api;

public sealed class SigningKeyProvider : IDisposable
{
    private readonly ECDsa _key;

    public SigningKeyProvider(LicensingServerOptions options)
    {
        var bytes = File.ReadAllBytes(options.SigningKeyPath);
        _key = ECDsa.Create();
        _key.ImportPkcs8PrivateKey(bytes, out var read);
        if (read != bytes.Length) throw new CryptographicException("Signing key contains trailing data.");
        var parameters = _key.ExportParameters(false);
        PublicDescriptor = new SigningPublicKeyDescriptor
        {
            ProtocolVersion = LicensingProtocol.CurrentVersion,
            KeyId = LicensingProtocol.DevelopmentKeyId,
            Curve = "P-256",
            X = LicenseCrypto.Base64UrlEncode(parameters.Q.X!),
            Y = LicenseCrypto.Base64UrlEncode(parameters.Q.Y!)
        };
    }

    public SigningPublicKeyDescriptor PublicDescriptor { get; }

    public EntitlementResponse Issue(ActivationSnapshot activation)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var claims = new EntitlementClaims
        {
            ProtocolVersion = LicensingProtocol.CurrentVersion,
            Issuer = LicensingProtocol.Issuer,
            KeyId = LicensingProtocol.DevelopmentKeyId,
            ProductId = activation.ProductId,
            LicenseId = activation.LicenseId,
            ActivationId = activation.ActivationId,
            Generation = activation.Generation,
            DeviceKeySha256 = activation.DeviceKeySha256,
            IssuedAtUnix = now,
            ExpiresAtUnix = now + LicensingProtocol.OfflineHours * 60L * 60L,
            Modules = new List<string> { "mline" }
        };
        return new EntitlementResponse
        {
            ProtocolVersion = LicensingProtocol.CurrentVersion,
            ActivationId = activation.ActivationId,
            EntitlementToken = LicenseCrypto.IssueEntitlement(claims, _key),
            ExpiresAtUnix = claims.ExpiresAtUnix
        };
    }

    public void Dispose() => _key.Dispose();
}

public sealed class SigningPublicKeyDescriptor
{
    public int ProtocolVersion { get; set; }
    public string KeyId { get; set; } = string.Empty;
    public string Curve { get; set; } = string.Empty;
    public string X { get; set; } = string.Empty;
    public string Y { get; set; } = string.Empty;
}
