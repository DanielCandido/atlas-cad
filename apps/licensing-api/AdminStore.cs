using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Licensing;
using Npgsql;

namespace Atlas.Licensing.Api;

public sealed class AdminStore(NpgsqlDataSource dataSource, CommerceOptions options, CommerceStore commerce)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task BootstrapAsync(AdminBootstrapRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
            throw new LicenseServerException("admin_name_invalid", "Informe o nome do administrador.", 400);
        if (request.Password?.Length < 12)
            throw new LicenseServerException("admin_password_invalid", "A senha deve ter pelo menos 12 caracteres.", 400);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            INSERT INTO atlas_admin_users(id,name,email,password_hash,active)
            VALUES ($1,$2,$3,$4,true)
            ON CONFLICT (email) DO UPDATE SET name=EXCLUDED.name,password_hash=EXCLUDED.password_hash,active=true,updated_at=now()
            """, connection);
        command.Parameters.AddWithValue(Guid.NewGuid());
        command.Parameters.AddWithValue(request.Name.Trim());
        command.Parameters.AddWithValue(email);
        command.Parameters.AddWithValue(PasswordHasher.Hash(request.Password!));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AdminAuthResponse> LoginAsync(AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT id,name,email,password_hash FROM atlas_admin_users WHERE email=$1 AND active=true", connection);
        command.Parameters.AddWithValue(email);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || !PasswordHasher.Verify(request.Password ?? string.Empty, reader.GetString(3)))
            throw new LicenseServerException("credentials_invalid", "E-mail ou senha inválidos.", 401);
        var admin = new AdminPrincipal(reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
        await reader.DisposeAsync();
        var token = LicenseCrypto.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var expires = DateTimeOffset.UtcNow.AddHours(8);
        await using var session = new NpgsqlCommand(
            "INSERT INTO atlas_admin_sessions(token_hash,admin_user_id,expires_at) VALUES ($1,$2,$3)", connection);
        session.Parameters.AddWithValue(HashToken(token));
        session.Parameters.AddWithValue(admin.Id);
        session.Parameters.AddWithValue(expires);
        await session.ExecuteNonQueryAsync(cancellationToken);
        return new AdminAuthResponse(token, expires, admin);
    }

    public async Task<AdminPrincipal> AuthenticateAsync(string? authorization, CancellationToken cancellationToken)
    {
        var token = ReadBearer(authorization);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("""
            SELECT u.id,u.name,u.email FROM atlas_admin_sessions s
            JOIN atlas_admin_users u ON u.id=s.admin_user_id
            WHERE s.token_hash=$1 AND s.expires_at>now() AND u.active=true
            """, connection);
        command.Parameters.AddWithValue(HashToken(token));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new LicenseServerException("admin_session_expired", "Sua sessão administrativa expirou.", 401);
        return new AdminPrincipal(reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
    }

    public async Task LogoutAsync(string? authorization, CancellationToken cancellationToken)
    {
        var token = ReadBearer(authorization);
        await using var command = dataSource.CreateCommand("DELETE FROM atlas_admin_sessions WHERE token_hash=$1");
        command.Parameters.AddWithValue(HashToken(token));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<AdminUserListResponse> ListUsersAsync(
        string? query, string? plan, string? subscriptionStatus, string? accessStatus,
        string? device, string? expiry, string? cursor, int pageSize, CancellationToken cancellationToken)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (cursorCreated, cursorId) = DecodeCursor(cursor);
        var filters = new List<string> { "($1='' OR c.name ILIKE '%'||$1||'%' OR c.email ILIKE '%'||$1||'%' OR COALESCE(l.id,'') ILIKE '%'||$1||'%' OR COALESCE(a.device_fingerprint,'') ILIKE '%'||$1||'%')" };
        if (!string.IsNullOrWhiteSpace(plan)) filters.Add("s.plan_code=$2");
        if (!string.IsNullOrWhiteSpace(subscriptionStatus)) filters.Add("s.status=$3");
        if (!string.IsNullOrWhiteSpace(accessStatus)) filters.Add("access.access_status=$4");
        if (device == "active") filters.Add("a.id IS NOT NULL");
        if (device == "none") filters.Add("a.id IS NULL");
        if (expiry == "7d") filters.Add("access.access_expires_at>now() AND access.access_expires_at<=now()+interval '7 days'");
        if (expiry == "30d") filters.Add("access.access_expires_at>now() AND access.access_expires_at<=now()+interval '30 days'");
        if (expiry == "expired") filters.Add("access.access_status='expired'");
        if (cursorCreated != null) filters.Add("(c.created_at,c.id)<($5,$6)");

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand($"""
            SELECT c.id,c.name,c.email,c.created_at,s.plan_code,s.status,l.id,
                   access.access_status,access.access_expires_at,access.access_source,
                   a.device_fingerprint,a.last_renewed_at
            FROM atlas_customers c
            LEFT JOIN LATERAL (SELECT * FROM atlas_subscriptions x WHERE x.customer_id=c.id ORDER BY x.created_at DESC LIMIT 1) s ON true
            LEFT JOIN atlas_licenses l ON l.subscription_id=s.id
            LEFT JOIN LATERAL (SELECT * FROM atlas_activations x WHERE x.license_id=l.id AND x.revoked_at IS NULL AND x.generation=l.generation ORDER BY x.activated_at DESC LIMIT 1) a ON true
            CROSS JOIN LATERAL (SELECT
                CASE WHEN l.id IS NULL THEN 'none' WHEN l.revoked_at IS NOT NULL THEN 'revoked'
                     WHEN (s.status='active' AND s.paid_through>now()) OR l.manual_valid_until>now() THEN 'active' ELSE 'expired' END access_status,
                CASE WHEN l.revoked_at IS NOT NULL THEN NULL
                     WHEN s.status='active' AND s.paid_through>now() AND l.manual_valid_until>now() THEN GREATEST(s.paid_through,l.manual_valid_until)
                     WHEN s.status='active' AND s.paid_through>now() THEN s.paid_through
                     WHEN l.manual_valid_until>now() THEN l.manual_valid_until END access_expires_at,
                CASE WHEN l.revoked_at IS NOT NULL THEN NULL
                     WHEN s.status='active' AND s.paid_through>now() AND l.manual_valid_until>now() THEN 'paid_and_manual'
                     WHEN s.status='active' AND s.paid_through>now() THEN 'paid'
                     WHEN l.manual_valid_until>now() THEN 'manual' END access_source) access
            WHERE {string.Join(" AND ", filters)}
            ORDER BY c.created_at DESC,c.id DESC LIMIT $7
            """, connection);
        command.Parameters.AddWithValue((query ?? string.Empty).Trim());
        command.Parameters.AddWithValue(plan ?? string.Empty);
        command.Parameters.AddWithValue(subscriptionStatus ?? string.Empty);
        command.Parameters.AddWithValue(accessStatus ?? string.Empty);
        command.Parameters.AddWithValue(cursorCreated ?? DateTimeOffset.MaxValue);
        command.Parameters.AddWithValue(cursorId ?? Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));
        command.Parameters.AddWithValue(pageSize + 1);
        var items = new List<AdminUserSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var planCode = reader.IsDBNull(4) ? null : reader.GetString(4);
            items.Add(new AdminUserSummary(
                reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTimeOffset>(3),
                planCode, planCode != null && options.Plans.TryGetValue(planCode, out var definition) ? definition.Name : planCode,
                reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateTimeOffset>(8),
                reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTimeOffset>(11)));
        }
        string? next = null;
        if (items.Count > pageSize)
        {
            items.RemoveAt(items.Count - 1);
            var last = items[^1];
            next = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{last.CreatedAt:O}|{last.Id}"));
        }
        return new AdminUserListResponse(items, next);
    }

    public async Task<AdminUserDetail> GetUserAsync(Guid customerId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        Guid id; string name; string email; DateTimeOffset created;
        await using (var customer = new NpgsqlCommand("SELECT id,name,email,created_at FROM atlas_customers WHERE id=$1", connection))
        {
            customer.Parameters.AddWithValue(customerId);
            await using var reader = await customer.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)) throw new LicenseServerException("customer_not_found", "Usuário não encontrado.", 404);
            id = reader.GetGuid(0); name = reader.GetString(1); email = reader.GetString(2); created = reader.GetFieldValue<DateTimeOffset>(3);
        }
        var subscriptions = new List<AdminSubscriptionSummary>();
        await using (var command = new NpgsqlCommand("SELECT s.id,l.slug,l.name,s.plan_code,COALESCE(p.name,s.plan_code),s.status,s.billing_type,s.paid_through,s.created_at FROM atlas_subscriptions s JOIN atlas_libraries l ON l.id=s.library_id LEFT JOIN atlas_library_plans p ON p.library_id=l.id AND p.code=s.plan_code WHERE s.customer_id=$1 ORDER BY s.created_at DESC", connection))
        {
            command.Parameters.AddWithValue(customerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var first = true;
            while (await reader.ReadAsync(cancellationToken))
            {
                var code = reader.GetString(3);
                subscriptions.Add(new AdminSubscriptionSummary(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), code,
                    reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7),
                    reader.GetFieldValue<DateTimeOffset>(8), first));
                first = false;
            }
        }
        AdminLicenseSummary? license = null;
        await using (var command = new NpgsqlCommand("""
            SELECT l.id,l.generation,l.created_at,l.revoked_at,l.manual_valid_until,l.manual_grant_reason,s.status,s.paid_through
            FROM atlas_licenses l JOIN atlas_subscriptions s ON s.id=l.subscription_id
            WHERE s.customer_id=$1 ORDER BY s.created_at DESC LIMIT 1
            """, connection))
        {
            command.Parameters.AddWithValue(customerId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                DateTimeOffset? revoked = reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3);
                DateTimeOffset? manual = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4);
                DateTimeOffset? paid = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7);
                var paidActive = reader.GetString(6) == "active" && paid > DateTimeOffset.UtcNow;
                var manualActive = manual > DateTimeOffset.UtcNow;
                var accessStatus = revoked != null ? "revoked" : paidActive || manualActive ? "active" : "expired";
                var source = revoked != null ? null : paidActive && manualActive ? "paid_and_manual" : paidActive ? "paid" : manualActive ? "manual" : null;
                DateTimeOffset? expires = source == "paid_and_manual" ? (paid > manual ? paid : manual) : source == "paid" ? paid : source == "manual" ? manual : null;
                license = new AdminLicenseSummary(reader.GetString(0), reader.GetInt32(1), reader.GetFieldValue<DateTimeOffset>(2), revoked,
                    manual, reader.IsDBNull(5) ? null : reader.GetString(5), accessStatus, source, expires);
            }
        }
        var activations = new List<AdminActivationSummary>();
        if (license != null)
        {
            await using var command = new NpgsqlCommand("SELECT id,device_fingerprint,activated_at,last_renewed_at,revoked_at,generation FROM atlas_activations WHERE license_id=$1 ORDER BY activated_at DESC", connection);
            command.Parameters.AddWithValue(license.Id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                activations.Add(new AdminActivationSummary(reader.GetString(0), reader.GetString(1), reader.GetFieldValue<DateTimeOffset>(2),
                    reader.GetFieldValue<DateTimeOffset>(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                    reader.IsDBNull(4) && reader.GetInt32(5) == license.Generation));
        }
        var audit = new List<AdminAuditSummary>();
        await using (var command = new NpgsqlCommand("SELECT id,admin_email,action,target_type,target_id,reason,outcome,ip_address,created_at FROM atlas_admin_audit_logs WHERE (target_type='customer' AND target_id=$1) OR (target_type='license' AND target_id=COALESCE($2,'')) OR target_id IN (SELECT id FROM atlas_activations WHERE license_id=COALESCE($2,'')) ORDER BY created_at DESC LIMIT 100", connection))
        {
            command.Parameters.AddWithValue(customerId.ToString()); command.Parameters.AddWithValue(license?.Id ?? string.Empty);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                audit.Add(new AdminAuditSummary(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetFieldValue<DateTimeOffset>(8)));
        }
        return new AdminUserDetail(id, name, email, created, subscriptions, license, activations, audit);
    }

    public Task GrantManualAsync(AdminPrincipal admin, string licenseId, AdminManualGrantRequest request, string? ip, CancellationToken ct)
    {
        var reason = ValidateReason(request.Reason);
        if (request.ValidUntil <= DateTimeOffset.UtcNow) throw new LicenseServerException("valid_until_invalid", "A validade deve estar no futuro.", 400);
        return MutateLicenseAsync(admin, licenseId, "manual_grant_set", reason, ip,
            "UPDATE atlas_licenses SET manual_valid_until=$1,manual_grant_reason=$2 WHERE id=$3", ct, request.ValidUntil, reason, licenseId);
    }

    public Task ClearManualAsync(AdminPrincipal admin, string licenseId, AdminReasonRequest request, string? ip, CancellationToken ct) =>
        MutateLicenseAsync(admin, licenseId, "manual_grant_removed", ValidateReason(request.Reason), ip,
            "UPDATE atlas_licenses SET manual_valid_until=NULL,manual_grant_reason=NULL WHERE id=$1", ct, licenseId);

    public Task RevokeLicenseAsync(AdminPrincipal admin, string licenseId, AdminReasonRequest request, string? ip, CancellationToken ct) =>
        MutateLicenseAsync(admin, licenseId, "license_revoked", ValidateReason(request.Reason), ip,
            "UPDATE atlas_licenses SET revoked_at=COALESCE(revoked_at,now()),generation=generation+1 WHERE id=$1", ct, licenseId, revokeDependents: true);

    public Task RestoreLicenseAsync(AdminPrincipal admin, string licenseId, AdminReasonRequest request, string? ip, CancellationToken ct) =>
        MutateLicenseAsync(admin, licenseId, "license_restored", ValidateReason(request.Reason), ip,
            "UPDATE atlas_licenses SET revoked_at=NULL WHERE id=$1", ct, licenseId);

    public async Task RevokeActivationAsync(AdminPrincipal admin, string activationId, AdminReasonRequest request, string? ip, CancellationToken ct)
    {
        var reason = ValidateReason(request.Reason);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var before = await ReadActivationStateAsync(connection, transaction, activationId, ct);
        if (before.RootElement.GetProperty("revokedAt").ValueKind != JsonValueKind.Null ||
            before.RootElement.GetProperty("activationGeneration").GetInt32() != before.RootElement.GetProperty("licenseGeneration").GetInt32())
            throw new LicenseServerException("activation_not_active", "O dispositivo não está ativo.", 409);
        var licenseId = before.RootElement.GetProperty("licenseId").GetString()!;
        await ExecuteAsync(connection, transaction, "UPDATE atlas_activations SET revoked_at=COALESCE(revoked_at,now()) WHERE id=$1", ct, activationId);
        await ExecuteAsync(connection, transaction, "UPDATE atlas_licenses SET generation=generation+1 WHERE id=$1", ct, licenseId);
        await ExecuteAsync(connection, transaction, "DELETE FROM atlas_activation_codes WHERE license_id=$1 AND used_at IS NULL", ct, licenseId);
        var after = await ReadActivationStateAsync(connection, transaction, activationId, ct);
        await AuditAsync(connection, transaction, admin, "activation_revoked", "activation", activationId, reason, before, after, "succeeded", ip, ct);
        await transaction.CommitAsync(ct);
    }

    public async Task CancelSubscriptionAsync(AdminPrincipal admin, Guid customerId, string librarySlug, AdminReasonRequest request, string? ip, CancellationToken ct)
    {
        var reason = ValidateReason(request.Reason);
        var before = await ReadCustomerStateAsync(customerId, ct);
        try
        {
            await commerce.CancelAsync(customerId, librarySlug, ct);
            var after = await ReadCustomerStateAsync(customerId, ct);
            await WriteAuditAsync(admin, "subscription_cancelled", "customer", customerId.ToString(), reason, "succeeded", ip, ct, before, after);
        }
        catch
        {
            await WriteAuditAsync(admin, "subscription_cancelled", "customer", customerId.ToString(), reason, "failed", ip, CancellationToken.None, before, null);
            throw;
        }
    }

    private async Task MutateLicenseAsync(AdminPrincipal admin, string licenseId, string action, string reason, string? ip,
        string sql, CancellationToken ct, object? value1 = null, object? value2 = null, object? value3 = null, bool revokeDependents = false)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var before = await ReadLicenseStateAsync(connection, transaction, licenseId, ct);
        var values = new[] { value1, value2, value3 }.Where(v => v != null).Cast<object>().ToArray();
        if (values.Length == 0) values = [licenseId];
        await ExecuteAsync(connection, transaction, sql, ct, values);
        if (revokeDependents)
        {
            await ExecuteAsync(connection, transaction, "UPDATE atlas_activations SET revoked_at=COALESCE(revoked_at,now()) WHERE license_id=$1 AND revoked_at IS NULL", ct, licenseId);
            await ExecuteAsync(connection, transaction, "DELETE FROM atlas_activation_codes WHERE license_id=$1 AND used_at IS NULL", ct, licenseId);
        }
        var after = await ReadLicenseStateAsync(connection, transaction, licenseId, ct);
        await AuditAsync(connection, transaction, admin, action, "license", licenseId, reason, before, after, "succeeded", ip, ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task<JsonDocument> ReadLicenseStateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("SELECT generation,revoked_at,manual_valid_until,manual_grant_reason FROM atlas_licenses WHERE id=$1 FOR UPDATE", connection, transaction);
        command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new LicenseServerException("license_not_found", "Licença não encontrada.", 404);
        DateTimeOffset? revokedAt = reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1);
        DateTimeOffset? manualValidUntil = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2);
        return JsonSerializer.SerializeToDocument(new { generation = reader.GetInt32(0), revokedAt, manualValidUntil, manualGrantReason = reader.IsDBNull(3) ? null : reader.GetString(3) }, JsonOptions);
    }

    private static async Task<JsonDocument> ReadActivationStateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT a.license_id,a.revoked_at,a.generation,l.generation FROM atlas_activations a
            JOIN atlas_licenses l ON l.id=a.license_id
            WHERE a.id=$1 FOR UPDATE OF a,l
            """, connection, transaction);
        command.Parameters.AddWithValue(id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new LicenseServerException("activation_not_found", "Dispositivo não encontrado.", 404);
        DateTimeOffset? revokedAt = reader.IsDBNull(1) ? null : reader.GetFieldValue<DateTimeOffset>(1);
        return JsonSerializer.SerializeToDocument(new { licenseId = reader.GetString(0), revokedAt, activationGeneration = reader.GetInt32(2), licenseGeneration = reader.GetInt32(3) }, JsonOptions);
    }

    private async Task<JsonDocument> ReadCustomerStateAsync(Guid customerId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT s.id,s.status,s.paid_through,l.id,l.revoked_at,l.manual_valid_until
            FROM atlas_subscriptions s LEFT JOIN atlas_licenses l ON l.subscription_id=s.id
            WHERE s.customer_id=$1 ORDER BY s.created_at DESC LIMIT 1
            """, connection);
        command.Parameters.AddWithValue(customerId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new LicenseServerException("subscription_not_found", "Assinatura não encontrada.", 404);
        DateTimeOffset? paidThrough = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2);
        DateTimeOffset? revokedAt = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4);
        DateTimeOffset? manualValidUntil = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5);
        return JsonSerializer.SerializeToDocument(new
        {
            subscriptionId = reader.GetGuid(0), status = reader.GetString(1), paidThrough,
            licenseId = reader.IsDBNull(3) ? null : reader.GetString(3), revokedAt, manualValidUntil
        }, JsonOptions);
    }

    private async Task WriteAuditAsync(AdminPrincipal admin, string action, string targetType, string targetId, string reason, string outcome, string? ip, CancellationToken ct,
        JsonDocument? before = null, JsonDocument? after = null)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await AuditAsync(connection, transaction, admin, action, targetType, targetId, reason, before, after, outcome, ip, ct);
        await transaction.CommitAsync(ct);
    }

    private static async Task AuditAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, AdminPrincipal admin,
        string action, string targetType, string targetId, string reason, JsonDocument? before, JsonDocument? after,
        string outcome, string? ip, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO atlas_admin_audit_logs(id,admin_user_id,admin_email,action,target_type,target_id,reason,before_state,after_state,outcome,ip_address)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9::jsonb,$10,$11)
            """, connection, transaction);
        command.Parameters.AddWithValue(Guid.NewGuid()); command.Parameters.AddWithValue(admin.Id); command.Parameters.AddWithValue(admin.Email);
        command.Parameters.AddWithValue(action); command.Parameters.AddWithValue(targetType); command.Parameters.AddWithValue(targetId); command.Parameters.AddWithValue(reason);
        command.Parameters.AddWithValue(before?.RootElement.GetRawText() ?? "null"); command.Parameters.AddWithValue(after?.RootElement.GetRawText() ?? "null");
        command.Parameters.AddWithValue(outcome); command.Parameters.AddWithValue(ip ?? string.Empty);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken ct, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string ValidateReason(string? reason)
    {
        var value = reason?.Trim() ?? string.Empty;
        if (value.Length is < 3 or > 500) throw new LicenseServerException("reason_invalid", "Informe um motivo entre 3 e 500 caracteres.", 400);
        return value;
    }

    private static string NormalizeEmail(string? email)
    {
        var value = email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!value.Contains('@') || value.Length > 254) throw new LicenseServerException("email_invalid", "E-mail inválido.", 400);
        return value;
    }
    private static string ReadBearer(string? authorization)
    {
        if (authorization == null || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw new LicenseServerException("admin_authentication_required", "Autenticação administrativa necessária.", 401);
        var token = authorization[7..].Trim();
        if (token.Length < 40) throw new LicenseServerException("admin_authentication_required", "Sessão administrativa inválida.", 401);
        return token;
    }
    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static (DateTimeOffset?, Guid?) DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return (null, null);
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(cursor)).Split('|');
            return (DateTimeOffset.Parse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind), Guid.Parse(parts[1]));
        }
        catch { throw new LicenseServerException("cursor_invalid", "Cursor de paginação inválido.", 400); }
    }
}
