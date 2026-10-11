using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using Atlas.Licensing;

namespace Atlas.AutoCAD.Adapter;

internal static class LicenseTrust
{
    private static readonly SigningKeyResource Descriptor = Load();
    public static string KeyId => Descriptor.KeyId;
    public static ECDsa CreateVerificationKey() => ECDsa.Create(LicenseCrypto.PublicParameters(Descriptor.X, Descriptor.Y));

    private static SigningKeyResource Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Atlas.SigningPublicKey.json") ??
            throw new InvalidOperationException("Embedded ATLAS signing public key was not found.");
        var serializer = new DataContractJsonSerializer(typeof(SigningKeyResource));
        var value = serializer.ReadObject(stream) as SigningKeyResource ?? throw new InvalidDataException("Signing public key is empty.");
        if (string.IsNullOrWhiteSpace(value.KeyId) || string.IsNullOrWhiteSpace(value.X) || string.IsNullOrWhiteSpace(value.Y))
            throw new InvalidDataException("Signing public key is invalid.");
        return value;
    }

    [DataContract]
    private sealed class SigningKeyResource
    {
        [DataMember(Name = "keyId", IsRequired = true)] public string KeyId { get; set; } = string.Empty;
        [DataMember(Name = "x", IsRequired = true)] public string X { get; set; } = string.Empty;
        [DataMember(Name = "y", IsRequired = true)] public string Y { get; set; } = string.Empty;
    }
}
