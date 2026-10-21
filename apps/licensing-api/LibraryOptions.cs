namespace Atlas.Licensing.Api;

public sealed class LibraryOptions
{
    public string StoragePath { get; init; } = string.Empty;
    public long MaximumUploadBytes { get; init; }

    public static LibraryOptions FromEnvironment(string contentRoot)
    {
        var path = Environment.GetEnvironmentVariable("ATLAS_LIBRARY_STORAGE_PATH") ??
            Path.Combine(contentRoot, "App_Data", "libraries");
        var rawLimit = Environment.GetEnvironmentVariable("ATLAS_LIBRARY_MAX_UPLOAD_BYTES");
        var limit = long.TryParse(rawLimit, out var parsed) && parsed > 0 ? parsed : 512L * 1024 * 1024;
        return new LibraryOptions { StoragePath = Path.GetFullPath(path), MaximumUploadBytes = limit };
    }
}
