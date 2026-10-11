using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Licensing;

const string keyName = "AtlasCad.Device.v1";
try
{
    using var key = OpenOrCreateKey(keyName);
    var parameters = key.ExportParameters(false);
    var x = LicenseCrypto.Base64UrlEncode(parameters.Q.X!);
    var y = LicenseCrypto.Base64UrlEncode(parameters.Q.Y!);
    var nonce = LicenseCrypto.Base64UrlEncode(RandomNumberGenerator.GetBytes(18));
    var canonical = Encoding.UTF8.GetBytes($"ATLAS-DEVICE|1|{x}|{y}|{nonce}");
    var proof = LicenseCrypto.Base64UrlEncode(key.SignData(canonical, HashAlgorithmName.SHA256));
    var payload = JsonSerializer.SerializeToUtf8Bytes(new { v = 1, x, y, n = nonce, p = proof });
    var identifier = "ATLAS1." + LicenseCrypto.Base64UrlEncode(payload);
    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    var output = Path.Combine(desktop, "AtlasCad-Identificador.txt");
    await File.WriteAllTextAsync(output, identifier + Environment.NewLine, new UTF8Encoding(false));

    Console.WriteLine("ATLAS CAD - Identificador da maquina");
    Console.WriteLine();
    Console.WriteLine("O identificador foi salvo em:");
    Console.WriteLine(output);
    Console.WriteLine();
    Console.WriteLine(identifier);
    Console.WriteLine();
    Console.WriteLine("Cole esse identificador na Area do Cliente. Ele nao contem serial de hardware nem dados pessoais.");
    Console.WriteLine("Pressione Enter para fechar.");
    Console.ReadLine();
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine("Nao foi possivel gerar o identificador: " + exception.Message);
    Console.Error.WriteLine("Pressione Enter para fechar.");
    Console.ReadLine();
    return 1;
}

static ECDsaCng OpenOrCreateKey(string keyName)
{
    CngKey key;
    if (CngKey.Exists(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.UserKey))
        key = CngKey.Open(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider, CngKeyOpenOptions.UserKey);
    else
    {
        var creation = new CngKeyCreationParameters
        {
            Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
            ExportPolicy = CngExportPolicies.None,
            KeyUsage = CngKeyUsages.Signing
        };
        creation.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(256), CngPropertyOptions.None));
        key = CngKey.Create(CngAlgorithm.ECDsaP256, keyName, creation);
    }
    return new ECDsaCng(key) { HashAlgorithm = CngAlgorithm.Sha256 };
}
