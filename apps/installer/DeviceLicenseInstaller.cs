using System.Runtime.Serialization;
using System.Security.Cryptography;
using Atlas.Licensing;

namespace Atlas.Installer;

internal sealed class DeviceLicenseInstaller : IDisposable
{
    private readonly InstallerSettings settings;
    private readonly InstallerApiClient api;
    private readonly ECDsaCng deviceKey;

    public DeviceLicenseInstaller(InstallerSettings settings)
    {
        this.settings = settings;
        api = new InstallerApiClient(settings.LicenseApiBaseUrl);
        deviceKey = OpenOrCreateKey();
    }

    public async Task<InstallerPairingCreateResponse> CreatePairingAsync(IReadOnlyList<string> versions, CancellationToken cancellationToken)
    {
        var parameters = deviceKey.ExportParameters(false);
        var request = new InstallerPairingCreateRequest
        {
            ProtocolVersion = LicensingProtocol.CurrentVersion,
            DevicePublicKeyX = LicenseCrypto.Base64UrlEncode(parameters.Q.X!),
            DevicePublicKeyY = LicenseCrypto.Base64UrlEncode(parameters.Q.Y!),
            Nonce = NewNonce(),
            DetectedVersions = versions.ToList(),
            LibrarySlug = settings.LibrarySlug
        };
        request.Proof = Sign(LicenseCrypto.InstallerPairingCreateProofBytes(request));
        return await api.CreatePairingAsync(request, cancellationToken);
    }

    public async Task<InstallerPairingStatusResponse> PollAsync(string pairingId, CancellationToken cancellationToken)
    {
        var request = Proof("poll", pairingId);
        return await api.PollAsync(pairingId, request, cancellationToken);
    }

    public async Task CompleteAsync(string pairingId, string catalogSource, CancellationToken cancellationToken)
    {
        var response = await api.CompleteAsync(pairingId, Proof("complete", pairingId), cancellationToken);
        var fingerprint = LicenseCrypto.DeviceFingerprint(deviceKey.ExportParameters(false));
        if (string.IsNullOrWhiteSpace(settings.SigningKeyX) || string.IsNullOrWhiteSpace(settings.SigningKeyY))
            throw new InstallerException("installer_trust_missing", "O instalador não contém a chave pública de produção.");
        using var serverKey = ECDsa.Create(LicenseCrypto.PublicParameters(settings.SigningKeyX, settings.SigningKeyY));
        var validation = LicenseCrypto.ValidateEntitlement(response.EntitlementToken, serverKey, settings.ProductId,
            fingerprint, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        if (!validation.Success || validation.Claims == null || validation.Claims.KeyId != settings.SigningKeyId)
            throw new InstallerException(validation.ErrorCode, "A licença retornada pelo servidor é inválida.");

        CatalogPackageHeader header;
        using (var stream = File.OpenRead(catalogSource)) header = CatalogPackage.ReadHeader(stream);
        var keyRequest = new CatalogKeyRequest
        {
            ProtocolVersion = LicensingProtocol.CurrentVersion,
            ActivationId = response.ActivationId,
            KeyId = header.KeyId,
            Nonce = NewNonce()
        };
        keyRequest.Proof = Sign(LicenseCrypto.CatalogKeyProofBytes(keyRequest));
        var keyResponse = await api.GetCatalogKeyAsync(keyRequest, cancellationToken);
        if (keyResponse.KeyId != header.KeyId || keyResponse.ExpiresAtUnix <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            throw new InstallerException("catalog_key_response_invalid", "A chave do catálogo retornada é inválida.");
        var catalogKey = LicenseCrypto.Base64UrlDecode(keyResponse.KeyMaterial);
        byte[]? plaintext = null;
        try
        {
            using var stream = File.OpenRead(catalogSource);
            plaintext = CatalogPackage.Decrypt(stream, catalogKey);
            if (plaintext.Length == 0) throw new InvalidDataException("Catálogo vazio.");
            InstallCatalog(catalogSource);
            WriteState(new LocalLicenseState
            {
                ActivationId = response.ActivationId,
                EntitlementToken = response.EntitlementToken,
                LastSeenUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                CatalogKeys = new List<ProtectedCatalogKey>
                {
                    new()
                    {
                        KeyId = header.KeyId,
                        ProtectedKey = LicenseCrypto.Base64UrlEncode(WindowsDataProtection.Protect(catalogKey)),
                        ExpiresAtUnix = Math.Min(keyResponse.ExpiresAtUnix, response.ExpiresAtUnix)
                    }
                }
            });
        }
        finally
        {
            if (plaintext != null) CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(catalogKey);
        }
    }

    private InstallerPairingProofRequest Proof(string operation, string pairingId)
    {
        var request = new InstallerPairingProofRequest { ProtocolVersion = LicensingProtocol.CurrentVersion, Nonce = NewNonce() };
        request.Proof = Sign(LicenseCrypto.InstallerPairingProofBytes(operation, pairingId, request));
        return request;
    }

    private void WriteState(LocalLicenseState state)
    {
        state.StateSignature = Sign(StateBytes(state));
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtlasCad", "license", "v1");
        SecureDirectory.Create(directory);
        var path = Path.Combine(directory, "state.json");
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, LicenseCrypto.Serialize(state, typeof(LocalLicenseState)));
        File.Move(temporary, path, true);
    }

    private static void InstallCatalog(string source)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtlasCad", "catalog", "v2");
        SecureDirectory.Create(directory);
        var target = Path.Combine(directory, "steelflow.atlascatalog");
        var temporary = target + ".tmp";
        File.Copy(source, temporary, true);
        File.Move(temporary, target, true);
        var legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtlasCad", "catalog", "v1", "steel.atlascatalog.json");
        if (File.Exists(legacy)) File.Delete(legacy);
    }

    private static byte[] StateBytes(LocalLicenseState state)
    {
        var canonical = "state\n" + state.ActivationId + "\n" + state.EntitlementToken + "\n" + state.LastSeenUnix;
        if (state.CatalogKeys.Count > 0)
        {
            canonical += "\nkeys";
            foreach (var key in state.CatalogKeys.OrderBy(item => item.KeyId, StringComparer.Ordinal))
                canonical += "\n" + key.KeyId + "\n" + key.ProtectedKey + "\n" + key.ExpiresAtUnix;
        }
        return System.Text.Encoding.UTF8.GetBytes(canonical);
    }

    private string Sign(byte[] value) => LicenseCrypto.Base64UrlEncode(deviceKey.SignData(value, HashAlgorithmName.SHA256));
    private static string NewNonce() => LicenseCrypto.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));

    private static ECDsaCng OpenOrCreateKey()
    {
        const string name = "AtlasCad.Device.v1";
        CngKey key;
        if (CngKey.Exists(name, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.UserKey))
            key = CngKey.Open(name, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.UserKey);
        else
        {
            var creation = new CngKeyCreationParameters
            {
                Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
                ExportPolicy = CngExportPolicies.None,
                KeyUsage = CngKeyUsages.Signing
            };
            creation.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(256), CngPropertyOptions.None));
            key = CngKey.Create(CngAlgorithm.ECDsaP256, name, creation);
        }
        return new ECDsaCng(key) { HashAlgorithm = CngAlgorithm.Sha256 };
    }

    public void Dispose() { deviceKey.Dispose(); api.Dispose(); }
}

[DataContract]
internal sealed class LocalLicenseState
{
    [DataMember(Name = "activationId", Order = 1)] public string ActivationId { get; set; } = string.Empty;
    [DataMember(Name = "entitlementToken", Order = 2)] public string EntitlementToken { get; set; } = string.Empty;
    [DataMember(Name = "lastSeenUnix", Order = 3)] public long LastSeenUnix { get; set; }
    [DataMember(Name = "stateSignature", Order = 4)] public string StateSignature { get; set; } = string.Empty;
    [DataMember(Name = "catalogKeys", Order = 5)] public List<ProtectedCatalogKey> CatalogKeys { get; set; } = new();
}

[DataContract]
internal sealed class ProtectedCatalogKey
{
    [DataMember(Name = "keyId", Order = 1)] public string KeyId { get; set; } = string.Empty;
    [DataMember(Name = "protectedKey", Order = 2)] public string ProtectedKey { get; set; } = string.Empty;
    [DataMember(Name = "expiresAtUnix", Order = 3)] public long ExpiresAtUnix { get; set; }
}
