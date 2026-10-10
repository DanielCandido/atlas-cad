namespace Atlas.Licensing.Api;

public sealed class LicensingServerOptions
{
    public string StatePath { get; private set; } = string.Empty;
    public string SigningKeyPath { get; private set; } = string.Empty;
    public string AdminApiKey { get; private set; } = string.Empty;
    public string CatalogKeysPath { get; private set; } = string.Empty;

    public static LicensingServerOptions FromEnvironment(string contentRoot)
    {
        var statePath = Environment.GetEnvironmentVariable("ATLAS_LICENSE_STATE_PATH");
        var signingPath = Environment.GetEnvironmentVariable("ATLAS_LICENSE_SIGNING_KEY_PATH");
        var adminKey = Environment.GetEnvironmentVariable("ATLAS_LICENSE_ADMIN_KEY");
        var catalogKeysPath = Environment.GetEnvironmentVariable("ATLAS_CATALOG_KEYS_PATH");
        if (string.IsNullOrWhiteSpace(signingPath))
            throw new InvalidOperationException("ATLAS_LICENSE_SIGNING_KEY_PATH is required.");
        if (string.IsNullOrWhiteSpace(adminKey) || adminKey.Length < 24)
            throw new InvalidOperationException("ATLAS_LICENSE_ADMIN_KEY must contain at least 24 characters.");
        if (string.IsNullOrWhiteSpace(catalogKeysPath))
            throw new InvalidOperationException("ATLAS_CATALOG_KEYS_PATH is required.");
        return new LicensingServerOptions
        {
            StatePath = Path.GetFullPath(string.IsNullOrWhiteSpace(statePath)
                ? Path.Combine(contentRoot, "App_Data", "licensing-state.json")
                : statePath),
            SigningKeyPath = Path.GetFullPath(signingPath),
            AdminApiKey = adminKey,
            CatalogKeysPath = Path.GetFullPath(catalogKeysPath)
        };
    }
}
