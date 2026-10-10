using System.Security.Cryptography;
using Atlas.Licensing;

using var server = ECDsa.Create(ECCurve.NamedCurves.nistP256);
using var device = ECDsa.Create(ECCurve.NamedCurves.nistP256);
using var otherDevice = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
var claims = Claims(now, LicenseCrypto.DeviceFingerprint(device.ExportParameters(false)));
var token = LicenseCrypto.IssueEntitlement(claims, server);

Assert("valid", LicenseCrypto.ValidateEntitlement(token, server, LicensingProtocol.ProductId, claims.DeviceKeySha256, now).Success);
Assert("tampered", !LicenseCrypto.ValidateEntitlement(token + "x", server, LicensingProtocol.ProductId, claims.DeviceKeySha256, now).Success);
Assert("copied-device", !LicenseCrypto.ValidateEntitlement(token, server, LicensingProtocol.ProductId,
    LicenseCrypto.DeviceFingerprint(otherDevice.ExportParameters(false)), now).Success);
Assert("wrong-product", !LicenseCrypto.ValidateEntitlement(token, server, "other-product", claims.DeviceKeySha256, now).Success);
var expired = Claims(now - 7200, claims.DeviceKeySha256);
expired.ExpiresAtUnix = now - 1;
var expiredToken = LicenseCrypto.IssueEntitlement(expired, server);
Assert("expired", LicenseCrypto.ValidateEntitlement(expiredToken, server, LicensingProtocol.ProductId, claims.DeviceKeySha256, now).ErrorCode == "token_expired");
var catalogPlaintext = System.Text.Encoding.UTF8.GetBytes("private-style:W150X13:offsets");
var catalogKey = RandomNumberGenerator.GetBytes(32);
using var package = new MemoryStream();
CatalogPackage.Protect(package, catalogPlaintext, catalogKey, "test-key-1", 7);
var packageBytes = package.ToArray();
using var headerStream = new MemoryStream(packageBytes, writable: false);
var packageHeader = CatalogPackage.ReadHeader(headerStream);
Assert("catalog-header", packageHeader.CatalogVersion == 7 && packageHeader.KeyId == "test-key-1");
using var decryptStream = new MemoryStream(packageBytes, writable: false);
Assert("catalog-roundtrip", CatalogPackage.Decrypt(decryptStream, catalogKey).SequenceEqual(catalogPlaintext));
Assert("catalog-confidentiality", !System.Text.Encoding.Latin1.GetString(packageBytes).Contains("W150X13", StringComparison.Ordinal));
var tampered = (byte[])packageBytes.Clone(); tampered[tampered.Length - 1] ^= 0x01;
Assert("catalog-tamper", RejectsCatalog(tampered, catalogKey));
Assert("catalog-wrong-key", RejectsCatalog(packageBytes, RandomNumberGenerator.GetBytes(32)));
Console.WriteLine("Atlas licensing/catalog smoke: 10/10 passed.");
return;

static EntitlementClaims Claims(long now, string fingerprint) => new()
{
    ProtocolVersion = LicensingProtocol.CurrentVersion,
    Issuer = LicensingProtocol.Issuer,
    KeyId = LicensingProtocol.DevelopmentKeyId,
    ProductId = LicensingProtocol.ProductId,
    LicenseId = "lic_test",
    ActivationId = "act_test",
    Generation = 1,
    DeviceKeySha256 = fingerprint,
    IssuedAtUnix = now,
    ExpiresAtUnix = now + 3600,
    Modules = new List<string> { "mline" }
};

static void Assert(string name, bool condition)
{
    if (!condition) throw new InvalidOperationException("Failed: " + name);
    Console.WriteLine("[OK] " + name);
}

static bool RejectsCatalog(byte[] package, byte[] key)
{
    try { using var stream = new MemoryStream(package, writable: false); CatalogPackage.Decrypt(stream, key); return false; }
    catch (InvalidDataException) { return true; }
}
