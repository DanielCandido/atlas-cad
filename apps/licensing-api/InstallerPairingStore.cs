using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Licensing;
using Npgsql;

namespace Atlas.Licensing.Api;

public sealed class InstallerPairingStore(
    NpgsqlDataSource dataSource,
    CommerceOptions commerceOptions,
    LibraryStore libraries)
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);
    private const int LifetimeMinutes = 10;
    private const int PollIntervalSeconds = 5;

    public async Task<InstallerPairingCreateResponse> CreateAsync(
        InstallerPairingCreateRequest request, CancellationToken cancellationToken)
    {
        ValidateCreate(request);
        var library = await libraries.ResolveAsync(string.IsNullOrWhiteSpace(request.LibrarySlug) ? "steelflow" : request.LibrarySlug, cancellationToken);
        ECParameters parameters;
        try { parameters = LicenseCrypto.PublicParameters(request.DevicePublicKeyX, request.DevicePublicKeyY); }
        catch { throw new LicenseServerException("device_key_invalid", "A chave do dispositivo é inválida.", 400); }
        using (var key = ECDsa.Create(parameters))
        {
            if (!Verify(request.Proof, LicenseCrypto.InstallerPairingCreateProofBytes(request), key))
                throw new LicenseServerException("proof_invalid", "A prova do dispositivo é inválida.", 403);
        }

        var id = "pair_" + Guid.NewGuid().ToString("N");
        var userCode = NewUserCode();
        var expires = DateTimeOffset.UtcNow.AddMinutes(LifetimeMinutes);
        var fingerprint = LicenseCrypto.DeviceFingerprint(parameters);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO atlas_installer_pairings
              (id,user_code_hash,device_public_key_x,device_public_key_y,device_fingerprint,detected_versions,status,expires_at,library_id)
            VALUES ($1,$2,$3,$4,$5,$6::jsonb,'pending',$7,$8)
            """, connection);
        command.Parameters.AddWithValue(id);
        command.Parameters.AddWithValue(HashCode(userCode));
        command.Parameters.AddWithValue(request.DevicePublicKeyX);
        command.Parameters.AddWithValue(request.DevicePublicKeyY);
        command.Parameters.AddWithValue(fingerprint);
        command.Parameters.AddWithValue(JsonSerializer.Serialize(request.DetectedVersions, WebJson));
        command.Parameters.AddWithValue(expires);
        command.Parameters.AddWithValue(library.Id);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new InstallerPairingCreateResponse
        {
            PairingId = id,
            UserCode = userCode,
            VerificationUri = commerceOptions.PublicWebUrl + "/vincular?codigo=" + Uri.EscapeDataString(userCode),
            ExpiresAtUnix = expires.ToUnixTimeSeconds(),
            PollIntervalSeconds = PollIntervalSeconds
        };
    }

    public async Task<InstallerPairingStatusResponse> PollAsync(
        string pairingId, InstallerPairingProofRequest request, CancellationToken cancellationToken)
    {
        var row = await ReadPairingByIdAsync(pairingId, cancellationToken);
        VerifyPairingProof("poll", pairingId, request, row);
        var status = row.ExpiresAt <= DateTimeOffset.UtcNow && row.Status is "pending" or "approved"
            ? "expired" : row.Status;
        return new InstallerPairingStatusResponse { Status = status, ExpiresAtUnix = row.ExpiresAt.ToUnixTimeSeconds() };
    }

    public async Task<ActivationSnapshot> CompleteAsync(
        string pairingId, InstallerPairingProofRequest request, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var row = await ReadPairingByIdAsync(connection, transaction, pairingId, cancellationToken);
        VerifyPairingProof("complete", pairingId, request, row);
        if (row.ExpiresAt <= DateTimeOffset.UtcNow && row.Status != "completed")
            throw new LicenseServerException("pairing_expired", "A vinculação expirou. Inicie novamente.", 410);
        if (row.Status == "rejected") throw new LicenseServerException("pairing_rejected", "A vinculação foi recusada.", 403);
        if (row.Status is not ("approved" or "completed"))
            throw new LicenseServerException("pairing_not_approved", "A vinculação ainda não foi aprovada.", 409);

        if (row.ActivationId != null)
        {
            var existing = await ReadActivationSnapshotAsync(connection, transaction, row.ActivationId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return existing;
        }
        if (row.LicenseId == null || row.CustomerId == null)
            throw new LicenseServerException("pairing_invalid", "A vinculação aprovada está incompleta.", 409);

        await using var license = new NpgsqlCommand("""
            SELECT l.generation,l.product_id,s.status,s.paid_through,l.revoked_at,l.manual_valid_until,lib.module_code
            FROM atlas_licenses l JOIN atlas_subscriptions s ON s.id=l.subscription_id
            JOIN atlas_libraries lib ON lib.id=s.library_id
            WHERE l.id=$1 AND s.customer_id=$2 FOR UPDATE OF l,s
            """, connection, transaction);
        license.Parameters.AddWithValue(row.LicenseId);
        license.Parameters.AddWithValue(row.CustomerId.Value);
        await using var reader = await license.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new LicenseServerException("license_not_found", "Licença não encontrada.", 404);
        var generation = reader.GetInt32(0);
        var productId = reader.GetString(1);
        var moduleCode = reader.GetString(6);
        var activeSubscription = reader.IsDBNull(4) &&
            ((reader.GetString(2) == "active" && !reader.IsDBNull(3) && reader.GetFieldValue<DateTimeOffset>(3) > DateTimeOffset.UtcNow) ||
             (!reader.IsDBNull(5) && reader.GetFieldValue<DateTimeOffset>(5) > DateTimeOffset.UtcNow));
        await reader.DisposeAsync();
        if (!activeSubscription)
            throw new LicenseServerException("subscription_inactive", "A assinatura não está ativa.", 403);

        await using var active = new NpgsqlCommand(
            "SELECT id,device_fingerprint FROM atlas_activations WHERE license_id=$1 AND generation=$2 AND revoked_at IS NULL FOR UPDATE",
            connection, transaction);
        active.Parameters.AddWithValue(row.LicenseId);
        active.Parameters.AddWithValue(generation);
        await using var activeReader = await active.ExecuteReaderAsync(cancellationToken);
        string? currentActivation = null;
        string? currentFingerprint = null;
        if (await activeReader.ReadAsync(cancellationToken))
        {
            currentActivation = activeReader.GetString(0);
            currentFingerprint = activeReader.GetString(1);
        }
        await activeReader.DisposeAsync();

        if (currentActivation != null && FixedEquals(currentFingerprint!, row.Fingerprint))
        {
            await ExecuteAsync(connection, transaction,
                "UPDATE atlas_activations SET last_renewed_at=now() WHERE id=$1",
                cancellationToken, currentActivation);
            await ExecuteAsync(connection, transaction,
                "UPDATE atlas_installer_pairings SET status='completed',activation_id=$2,completed_at=now() WHERE id=$1",
                cancellationToken, pairingId, currentActivation);
            var sameDevice = await ReadActivationSnapshotAsync(connection, transaction, currentActivation, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return sameDevice;
        }
        if (currentActivation != null && !row.ReplaceExisting)
            throw new LicenseServerException("replacement_confirmation_required", "Confirme a substituição da máquina atual.", 409);
        if (currentActivation != null)
        {
            generation++;
            await ExecuteAsync(connection, transaction, "UPDATE atlas_licenses SET generation=$2 WHERE id=$1", cancellationToken, row.LicenseId, generation);
            await ExecuteAsync(connection, transaction, "UPDATE atlas_activations SET revoked_at=now() WHERE license_id=$1 AND revoked_at IS NULL", cancellationToken, row.LicenseId);
            await ExecuteAsync(connection, transaction, "DELETE FROM atlas_activation_codes WHERE license_id=$1 AND used_at IS NULL", cancellationToken, row.LicenseId);
        }

        var activationId = "act_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(connection, transaction, """
            INSERT INTO atlas_activations
              (id,license_id,generation,device_public_key_x,device_public_key_y,device_fingerprint,activated_at,last_renewed_at)
            VALUES ($1,$2,$3,$4,$5,$6,now(),now())
            """, cancellationToken, activationId, row.LicenseId, generation, row.X, row.Y, row.Fingerprint);
        await ExecuteAsync(connection, transaction,
            "UPDATE atlas_installer_pairings SET status='completed',activation_id=$2,completed_at=now() WHERE id=$1",
            cancellationToken, pairingId, activationId);
        await transaction.CommitAsync(cancellationToken);
        return new ActivationSnapshot
        {
            LicenseId = row.LicenseId,
            ProductId = productId,
            Generation = generation,
            ActivationId = activationId,
            DeviceKeySha256 = row.Fingerprint,
            ModuleCode = moduleCode
        };
    }

    public async Task<PortalPairingSnapshot> GetPortalAsync(
        Guid customerId, string userCode, CancellationToken cancellationToken)
    {
        var row = await ReadPairingByCodeAsync(userCode, cancellationToken);
        if (row.CustomerId != null && row.CustomerId != customerId)
            throw new LicenseServerException("pairing_not_found", "Vinculação não encontrada.", 404);
        var activeFingerprint = await FindActiveFingerprintAsync(customerId, row.LibraryId, cancellationToken);
        return new PortalPairingSnapshot(
            NormalizeCode(userCode),
            row.LibrarySlug,
            row.ExpiresAt <= DateTimeOffset.UtcNow && row.Status is "pending" or "approved" ? "expired" : row.Status,
            row.Fingerprint,
            row.DetectedVersions,
            row.ExpiresAt,
            activeFingerprint != null && !FixedEquals(activeFingerprint, row.Fingerprint),
            activeFingerprint);
    }

    public async Task ApproveAsync(Guid customerId, string userCode, bool replaceExisting, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var row = await ReadPairingByCodeAsync(connection, transaction, userCode, cancellationToken);
        if (row.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new LicenseServerException("pairing_expired", "A vinculação expirou. Inicie novamente.", 410);
        if (row.Status != "pending" || (row.CustomerId != null && row.CustomerId != customerId))
            throw new LicenseServerException("pairing_unavailable", "A vinculação não está disponível.", 409);

        await using var license = new NpgsqlCommand("""
            SELECT l.id,l.generation
            FROM atlas_licenses l JOIN atlas_subscriptions s ON s.id=l.subscription_id
            WHERE s.customer_id=$1 AND s.library_id=$2 AND l.revoked_at IS NULL
              AND ((s.status='active' AND s.paid_through>now()) OR l.manual_valid_until>now())
            ORDER BY s.created_at DESC LIMIT 1 FOR UPDATE OF l
            """, connection, transaction);
        license.Parameters.AddWithValue(customerId);
        license.Parameters.AddWithValue(row.LibraryId);
        await using var reader = await license.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new LicenseServerException("subscription_inactive", "O pagamento precisa estar confirmado.", 403);
        var licenseId = reader.GetString(0);
        var generation = reader.GetInt32(1);
        await reader.DisposeAsync();
        await using var active = new NpgsqlCommand(
            "SELECT device_fingerprint FROM atlas_activations WHERE license_id=$1 AND generation=$2 AND revoked_at IS NULL",
            connection, transaction);
        active.Parameters.AddWithValue(licenseId);
        active.Parameters.AddWithValue(generation);
        var activeFingerprint = await active.ExecuteScalarAsync(cancellationToken) as string;
        if (activeFingerprint != null && !FixedEquals(activeFingerprint, row.Fingerprint) && !replaceExisting)
            throw new LicenseServerException("replacement_confirmation_required", "Confirme a substituição da máquina atual.", 409);

        await ExecuteAsync(connection, transaction, """
            UPDATE atlas_installer_pairings
            SET status='approved',customer_id=$2,license_id=$3,replace_existing=$4,approved_at=now()
            WHERE id=$1
            """, cancellationToken, row.Id, customerId, licenseId, replaceExisting);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid customerId, string userCode, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            UPDATE atlas_installer_pairings SET status='rejected',customer_id=$2
            WHERE user_code_hash=$1 AND status='pending' AND expires_at>now()
              AND (customer_id IS NULL OR customer_id=$2)
            """, connection);
        command.Parameters.AddWithValue(HashCode(userCode));
        command.Parameters.AddWithValue(customerId);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new LicenseServerException("pairing_unavailable", "A vinculação não está disponível.", 409);
    }

    private async Task<string?> FindActiveFingerprintAsync(Guid customerId, Guid libraryId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT a.device_fingerprint FROM atlas_activations a
            JOIN atlas_licenses l ON l.id=a.license_id JOIN atlas_subscriptions s ON s.id=l.subscription_id
            WHERE s.customer_id=$1 AND s.library_id=$2 AND a.revoked_at IS NULL AND a.generation=l.generation
            ORDER BY a.activated_at DESC LIMIT 1
            """, connection);
        command.Parameters.AddWithValue(customerId);
        command.Parameters.AddWithValue(libraryId);
        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    private async Task<PairingRow> ReadPairingByIdAsync(string id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadPairingByIdAsync(connection, null, id, cancellationToken);
    }

    private static Task<PairingRow> ReadPairingByIdAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string id, CancellationToken cancellationToken) =>
        ReadPairingAsync(connection, transaction, "id=$1", id, cancellationToken);

    private async Task<PairingRow> ReadPairingByCodeAsync(string code, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ReadPairingByCodeAsync(connection, null, code, cancellationToken);
    }

    private static Task<PairingRow> ReadPairingByCodeAsync(NpgsqlConnection connection, NpgsqlTransaction? transaction, string code, CancellationToken cancellationToken) =>
        ReadPairingAsync(connection, transaction, "user_code_hash=$1", HashCode(code), cancellationToken);

    private static async Task<PairingRow> ReadPairingAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, string predicate, string value, CancellationToken cancellationToken)
    {
        var lockClause = transaction == null ? string.Empty : " FOR UPDATE";
        await using var command = new NpgsqlCommand($"""
            SELECT id,device_public_key_x,device_public_key_y,device_fingerprint,detected_versions::text,status,
                   customer_id,license_id,replace_existing,activation_id,expires_at,library_id,
                   (SELECT slug FROM atlas_libraries WHERE id=library_id)
            FROM atlas_installer_pairings WHERE {predicate}{lockClause}
            """, connection, transaction);
        command.Parameters.AddWithValue(value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new LicenseServerException("pairing_not_found", "Vinculação não encontrada.", 404);
        return new PairingRow(
            reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            JsonSerializer.Deserialize<List<string>>(reader.GetString(4), WebJson) ?? new List<string>(),
            reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.GetBoolean(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetFieldValue<DateTimeOffset>(10), reader.GetGuid(11), reader.GetString(12));
    }

    private static async Task<ActivationSnapshot> ReadActivationSnapshotAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string activationId, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT a.license_id,l.product_id,a.generation,a.id,a.device_fingerprint,COALESCE(lib.module_code,'')
            FROM atlas_activations a JOIN atlas_licenses l ON l.id=a.license_id
            LEFT JOIN atlas_subscriptions s ON s.id=l.subscription_id LEFT JOIN atlas_libraries lib ON lib.id=s.library_id
            WHERE a.id=$1
            """, connection, transaction);
        command.Parameters.AddWithValue(activationId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new LicenseServerException("activation_not_found", "Ativação não encontrada.", 404);
        return new ActivationSnapshot
        {
            LicenseId = reader.GetString(0), ProductId = reader.GetString(1), Generation = reader.GetInt32(2),
            ActivationId = reader.GetString(3), DeviceKeySha256 = reader.GetString(4), ModuleCode = reader.GetString(5)
        };
    }

    private static void VerifyPairingProof(string operation, string pairingId, InstallerPairingProofRequest request, PairingRow row)
    {
        if (request.ProtocolVersion != LicensingProtocol.CurrentVersion || string.IsNullOrWhiteSpace(request.Nonce) || request.Nonce.Length < 24)
            throw new LicenseServerException("pairing_request_invalid", "Solicitação de vinculação inválida.", 400);
        using var key = ECDsa.Create(LicenseCrypto.PublicParameters(row.X, row.Y));
        if (!Verify(request.Proof, LicenseCrypto.InstallerPairingProofBytes(operation, pairingId, request), key))
            throw new LicenseServerException("proof_invalid", "A prova do dispositivo é inválida.", 403);
    }

    private static void ValidateCreate(InstallerPairingCreateRequest request)
    {
        if (request == null || request.ProtocolVersion != LicensingProtocol.CurrentVersion ||
            string.IsNullOrWhiteSpace(request.DevicePublicKeyX) || string.IsNullOrWhiteSpace(request.DevicePublicKeyY) ||
            string.IsNullOrWhiteSpace(request.Nonce) || request.Nonce.Length < 24 || string.IsNullOrWhiteSpace(request.Proof) ||
            request.DetectedVersions == null || request.DetectedVersions.Count is < 1 or > 10 ||
            request.DetectedVersions.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 80))
            throw new LicenseServerException("pairing_request_invalid", "Solicitação de vinculação inválida.", 400);
    }

    private static bool Verify(string proof, byte[] bytes, ECDsa key)
    {
        try { return key.VerifyData(bytes, LicenseCrypto.Base64UrlDecode(proof), HashAlgorithmName.SHA256); }
        catch (FormatException) { return false; }
    }

    private static string NewUserCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(8);
        var chars = bytes.Select(value => alphabet[value % alphabet.Length]).ToArray();
        return new string(chars, 0, 4) + "-" + new string(chars, 4, 4);
    }

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string HashCode(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(NormalizeCode(value))));
    private static bool FixedEquals(string left, string right)
    {
        var a = Encoding.UTF8.GetBytes(left); var b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql,
        CancellationToken cancellationToken, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private sealed record PairingRow(string Id, string X, string Y, string Fingerprint, List<string> DetectedVersions,
        string Status, Guid? CustomerId, string? LicenseId, bool ReplaceExisting, string? ActivationId, DateTimeOffset ExpiresAt, Guid LibraryId, string LibrarySlug);
}
