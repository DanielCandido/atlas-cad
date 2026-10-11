namespace Atlas.Licensing.Api;

public sealed class LicensingServerOptions
{
    public string SigningKeyPath { get; private set; } = string.Empty;
    public string AdminApiKey { get; private set; } = string.Empty;
    public string CatalogKeysPath { get; private set; } = string.Empty;
    public string SigningKeyId { get; private set; } = string.Empty;

    public static LicensingServerOptions FromEnvironment(string contentRoot)
    {
        var signingPath = Environment.GetEnvironmentVariable("ATLAS_LICENSE_SIGNING_KEY_PATH");
        var adminKey = Environment.GetEnvironmentVariable("ATLAS_LICENSE_ADMIN_KEY");
        var catalogKeysPath = Environment.GetEnvironmentVariable("ATLAS_CATALOG_KEYS_PATH");
        var signingKeyId = Environment.GetEnvironmentVariable("ATLAS_LICENSE_SIGNING_KEY_ID");
        if (string.IsNullOrWhiteSpace(signingPath))
            throw new InvalidOperationException("ATLAS_LICENSE_SIGNING_KEY_PATH is required.");
        if (string.IsNullOrWhiteSpace(adminKey) || adminKey.Length < 24)
            throw new InvalidOperationException("ATLAS_LICENSE_ADMIN_KEY must contain at least 24 characters.");
        if (string.IsNullOrWhiteSpace(catalogKeysPath))
            throw new InvalidOperationException("ATLAS_CATALOG_KEYS_PATH is required.");
        if (string.IsNullOrWhiteSpace(signingKeyId) || signingKeyId.Length > 80)
            throw new InvalidOperationException("ATLAS_LICENSE_SIGNING_KEY_ID is required (max 80 characters).");
        return new LicensingServerOptions
        {
            SigningKeyPath = Path.GetFullPath(signingPath),
            AdminApiKey = adminKey,
            CatalogKeysPath = Path.GetFullPath(catalogKeysPath),
            SigningKeyId = signingKeyId
        };
    }
}
