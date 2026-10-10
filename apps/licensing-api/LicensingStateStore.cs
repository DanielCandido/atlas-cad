using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Licensing;

namespace Atlas.Licensing.Api;

public sealed class LicensingStateStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public LicensingStateStore(LicensingServerOptions options) => _path = options.StatePath;

    public async Task<CreateLicenseResponse> CreateLicenseAsync(CreateLicenseRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CustomerReference) || request.CustomerReference.Length > 200)
            throw new LicenseServerException("customer_reference_invalid", "Customer reference is required.", 400);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadAsync(cancellationToken);
            var license = new LicenseRecord
            {
                Id = "lic_" + Guid.NewGuid().ToString("N"),
                ProductId = LicensingProtocol.ProductId,
                CustomerReference = request.CustomerReference.Trim(),
                Generation = 1,
                CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            state.Licenses.Add(license);
            var code = AddActivationCode(state, license);
            await SaveAsync(state, cancellationToken);
            return Response(license, code);
        }
        finally { _gate.Release(); }
    }

    public async Task<CreateLicenseResponse> TransferAsync(string licenseId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadAsync(cancellationToken);
            var license = state.Licenses.SingleOrDefault(item => item.Id == licenseId) ??
                throw new LicenseServerException("license_not_found", "License was not found.", 404);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var activation in state.Activations.Where(item => item.LicenseId == license.Id && item.RevokedAtUnix == null))
                activation.RevokedAtUnix = now;
            license.Generation++;
            var code = AddActivationCode(state, license);
            await SaveAsync(state, cancellationToken);
            return Response(license, code);
        }
        finally { _gate.Release(); }
    }

    public async Task<ActivationSnapshot> ActivateAsync(ActivationRequest request, CancellationToken cancellationToken)
    {
        ValidateActivationRequest(request);
        ECParameters parameters;
        try { parameters = LicenseCrypto.PublicParameters(request.DevicePublicKeyX, request.DevicePublicKeyY); }
        catch (Exception) { throw new LicenseServerException("device_key_invalid", "Device public key is invalid.", 400); }
        using (var deviceKey = ECDsa.Create(parameters))
        {
            byte[] proof;
            try { proof = LicenseCrypto.Base64UrlDecode(request.Proof); }
            catch (FormatException) { throw new LicenseServerException("proof_invalid", "Activation proof is invalid.", 400); }
            if (!deviceKey.VerifyData(LicenseCrypto.ActivationProofBytes(request), proof, HashAlgorithmName.SHA256))
                throw new LicenseServerException("proof_invalid", "Activation proof is invalid.", 403);
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var codeHash = HashActivationCode(request.ActivationCode);
            var code = state.ActivationCodes.SingleOrDefault(item => item.CodeHash == codeHash && item.UsedAtUnix == null) ??
                throw new LicenseServerException("activation_code_invalid", "Activation code is invalid or already used.", 403);
            if (code.ExpiresAtUnix <= now)
                throw new LicenseServerException("activation_code_expired", "Activation code has expired.", 403);
            var license = state.Licenses.Single(item => item.Id == code.LicenseId);
            if (code.Generation != license.Generation)
                throw new LicenseServerException("activation_code_revoked", "Activation code belongs to an old license generation.", 403);
            if (state.Activations.Any(item => item.LicenseId == license.Id && item.Generation == license.Generation && item.RevokedAtUnix == null))
                throw new LicenseServerException("activation_limit", "License already has an active device.", 409);
            var activation = new ActivationRecord
            {
                Id = "act_" + Guid.NewGuid().ToString("N"),
                LicenseId = license.Id,
                Generation = license.Generation,
                DevicePublicKeyX = request.DevicePublicKeyX,
                DevicePublicKeyY = request.DevicePublicKeyY,
                DeviceKeySha256 = LicenseCrypto.DeviceFingerprint(parameters),
                ActivatedAtUnix = now,
                LastRenewedAtUnix = now
            };
            code.UsedAtUnix = now;
            state.Activations.Add(activation);
            await SaveAsync(state, cancellationToken);
            return Snapshot(license, activation);
        }
        finally { _gate.Release(); }
    }

    public async Task<ActivationSnapshot> RenewAsync(RenewalRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.ProtocolVersion != LicensingProtocol.CurrentVersion ||
            string.IsNullOrWhiteSpace(request.ActivationId) || request.ActivationId.Length > 80 ||
            string.IsNullOrWhiteSpace(request.Nonce) || request.Nonce.Length < 24 || request.Nonce.Length > 200 ||
            string.IsNullOrWhiteSpace(request.Proof))
            throw new LicenseServerException("renewal_request_invalid", "Renewal request is invalid.", 400);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadAsync(cancellationToken);
            var activation = state.Activations.SingleOrDefault(item => item.Id == request.ActivationId) ??
                throw new LicenseServerException("activation_not_found", "Activation was not found.", 404);
            var license = state.Licenses.Single(item => item.Id == activation.LicenseId);
            if (activation.RevokedAtUnix != null || activation.Generation != license.Generation)
                throw new LicenseServerException("activation_revoked", "Activation has been revoked.", 403);
            if (activation.UsedNonces.Contains(request.Nonce, StringComparer.Ordinal))
                throw new LicenseServerException("renewal_replay", "Renewal nonce was already used.", 409);
            var parameters = LicenseCrypto.PublicParameters(activation.DevicePublicKeyX, activation.DevicePublicKeyY);
            using var deviceKey = ECDsa.Create(parameters);
            byte[] proof;
            try { proof = LicenseCrypto.Base64UrlDecode(request.Proof); }
            catch (FormatException) { throw new LicenseServerException("proof_invalid", "Renewal proof is invalid.", 400); }
            if (!deviceKey.VerifyData(LicenseCrypto.RenewalProofBytes(request), proof, HashAlgorithmName.SHA256))
                throw new LicenseServerException("proof_invalid", "Renewal proof is invalid.", 403);
            activation.LastNonce = request.Nonce;
            activation.UsedNonces.Add(request.Nonce);
            if (activation.UsedNonces.Count > 128) activation.UsedNonces.RemoveAt(0);
            activation.LastRenewedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await SaveAsync(state, cancellationToken);
            return Snapshot(license, activation);
        }
        finally { _gate.Release(); }
    }

    public async Task<long> AuthorizeCatalogKeyAsync(CatalogKeyRequest request, CancellationToken cancellationToken)
    {
        if (request == null || request.ProtocolVersion != LicensingProtocol.CurrentVersion ||
            string.IsNullOrWhiteSpace(request.ActivationId) || request.ActivationId.Length > 80 ||
            string.IsNullOrWhiteSpace(request.KeyId) || request.KeyId.Length > 80 ||
            string.IsNullOrWhiteSpace(request.Nonce) || request.Nonce.Length < 24 || request.Nonce.Length > 200 ||
            string.IsNullOrWhiteSpace(request.Proof))
            throw new LicenseServerException("catalog_key_request_invalid", "Catalog key request is invalid.", 400);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadAsync(cancellationToken);
            var activation = state.Activations.SingleOrDefault(item => item.Id == request.ActivationId) ??
                throw new LicenseServerException("activation_not_found", "Activation was not found.", 404);
            var license = state.Licenses.Single(item => item.Id == activation.LicenseId);
            if (activation.RevokedAtUnix != null || activation.Generation != license.Generation)
                throw new LicenseServerException("activation_revoked", "Activation has been revoked.", 403);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var expires = activation.LastRenewedAtUnix + LicensingProtocol.OfflineHours * 60L * 60L;
            if (expires <= now)
                throw new LicenseServerException("activation_refresh_required", "License must be renewed before releasing a catalog key.", 403);
            if (activation.UsedCatalogNonces.Contains(request.Nonce, StringComparer.Ordinal))
                throw new LicenseServerException("catalog_key_replay", "Catalog key nonce was already used.", 409);
            var parameters = LicenseCrypto.PublicParameters(activation.DevicePublicKeyX, activation.DevicePublicKeyY);
            using var deviceKey = ECDsa.Create(parameters);
            byte[] proof;
            try { proof = LicenseCrypto.Base64UrlDecode(request.Proof); }
            catch (FormatException) { throw new LicenseServerException("proof_invalid", "Catalog key proof is invalid.", 400); }
            if (!deviceKey.VerifyData(LicenseCrypto.CatalogKeyProofBytes(request), proof, HashAlgorithmName.SHA256))
                throw new LicenseServerException("proof_invalid", "Catalog key proof is invalid.", 403);
            activation.UsedCatalogNonces.Add(request.Nonce);
            if (activation.UsedCatalogNonces.Count > 128) activation.UsedCatalogNonces.RemoveAt(0);
            await SaveAsync(state, cancellationToken);
            return expires;
        }
        finally { _gate.Release(); }
    }

    private static void ValidateActivationRequest(ActivationRequest request)
    {
        if (request == null || request.ProtocolVersion != LicensingProtocol.CurrentVersion ||
            string.IsNullOrWhiteSpace(request.ActivationCode) || request.ActivationCode.Length > 100 ||
            string.IsNullOrWhiteSpace(request.DevicePublicKeyX) || string.IsNullOrWhiteSpace(request.DevicePublicKeyY) ||
            string.IsNullOrWhiteSpace(request.Proof))
            throw new LicenseServerException("activation_request_invalid", "Activation request is invalid.", 400);
    }

    private static string AddActivationCode(LicensingState state, LicenseRecord license)
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(15));
        var code = "ATLAS-" + string.Join("-", Enumerable.Range(0, 5).Select(index => raw.Substring(index * 6, 6)));
        state.ActivationCodes.Add(new ActivationCodeRecord
        {
            LicenseId = license.Id,
            Generation = license.Generation,
            CodeHash = HashActivationCode(code),
            ExpiresAtUnix = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds()
        });
        return code;
    }

    private static string HashActivationCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));

    private async Task<LicensingState> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path)) return new LicensingState();
        await using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<LicensingState>(stream, _json, cancellationToken) ??
            throw new InvalidDataException("Licensing state is empty.");
    }

    private async Task SaveAsync(LicensingState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporary = _path + ".tmp";
        if (File.Exists(temporary)) File.Delete(temporary);
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, state, _json, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporary, _path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static CreateLicenseResponse Response(LicenseRecord license, string code) => new()
    { LicenseId = license.Id, ProductId = license.ProductId, Generation = license.Generation, ActivationCode = code };

    private static ActivationSnapshot Snapshot(LicenseRecord license, ActivationRecord activation) => new()
    {
        LicenseId = license.Id, ProductId = license.ProductId, Generation = activation.Generation,
        ActivationId = activation.Id, DeviceKeySha256 = activation.DeviceKeySha256
    };
}

public sealed class CreateLicenseRequest { public string CustomerReference { get; set; } = string.Empty; }
public sealed class CreateLicenseResponse
{
    public string LicenseId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Generation { get; set; }
    public string ActivationCode { get; set; } = string.Empty;
}
public sealed class ActivationSnapshot
{
    public string LicenseId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public int Generation { get; set; }
    public string ActivationId { get; set; } = string.Empty;
    public string DeviceKeySha256 { get; set; } = string.Empty;
}
public sealed class LicensingState
{
    public int Version { get; set; } = 1;
    public List<LicenseRecord> Licenses { get; set; } = new();
    public List<ActivationRecord> Activations { get; set; } = new();
    public List<ActivationCodeRecord> ActivationCodes { get; set; } = new();
}
public sealed class LicenseRecord
{
    public string Id { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string CustomerReference { get; set; } = string.Empty;
    public int Generation { get; set; }
    public long CreatedAtUnix { get; set; }
}
public sealed class ActivationRecord
{
    public string Id { get; set; } = string.Empty;
    public string LicenseId { get; set; } = string.Empty;
    public int Generation { get; set; }
    public string DevicePublicKeyX { get; set; } = string.Empty;
    public string DevicePublicKeyY { get; set; } = string.Empty;
    public string DeviceKeySha256 { get; set; } = string.Empty;
    public long ActivatedAtUnix { get; set; }
    public long LastRenewedAtUnix { get; set; }
    public long? RevokedAtUnix { get; set; }
    public string? LastNonce { get; set; }
    public List<string> UsedNonces { get; set; } = new();
    public List<string> UsedCatalogNonces { get; set; } = new();
}
public sealed class ActivationCodeRecord
{
    public string LicenseId { get; set; } = string.Empty;
    public int Generation { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public long ExpiresAtUnix { get; set; }
    public long? UsedAtUnix { get; set; }
}
public sealed class LicenseServerException : Exception
{
    public LicenseServerException(string code, string message, int statusCode) : base(message)
    { Code = code; StatusCode = statusCode; }
    public string Code { get; }
    public int StatusCode { get; }
}
