using System.Reflection;
using System.Text.Json;
using Atlas.Licensing;

namespace Atlas.Installer;

internal sealed class InstallerSettings
{
    public string LicenseApiBaseUrl { get; set; } = string.Empty;
    public string SigningKeyId { get; set; } = string.Empty;
    public string SigningKeyX { get; set; } = string.Empty;
    public string SigningKeyY { get; set; } = string.Empty;
    public string LibrarySlug { get; set; } = "steelflow";
    public string ProductId { get; set; } = LicensingProtocol.ProductId;

    public static InstallerSettings Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Atlas.Installer.Settings.json") ??
            throw new InvalidOperationException("Configuração do instalador não encontrada.");
        var value = JsonSerializer.Deserialize<InstallerSettings>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ??
            throw new InvalidDataException("Configuração do instalador inválida.");
        if (!Uri.TryCreate(value.LicenseApiBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("A API de licenciamento deve usar HTTPS.");
        if (string.IsNullOrWhiteSpace(value.LibrarySlug) || string.IsNullOrWhiteSpace(value.ProductId))
            throw new InvalidDataException("A biblioteca do instalador não foi configurada.");
        return value;
    }
}
