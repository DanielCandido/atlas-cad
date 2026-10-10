using System.Security.Cryptography;
using System.Text.Json;
using Atlas.Licensing;

namespace Atlas.Licensing.Api;

public sealed class CatalogKeyProvider
{
    private readonly Dictionary<string, byte[]> _keys;

    public CatalogKeyProvider(LicensingServerOptions options)
    {
        var file = new FileInfo(options.CatalogKeysPath);
        if (!file.Exists || file.Length <= 0 || file.Length > 1024 * 1024)
            throw new InvalidDataException("Catalog key ring file is missing or invalid.");
        var ring = JsonSerializer.Deserialize<CatalogKeyRing>(File.ReadAllText(file.FullName),
            new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true }) ??
            throw new InvalidDataException("Catalog key ring is empty.");
        if (ring.Version != 1 || ring.Keys == null || ring.Keys.Count == 0)
            throw new InvalidDataException("Catalog key ring is invalid.");
        _keys = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in ring.Keys)
        {
            if (string.IsNullOrWhiteSpace(entry.KeyId) || !_keys.TryAdd(entry.KeyId, LicenseCrypto.Base64UrlDecode(entry.KeyMaterial)))
                throw new InvalidDataException("Catalog key ring contains an invalid or duplicate key ID.");
            if (_keys[entry.KeyId].Length != 32) throw new CryptographicException("Catalog keys must contain 32 bytes.");
        }
    }

    public CatalogKeyResponse Release(string keyId, long expiresAtUnix)
    {
        if (!_keys.TryGetValue(keyId, out var key))
            throw new LicenseServerException("catalog_key_not_found", "Catalog key is not available.", 404);
        return new CatalogKeyResponse
        {
            ProtocolVersion = LicensingProtocol.CurrentVersion,
            KeyId = keyId,
            KeyMaterial = LicenseCrypto.Base64UrlEncode(key),
            ExpiresAtUnix = expiresAtUnix
        };
    }
}
