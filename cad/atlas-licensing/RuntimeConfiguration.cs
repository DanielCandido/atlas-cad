using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Atlas.AutoCAD.Adapter;

internal static class RuntimeConfiguration
{
    public static string LicenseApiBaseUrl { get; } = Load();

    private static string Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Atlas.RuntimeConfiguration.json") ??
            throw new InvalidOperationException("Embedded ATLAS runtime configuration was not found.");
        var serializer = new DataContractJsonSerializer(typeof(RuntimeResource));
        var value = serializer.ReadObject(stream) as RuntimeResource;
        if (value == null || !Uri.TryCreate(value.LicenseApiBaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("Production license API URL must use HTTPS.");
        return value.LicenseApiBaseUrl.TrimEnd('/') + "/";
    }

    [DataContract]
    private sealed class RuntimeResource
    {
        [DataMember(Name = "licenseApiBaseUrl", IsRequired = true)] public string LicenseApiBaseUrl { get; set; } = string.Empty;
    }
}
