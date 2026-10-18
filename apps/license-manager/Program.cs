using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Licensing;

return await MainAsync(args);

static async Task<int> MainAsync(string[] arguments)
{
    try
    {
        if (arguments.Length == 0) return Usage();
        return arguments[0].ToLowerInvariant() switch
        {
            "generate-signing-key" => GenerateSigningKey(arguments),
            "generate-catalog-key" => GenerateCatalogKey(arguments),
            "import-catalog-key" => ImportCatalogKey(arguments),
            "issue" => await IssueAsync(arguments),
            "transfer" => await TransferAsync(arguments),
            "create-admin" => await CreateAdminAsync(arguments),
            "simulate-device" => await SimulateDeviceAsync(arguments),
            "simulate-revocation" => await SimulateRevocationAsync(arguments),
            "simulate-catalog" => await SimulateCatalogAsync(arguments),
            _ => Usage()
        };
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine("ERROR: " + exception.Message);
        return 1;
    }
}

static int GenerateSigningKey(string[] arguments)
{
    if (arguments.Length != 4) return Usage();
    var privatePath = Path.GetFullPath(arguments[1]);
    var publicPath = Path.GetFullPath(arguments[2]);
    var keyId = arguments[3].Trim();
    if (!System.Text.RegularExpressions.Regex.IsMatch(keyId, "^[A-Za-z0-9._-]{1,80}$"))
        throw new InvalidOperationException("Signing key ID is invalid.");
    if (File.Exists(privatePath) || File.Exists(publicPath))
        throw new InvalidOperationException("Refusing to overwrite an existing signing key.");
    Directory.CreateDirectory(Path.GetDirectoryName(privatePath)!);
    Directory.CreateDirectory(Path.GetDirectoryName(publicPath)!);
    using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    File.WriteAllBytes(privatePath, key.ExportPkcs8PrivateKey());
    var parameters = key.ExportParameters(false);
    File.WriteAllText(publicPath, JsonSerializer.Serialize(new
    {
        protocolVersion = LicensingProtocol.CurrentVersion,
        keyId,
        curve = "P-256",
        x = LicenseCrypto.Base64UrlEncode(parameters.Q.X!),
        y = LicenseCrypto.Base64UrlEncode(parameters.Q.Y!)
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Private signing key: " + privatePath);
    Console.WriteLine("Public descriptor: " + publicPath);
    return 0;
}

static int GenerateCatalogKey(string[] arguments)
{
    if (arguments.Length != 3) return Usage();
    var path = Path.GetFullPath(arguments[1]);
    var keyId = arguments[2].Trim();
    if (!System.Text.RegularExpressions.Regex.IsMatch(keyId, "^[A-Za-z0-9._-]{1,80}$"))
        throw new InvalidOperationException("Catalog key ID is invalid.");
    if (File.Exists(path)) throw new InvalidOperationException("Refusing to overwrite an existing catalog key ring.");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var ring = new CatalogKeyRing
    {
        Version = 1,
        CurrentKeyId = keyId,
        Keys = new List<CatalogKeyEntry>
        {
            new() { KeyId = keyId, KeyMaterial = LicenseCrypto.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)) }
        }
    };
    File.WriteAllText(path, JsonSerializer.Serialize(ring, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    Console.WriteLine("Catalog key ring: " + path);
    Console.WriteLine("Current key ID: " + keyId);
    return 0;
}

static int ImportCatalogKey(string[] arguments)
{
    if (arguments.Length != 4) return Usage();
    var targetPath = Path.GetFullPath(arguments[1]);
    var sourcePath = Path.GetFullPath(arguments[2]);
    var keyId = arguments[3].Trim();
    if (!File.Exists(targetPath) || !File.Exists(sourcePath))
        throw new InvalidOperationException("Target or source catalog key ring was not found.");
    if (!System.Text.RegularExpressions.Regex.IsMatch(keyId, "^[A-Za-z0-9._-]{1,80}$"))
        throw new InvalidOperationException("Catalog key ID is invalid.");
    var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };
    var target = JsonSerializer.Deserialize<CatalogKeyRing>(File.ReadAllText(targetPath), jsonOptions) ??
        throw new InvalidOperationException("Target catalog key ring is empty.");
    var source = JsonSerializer.Deserialize<CatalogKeyRing>(File.ReadAllText(sourcePath), jsonOptions) ??
        throw new InvalidOperationException("Source catalog key ring is empty.");
    var sourceEntry = source.Keys.SingleOrDefault(item => string.Equals(item.KeyId, keyId, StringComparison.Ordinal)) ??
        throw new InvalidOperationException("Requested catalog key does not exist in the source key ring.");
    var existing = target.Keys.SingleOrDefault(item => string.Equals(item.KeyId, keyId, StringComparison.Ordinal));
    if (existing != null)
    {
        if (!string.Equals(existing.KeyMaterial, sourceEntry.KeyMaterial, StringComparison.Ordinal))
            throw new InvalidOperationException("Target contains the same key ID with different key material.");
        Console.WriteLine("Catalog key already available: " + keyId);
        return 0;
    }
    target.Keys.Add(new CatalogKeyEntry { KeyId = sourceEntry.KeyId, KeyMaterial = sourceEntry.KeyMaterial });
    var temporary = targetPath + ".tmp";
    try
    {
        File.WriteAllText(temporary, JsonSerializer.Serialize(target, jsonOptions));
        File.Move(temporary, targetPath, overwrite: true);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
    Console.WriteLine("Imported legacy catalog key: " + keyId);
    Console.WriteLine("Current catalog key remains: " + target.CurrentKeyId);
    return 0;
}

static async Task<int> IssueAsync(string[] arguments)
{
    if (arguments.Length != 4) return Usage();
    using var client = AdminClient(arguments[1], arguments[2]);
    using var response = await client.PostAsJsonAsync("api/v1/admin/licenses", new { customerReference = arguments[3] });
    Console.WriteLine(await ReadSuccessAsync(response));
    return 0;
}

static async Task<int> TransferAsync(string[] arguments)
{
    if (arguments.Length != 4) return Usage();
    using var client = AdminClient(arguments[1], arguments[2]);
    using var response = await client.PostAsync("api/v1/admin/licenses/" + Uri.EscapeDataString(arguments[3]) + "/transfer", null);
    Console.WriteLine(await ReadSuccessAsync(response));
    return 0;
}

static async Task<int> CreateAdminAsync(string[] arguments)
{
    if (arguments.Length != 5) return Usage();
    Console.Write("Senha (mínimo 12 caracteres): ");
    var password = ReadSecret();
    Console.WriteLine();
    Console.Write("Confirme a senha: ");
    var confirmation = ReadSecret();
    Console.WriteLine();
    if (password != confirmation) throw new InvalidOperationException("As senhas não conferem.");
    using var client = AdminClient(arguments[1], arguments[2]);
    using var response = await client.PutAsJsonAsync("api/v1/admin/operators", new
    {
        email = arguments[3],
        name = arguments[4],
        password
    });
    await ReadSuccessAsync(response);
    Console.WriteLine("Administrador criado ou atualizado: " + arguments[3]);
    return 0;
}

static string ReadSecret()
{
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) return value.ToString();
        if (key.Key == ConsoleKey.Backspace)
        {
            if (value.Length > 0) value.Length--;
            continue;
        }
        if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
    }
}

static async Task<int> SimulateDeviceAsync(string[] arguments)
{
    if (arguments.Length != 3) return Usage();
    using var device = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var publicParameters = device.ExportParameters(false);
    var activation = new ActivationRequest
    {
        ProtocolVersion = LicensingProtocol.CurrentVersion,
        ActivationCode = arguments[2],
        DevicePublicKeyX = LicenseCrypto.Base64UrlEncode(publicParameters.Q.X!),
        DevicePublicKeyY = LicenseCrypto.Base64UrlEncode(publicParameters.Q.Y!)
    };
    activation.Proof = LicenseCrypto.Base64UrlEncode(device.SignData(
        LicenseCrypto.ActivationProofBytes(activation), HashAlgorithmName.SHA256));
    using var client = ApiClient(arguments[1]);
    using var activationResponse = await client.PostAsJsonAsync("api/v1/activations", activation);
    var entitlement = JsonSerializer.Deserialize<EntitlementResponse>(await ReadSuccessAsync(activationResponse), WebJson())!;
    var descriptor = await client.GetFromJsonAsync<PublicKeyDescriptor>("api/v1/signing-key", WebJson()) ??
        throw new InvalidDataException("Signing key response is empty.");
    using var serverKey = ECDsa.Create(LicenseCrypto.PublicParameters(descriptor.X, descriptor.Y));
    var fingerprint = LicenseCrypto.DeviceFingerprint(publicParameters);
    var validation = LicenseCrypto.ValidateEntitlement(entitlement.EntitlementToken, serverKey,
        LicensingProtocol.ProductId, fingerprint, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    if (!validation.Success) throw new CryptographicException("Activation token rejected: " + validation.ErrorCode);

    var renewal = new RenewalRequest
    {
        ProtocolVersion = LicensingProtocol.CurrentVersion,
        ActivationId = entitlement.ActivationId,
        Nonce = LicenseCrypto.Base64UrlEncode(RandomNumberGenerator.GetBytes(24))
    };
    renewal.Proof = LicenseCrypto.Base64UrlEncode(device.SignData(
        LicenseCrypto.RenewalProofBytes(renewal), HashAlgorithmName.SHA256));
    using var renewalResponse = await client.PostAsJsonAsync("api/v1/renewals", renewal);
    var renewed = JsonSerializer.Deserialize<EntitlementResponse>(await ReadSuccessAsync(renewalResponse), WebJson())!;
    validation = LicenseCrypto.ValidateEntitlement(renewed.EntitlementToken, serverKey,
        LicensingProtocol.ProductId, fingerprint, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    if (!validation.Success) throw new CryptographicException("Renewed token rejected: " + validation.ErrorCode);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        status = "ok", entitlement.ActivationId, renewed.ExpiresAtUnix, deviceKeySha256 = fingerprint
    }, new JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

static async Task<int> SimulateRevocationAsync(string[] arguments)
{
    if (arguments.Length != 5) return Usage();
    using var device = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var publicParameters = device.ExportParameters(false);
    var activation = new ActivationRequest
    {
        ProtocolVersion = LicensingProtocol.CurrentVersion,
        ActivationCode = arguments[4],
        DevicePublicKeyX = LicenseCrypto.Base64UrlEncode(publicParameters.Q.X!),
        DevicePublicKeyY = LicenseCrypto.Base64UrlEncode(publicParameters.Q.Y!)
    };
    activation.Proof = LicenseCrypto.Base64UrlEncode(device.SignData(
        LicenseCrypto.ActivationProofBytes(activation), HashAlgorithmName.SHA256));
    using var api = ApiClient(arguments[1]);
    using var activationResponse = await api.PostAsJsonAsync("api/v1/activations", activation);
    var entitlement = JsonSerializer.Deserialize<EntitlementResponse>(await ReadSuccessAsync(activationResponse), WebJson())!;
    using (var admin = AdminClient(arguments[1], arguments[2]))
    using (var transfer = await admin.PostAsync("api/v1/admin/licenses/" + Uri.EscapeDataString(arguments[3]) + "/transfer", null))
        await ReadSuccessAsync(transfer);
    var nonce = new byte[24];
    RandomNumberGenerator.Fill(nonce);
    var renewal = new RenewalRequest
    {
        ProtocolVersion = LicensingProtocol.CurrentVersion,
        ActivationId = entitlement.ActivationId,
        Nonce = LicenseCrypto.Base64UrlEncode(nonce)
    };
    renewal.Proof = LicenseCrypto.Base64UrlEncode(device.SignData(
        LicenseCrypto.RenewalProofBytes(renewal), HashAlgorithmName.SHA256));
    using var renewalResponse = await api.PostAsJsonAsync("api/v1/renewals", renewal);
    var body = await renewalResponse.Content.ReadAsStringAsync();
    if ((int)renewalResponse.StatusCode != 403 || !body.Contains("activation_revoked", StringComparison.Ordinal))
        throw new InvalidOperationException("Revoked activation was not rejected: " + body);
    Console.WriteLine("{\"status\":\"ok\",\"revokedRenewal\":\"rejected\"}");
    return 0;
}

static async Task<int> SimulateCatalogAsync(string[] arguments)
{
    if (arguments.Length != 4) return Usage();
    using var device = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var publicParameters = device.ExportParameters(false);
    var activation = new ActivationRequest
    {
        ProtocolVersion = LicensingProtocol.CurrentVersion,
        ActivationCode = arguments[2],
        DevicePublicKeyX = LicenseCrypto.Base64UrlEncode(publicParameters.Q.X!),
        DevicePublicKeyY = LicenseCrypto.Base64UrlEncode(publicParameters.Q.Y!)
    };
    activation.Proof = LicenseCrypto.Base64UrlEncode(device.SignData(LicenseCrypto.ActivationProofBytes(activation), HashAlgorithmName.SHA256));
    using var client = ApiClient(arguments[1]);
    using var activationResponse = await client.PostAsJsonAsync("api/v1/activations", activation);
    var entitlement = JsonSerializer.Deserialize<EntitlementResponse>(await ReadSuccessAsync(activationResponse), WebJson())!;
    CatalogPackageHeader header;
    using (var package = File.OpenRead(arguments[3])) header = CatalogPackage.ReadHeader(package);
    var nonce = RandomNumberGenerator.GetBytes(24);
    var keyRequest = new CatalogKeyRequest
    {
        ProtocolVersion = LicensingProtocol.CurrentVersion,
        ActivationId = entitlement.ActivationId,
        KeyId = header.KeyId,
        Nonce = LicenseCrypto.Base64UrlEncode(nonce)
    };
    keyRequest.Proof = LicenseCrypto.Base64UrlEncode(device.SignData(LicenseCrypto.CatalogKeyProofBytes(keyRequest), HashAlgorithmName.SHA256));
    using var keyResponseMessage = await client.PostAsJsonAsync("api/v1/catalog-keys", keyRequest);
    var keyResponse = JsonSerializer.Deserialize<CatalogKeyResponse>(await ReadSuccessAsync(keyResponseMessage), WebJson())!;
    byte[] plaintext;
    using (var package = File.OpenRead(arguments[3])) plaintext = CatalogPackage.Decrypt(package, LicenseCrypto.Base64UrlDecode(keyResponse.KeyMaterial));
    using var replayResponse = await client.PostAsJsonAsync("api/v1/catalog-keys", keyRequest);
    var replayBody = await replayResponse.Content.ReadAsStringAsync();
    if ((int)replayResponse.StatusCode != 409 || !replayBody.Contains("catalog_key_replay", StringComparison.Ordinal))
        throw new InvalidOperationException("Catalog key proof replay was not rejected: " + replayBody);
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        status = "ok", header.CatalogVersion, header.KeyId, plaintextBytes = plaintext.Length,
        keyResponse.ExpiresAtUnix, replay = "rejected"
    }, new JsonSerializerOptions { WriteIndented = true }));
    CryptographicOperations.ZeroMemory(plaintext);
    return 0;
}

static HttpClient ApiClient(string baseUrl) => new() { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };

static HttpClient AdminClient(string baseUrl, string adminKey)
{
    var client = ApiClient(baseUrl);
    client.DefaultRequestHeaders.Add("X-Atlas-Admin-Key", adminKey);
    return client;
}

static async Task<string> ReadSuccessAsync(HttpResponseMessage response)
{
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
        throw new HttpRequestException($"HTTP {(int)response.StatusCode}: {body}");
    return body;
}

static JsonSerializerOptions WebJson() => new(JsonSerializerDefaults.Web);

static int Usage()
{
    Console.Error.WriteLine("Atlas License Manager");
    Console.Error.WriteLine("  generate-signing-key <private.pk8> <public.json> <key-id>");
    Console.Error.WriteLine("  generate-catalog-key <key-ring.json> <key-id>");
    Console.Error.WriteLine("  import-catalog-key <target-ring.json> <source-ring.json> <key-id>");
    Console.Error.WriteLine("  issue <api-base> <admin-key> <customer-reference>");
    Console.Error.WriteLine("  transfer <api-base> <admin-key> <license-id>");
    Console.Error.WriteLine("  create-admin <api-base> <admin-key> <email> <name>");
    Console.Error.WriteLine("  simulate-device <api-base> <activation-code>");
    Console.Error.WriteLine("  simulate-revocation <api-base> <admin-key> <license-id> <activation-code>");
    Console.Error.WriteLine("  simulate-catalog <api-base> <activation-code> <package.atlascatalog>");
    return 2;
}

sealed class PublicKeyDescriptor
{
    public string X { get; set; } = string.Empty;
    public string Y { get; set; } = string.Empty;
}
