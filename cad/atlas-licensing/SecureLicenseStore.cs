using System;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using Atlas.Licensing;
using System.Collections.Generic;
using System.Linq;

namespace Atlas.AutoCAD.Adapter;

[DataContract]
internal sealed class LocalLicenseState
{
    [DataMember(Name = "activationId", Order = 1)] public string ActivationId { get; set; } = string.Empty;
    [DataMember(Name = "entitlementToken", Order = 2)] public string EntitlementToken { get; set; } = string.Empty;
    [DataMember(Name = "lastSeenUnix", Order = 3)] public long LastSeenUnix { get; set; }
    [DataMember(Name = "stateSignature", Order = 4)] public string StateSignature { get; set; } = string.Empty;
    [DataMember(Name = "catalogKeys", Order = 5)] public List<ProtectedCatalogKey> CatalogKeys { get; set; } = new List<ProtectedCatalogKey>();
}

[DataContract]
internal sealed class ProtectedCatalogKey
{
    [DataMember(Name = "keyId", Order = 1)] public string KeyId { get; set; } = string.Empty;
    [DataMember(Name = "protectedKey", Order = 2)] public string ProtectedKey { get; set; } = string.Empty;
    [DataMember(Name = "expiresAtUnix", Order = 3)] public long ExpiresAtUnix { get; set; }
}

internal sealed class SecureLicenseStore
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtlasCad", "license", "v1", "state.json");

    public LocalLicenseState? Read(ECDsa deviceKey)
    {
        if (!File.Exists(_path)) return null;
        var bytes = File.ReadAllBytes(_path);
        if (bytes.Length == 0 || bytes.Length > 256 * 1024) throw new CryptographicException("License state size is invalid.");
        var state = (LocalLicenseState)LicenseCrypto.Deserialize(bytes, typeof(LocalLicenseState));
        byte[] signature;
        try { signature = LicenseCrypto.Base64UrlDecode(state.StateSignature); }
        catch (FormatException) { throw new CryptographicException("License state signature is invalid."); }
        if (!deviceKey.VerifyData(StateBytes(state), signature, HashAlgorithmName.SHA256))
            throw new CryptographicException("License state was modified or copied.");
        if (state.CatalogKeys == null) state.CatalogKeys = new List<ProtectedCatalogKey>();
        return state;
    }

    public void Write(LocalLicenseState state, ECDsa deviceKey)
    {
        state.StateSignature = LicenseCrypto.Base64UrlEncode(deviceKey.SignData(StateBytes(state), HashAlgorithmName.SHA256));
        var directory = Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        SecureBridgeFileStore.RestrictToCurrentUser(directory);
        var temporary = _path + ".tmp";
        if (File.Exists(temporary)) File.Delete(temporary);
        try
        {
            File.WriteAllBytes(temporary, LicenseCrypto.Serialize(state, typeof(LocalLicenseState)));
#if NETFRAMEWORK
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temporary, _path);
#else
            File.Move(temporary, _path, true);
#endif
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static byte[] StateBytes(LocalLicenseState state)
    {
        var canonical = "state\n" + state.ActivationId + "\n" + state.EntitlementToken + "\n" + state.LastSeenUnix;
        if (state.CatalogKeys == null || state.CatalogKeys.Count == 0)
            return System.Text.Encoding.UTF8.GetBytes(canonical);
        canonical += "\nkeys";
        foreach (var key in state.CatalogKeys.OrderBy(item => item.KeyId, StringComparer.Ordinal))
            canonical += "\n" + key.KeyId + "\n" + key.ProtectedKey + "\n" + key.ExpiresAtUnix;
        return System.Text.Encoding.UTF8.GetBytes(canonical);
    }
}
