using System.Security.Cryptography;
using System.Text;
using Atlas.Licensing;
using Npgsql;

namespace Atlas.Licensing.Api;

public sealed class PostgresLicensingStore(NpgsqlDataSource dataSource)
{
    public async Task<CreateLicenseResponse> CreateLicenseAsync(CreateLicenseRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.CustomerReference) || request.CustomerReference.Length > 200)
            throw new LicenseServerException("customer_reference_invalid", "Customer reference is required.", 400);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var licenseId = "lic_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(connection, transaction,
            "INSERT INTO atlas_licenses(id, product_id, customer_reference, generation) VALUES ($1,$2,$3,1)",
            cancellationToken, licenseId, LicensingProtocol.ProductId, request.CustomerReference.Trim());
        var code = await AddActivationCodeAsync(connection, transaction, licenseId, 1, string.Empty, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CreateLicenseResponse { LicenseId = licenseId, ProductId = LicensingProtocol.ProductId, Generation = 1, ActivationCode = code };
    }

    public async Task<CreateLicenseResponse> TransferAsync(string licenseId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("UPDATE atlas_licenses SET generation=generation+1 WHERE id=$1 RETURNING generation, product_id", connection, transaction);
        command.Parameters.AddWithValue(licenseId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new LicenseServerException("license_not_found", "License was not found.", 404);
        var generation = reader.GetInt32(0);
        var productId = reader.GetString(1);
        await reader.DisposeAsync();
        await ExecuteAsync(connection, transaction, "UPDATE atlas_activations SET revoked_at=now() WHERE license_id=$1 AND revoked_at IS NULL", cancellationToken, licenseId);
        var code = await AddActivationCodeAsync(connection, transaction, licenseId, generation, string.Empty, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new CreateLicenseResponse { LicenseId = licenseId, ProductId = productId, Generation = generation, ActivationCode = code };
    }

    public async Task<ActivationSnapshot> ActivateAsync(ActivationRequest request, CancellationToken cancellationToken)
    {
        ValidateActivationRequest(request);
        var parameters = ReadAndVerifyDevice(request);
        var fingerprint = LicenseCrypto.DeviceFingerprint(parameters);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT c.license_id,c.generation,l.product_id,l.generation,c.device_fingerprint,c.expires_at,c.used_at,
                   s.status,s.paid_through
            FROM atlas_activation_codes c
            JOIN atlas_licenses l ON l.id=c.license_id
            LEFT JOIN atlas_subscriptions s ON s.id=l.subscription_id
            WHERE c.code_hash=$1 FOR UPDATE OF c,l
            """, connection, transaction);
        command.Parameters.AddWithValue(HashActivationCode(request.ActivationCode));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new LicenseServerException("activation_code_invalid", "Activation code is invalid.", 403);
        var licenseId = reader.GetString(0);
        var codeGeneration = reader.GetInt32(1);
        var productId = reader.GetString(2);
        var licenseGeneration = reader.GetInt32(3);
        var expectedFingerprint = reader.GetString(4);
        var expiresAt = reader.GetFieldValue<DateTimeOffset>(5);
        var used = !reader.IsDBNull(6);
        var subscriptionActive = reader.IsDBNull(7) ||
            (reader.GetString(7) == "active" && !reader.IsDBNull(8) && reader.GetFieldValue<DateTimeOffset>(8) > DateTimeOffset.UtcNow);
        await reader.DisposeAsync();
        if (used) throw new LicenseServerException("activation_code_invalid", "Activation code is invalid or already used.", 403);
        if (expiresAt <= DateTimeOffset.UtcNow) throw new LicenseServerException("activation_code_expired", "Activation code has expired.", 403);
        if (codeGeneration != licenseGeneration) throw new LicenseServerException("activation_code_revoked", "Activation code belongs to an old license generation.", 403);
        if (!subscriptionActive) throw new LicenseServerException("subscription_inactive", "The subscription is not active.", 403);
        if (!string.IsNullOrEmpty(expectedFingerprint) && !FixedEquals(expectedFingerprint, fingerprint))
            throw new LicenseServerException("device_mismatch", "Activation code belongs to another machine.", 403);
        var activationId = "act_" + Guid.NewGuid().ToString("N");
        try
        {
            await ExecuteAsync(connection, transaction, """
                INSERT INTO atlas_activations(id,license_id,generation,device_public_key_x,device_public_key_y,device_fingerprint,activated_at,last_renewed_at)
                VALUES ($1,$2,$3,$4,$5,$6,now(),now())
                """, cancellationToken, activationId, licenseId, licenseGeneration, request.DevicePublicKeyX, request.DevicePublicKeyY, fingerprint);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new LicenseServerException("activation_limit", "License already has an active device.", 409); }
        await ExecuteAsync(connection, transaction, "UPDATE atlas_activation_codes SET used_at=now() WHERE code_hash=$1", cancellationToken, HashActivationCode(request.ActivationCode));
        await transaction.CommitAsync(cancellationToken);
        return new ActivationSnapshot { LicenseId = licenseId, ProductId = productId, Generation = licenseGeneration, ActivationId = activationId, DeviceKeySha256 = fingerprint };
    }

    public async Task<ActivationSnapshot> RenewAsync(RenewalRequest request, CancellationToken cancellationToken)
    {
        ValidateProofRequest(request?.ProtocolVersion ?? 0, request?.ActivationId, request?.Nonce, request?.Proof, "renewal_request_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var row = await ReadActivationAsync(connection, transaction, request!.ActivationId, cancellationToken);
        EnsureActive(row);
        using var key = ECDsa.Create(LicenseCrypto.PublicParameters(row.X, row.Y));
        if (!VerifyProof(request.Proof, LicenseCrypto.RenewalProofBytes(request), key))
            throw new LicenseServerException("proof_invalid", "Renewal proof is invalid.", 403);
        await InsertNonceAsync(connection, transaction, request.ActivationId, request.Nonce, "renewal", cancellationToken);
        await ExecuteAsync(connection, transaction, "UPDATE atlas_activations SET last_renewed_at=now() WHERE id=$1", cancellationToken, request.ActivationId);
        await transaction.CommitAsync(cancellationToken);
        return new ActivationSnapshot { LicenseId = row.LicenseId, ProductId = row.ProductId, Generation = row.Generation, ActivationId = request.ActivationId, DeviceKeySha256 = row.Fingerprint };
    }

    public async Task<long> AuthorizeCatalogKeyAsync(CatalogKeyRequest request, CancellationToken cancellationToken)
    {
        ValidateProofRequest(request?.ProtocolVersion ?? 0, request?.ActivationId, request?.Nonce, request?.Proof, "catalog_key_request_invalid");
        if (string.IsNullOrWhiteSpace(request!.KeyId) || request.KeyId.Length > 80)
            throw new LicenseServerException("catalog_key_request_invalid", "Catalog key request is invalid.", 400);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var row = await ReadActivationAsync(connection, transaction, request.ActivationId, cancellationToken);
        EnsureActive(row);
        var expires = row.LastRenewedAt.AddHours(LicensingProtocol.OfflineHours);
        if (expires <= DateTimeOffset.UtcNow) throw new LicenseServerException("activation_refresh_required", "License must be renewed before releasing a catalog key.", 403);
        using var key = ECDsa.Create(LicenseCrypto.PublicParameters(row.X, row.Y));
        if (!VerifyProof(request.Proof, LicenseCrypto.CatalogKeyProofBytes(request), key))
            throw new LicenseServerException("proof_invalid", "Catalog key proof is invalid.", 403);
        await InsertNonceAsync(connection, transaction, request.ActivationId, request.Nonce, "catalog", cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return expires.ToUnixTimeSeconds();
    }

    internal static string NewActivationCode()
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(15));
        return "ATLAS-" + string.Join("-", Enumerable.Range(0, 5).Select(index => raw.Substring(index * 6, 6)));
    }

    internal static string HashActivationCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));

    private static async Task<string> AddActivationCodeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        string licenseId, int generation, string fingerprint, CancellationToken cancellationToken)
    {
        var code = NewActivationCode();
        await ExecuteAsync(connection, transaction, "INSERT INTO atlas_activation_codes(code_hash,license_id,generation,device_fingerprint,expires_at) VALUES ($1,$2,$3,$4,now()+interval '7 days')",
            cancellationToken, HashActivationCode(code), licenseId, generation, fingerprint);
        return code;
    }

    private static ECParameters ReadAndVerifyDevice(ActivationRequest request)
    {
        ECParameters parameters;
        try { parameters = LicenseCrypto.PublicParameters(request.DevicePublicKeyX, request.DevicePublicKeyY); }
        catch { throw new LicenseServerException("device_key_invalid", "Device public key is invalid.", 400); }
        using var key = ECDsa.Create(parameters);
        if (!VerifyProof(request.Proof, LicenseCrypto.ActivationProofBytes(request), key))
            throw new LicenseServerException("proof_invalid", "Activation proof is invalid.", 403);
        return parameters;
    }

    private static bool VerifyProof(string proof, byte[] bytes, ECDsa key)
    {
        try { return key.VerifyData(bytes, LicenseCrypto.Base64UrlDecode(proof), HashAlgorithmName.SHA256); }
        catch (FormatException) { return false; }
    }

    private static async Task<ActivationRow> ReadActivationAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string activationId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT a.license_id,l.product_id,a.generation,l.generation,a.device_public_key_x,a.device_public_key_y,
                   a.device_fingerprint,a.last_renewed_at,a.revoked_at,s.status,s.paid_through
            FROM atlas_activations a JOIN atlas_licenses l ON l.id=a.license_id
            LEFT JOIN atlas_subscriptions s ON s.id=l.subscription_id WHERE a.id=$1 FOR UPDATE OF a,l
            """, connection, transaction);
        command.Parameters.AddWithValue(activationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) throw new LicenseServerException("activation_not_found", "Activation was not found.", 404);
        return new ActivationRow(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3),
            reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetFieldValue<DateTimeOffset>(7), !reader.IsDBNull(8),
            reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTimeOffset>(10));
    }

    private static void EnsureActive(ActivationRow row)
    {
        if (row.Revoked || row.Generation != row.LicenseGeneration)
            throw new LicenseServerException("activation_revoked", "Activation has been revoked.", 403);
        if (row.SubscriptionStatus != null && (row.SubscriptionStatus != "active" || row.PaidThrough <= DateTimeOffset.UtcNow))
            throw new LicenseServerException("subscription_inactive", "The subscription is not active.", 403);
    }

    private static async Task InsertNonceAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string activationId, string nonce, string kind, CancellationToken cancellationToken)
    {
        try { await ExecuteAsync(connection, transaction, "INSERT INTO atlas_activation_nonces(activation_id,nonce,kind) VALUES ($1,$2,$3)", cancellationToken, activationId, nonce, kind); }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new LicenseServerException(kind == "renewal" ? "renewal_replay" : "catalog_key_replay", "Nonce was already used.", 409); }
    }

    private static void ValidateActivationRequest(ActivationRequest request)
    {
        if (request == null || request.ProtocolVersion != LicensingProtocol.CurrentVersion || string.IsNullOrWhiteSpace(request.ActivationCode) ||
            request.ActivationCode.Length > 100 || string.IsNullOrWhiteSpace(request.DevicePublicKeyX) || string.IsNullOrWhiteSpace(request.DevicePublicKeyY) || string.IsNullOrWhiteSpace(request.Proof))
            throw new LicenseServerException("activation_request_invalid", "Activation request is invalid.", 400);
    }

    private static void ValidateProofRequest(int version, string? activationId, string? nonce, string? proof, string code)
    {
        if (version != LicensingProtocol.CurrentVersion || string.IsNullOrWhiteSpace(activationId) || activationId.Length > 80 ||
            string.IsNullOrWhiteSpace(nonce) || nonce.Length < 24 || nonce.Length > 200 || string.IsNullOrWhiteSpace(proof))
            throw new LicenseServerException(code, "Request is invalid.", 400);
    }

    private static bool FixedEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken cancellationToken, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record ActivationRow(string LicenseId, string ProductId, int Generation, int LicenseGeneration, string X, string Y,
        string Fingerprint, DateTimeOffset LastRenewedAt, bool Revoked, string? SubscriptionStatus, DateTimeOffset? PaidThrough);
}
