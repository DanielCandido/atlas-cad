using System;
using System.Security.Cryptography;
using Atlas.Licensing;

namespace Atlas.AutoCAD.Adapter;

internal sealed class DeviceKeyStore
{
    private const string KeyName = "AtlasCad.Device.v1";

    public ECDsaCng OpenOrCreate()
    {
        CngKey key;
        if (CngKey.Exists(KeyName, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.UserKey))
            key = CngKey.Open(KeyName, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.UserKey);
        else
        {
            var parameters = new CngKeyCreationParameters
            {
                Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
                KeyCreationOptions = CngKeyCreationOptions.None,
                ExportPolicy = CngExportPolicies.None,
                KeyUsage = CngKeyUsages.Signing
            };
            parameters.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(256), CngPropertyOptions.None));
            key = CngKey.Create(CngAlgorithm.ECDsaP256, KeyName, parameters);
        }
        return new ECDsaCng(key) { HashAlgorithm = CngAlgorithm.Sha256 };
    }

    public static string Fingerprint(ECDsa key) => LicenseCrypto.DeviceFingerprint(key.ExportParameters(false));
}
