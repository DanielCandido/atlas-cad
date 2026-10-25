using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

namespace Atlas.Licensing.Api;

public sealed partial class LibraryStore(NpgsqlDataSource dataSource, LibraryOptions options, CommerceOptions commerceOptions)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<LibraryView>> ListPublicAsync(CancellationToken ct) =>
        await ListAsync("WHERE l.status='published'", true, ct);

    public async Task<IReadOnlyList<LibraryView>> ListAdminAsync(CancellationToken ct) =>
        await ListAsync(string.Empty, false, ct);

    public async Task<LibraryView> GetPublicAsync(string slug, CancellationToken ct) =>
        (await ListAsync("WHERE l.status='published' AND l.slug=$1", true, ct, NormalizeSlug(slug))).SingleOrDefault() ??
        throw new LicenseServerException("library_not_found", "Biblioteca nao encontrada.", 404);

    public async Task<LibraryView> GetAdminAsync(Guid id, CancellationToken ct) =>
        (await ListAsync("WHERE l.id=$1", false, ct, id)).SingleOrDefault() ??
        throw new LicenseServerException("library_not_found", "Biblioteca nao encontrada.", 404);

    public async Task<LibraryView> CreateAsync(AdminPrincipal admin, LibraryUpsertRequest request, string? ip, CancellationToken ct)
    {
        Validate(request);
        var id = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            await using var command = new NpgsqlCommand("""
                INSERT INTO atlas_libraries(id,slug,name,product_id,module_code,catalog_id,summary,description,features,requirements)
                VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9::jsonb,$10::jsonb)
                """, connection, transaction);
            Add(command, id, NormalizeSlug(request.Slug), request.Name.Trim(), NormalizeCode(request.ProductId),
                NormalizeCode(request.ModuleCode), EmptyToNull(request.CatalogId), request.Summary.Trim(),
                NormalizeMultiline(request.Description).Trim(),
                JsonSerializer.Serialize(request.Features.Select(NormalizeMultiline), Json),
                JsonSerializer.Serialize(request.Requirements.Select(NormalizeMultiline), Json));
            await command.ExecuteNonQueryAsync(ct);
            await AuditAsync(connection, transaction, admin, "library_created", id.ToString(), "Biblioteca criada.", ip, ct);
            await transaction.CommitAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new LicenseServerException("library_conflict", "Slug, produto ou modulo ja cadastrado.", 409); }
        return await GetAdminAsync(id, ct);
    }

    public async Task<LibraryView> UpdateAsync(AdminPrincipal admin, Guid id, LibraryUpsertRequest request, string? ip, CancellationToken ct)
    {
        Validate(request);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var current = new NpgsqlCommand("SELECT status,slug FROM atlas_libraries WHERE id=$1 FOR UPDATE", connection, transaction);
        current.Parameters.AddWithValue(id);
        await using var reader = await current.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new LicenseServerException("library_not_found", "Biblioteca nao encontrada.", 404);
        var status = reader.GetString(0); var existingSlug = reader.GetString(1); await reader.DisposeAsync();
        var slug = NormalizeSlug(request.Slug);
        if (status != "draft" && slug != existingSlug)
            throw new LicenseServerException("library_slug_immutable", "O slug nao pode mudar depois da publicacao.", 409);
        await using var command = new NpgsqlCommand("""
            UPDATE atlas_libraries SET slug=$1,name=$2,product_id=$3,module_code=$4,catalog_id=$5,summary=$6,
                description=$7,features=$8::jsonb,requirements=$9::jsonb,updated_at=now() WHERE id=$10
            """, connection, transaction);
        Add(command, slug, request.Name.Trim(), NormalizeCode(request.ProductId), NormalizeCode(request.ModuleCode),
            EmptyToNull(request.CatalogId), request.Summary.Trim(), NormalizeMultiline(request.Description).Trim(),
            JsonSerializer.Serialize(request.Features.Select(NormalizeMultiline), Json),
            JsonSerializer.Serialize(request.Requirements.Select(NormalizeMultiline), Json), id);
        await command.ExecuteNonQueryAsync(ct);
        await AuditAsync(connection, transaction, admin, "library_updated", id.ToString(), "Biblioteca atualizada.", ip, ct);
        await transaction.CommitAsync(ct);
        return await GetAdminAsync(id, ct);
    }

    public async Task<LibraryView> UpsertPlanAsync(AdminPrincipal admin, Guid libraryId, LibraryPlanUpsertRequest request, string? ip, CancellationToken ct)
    {
        var code = NormalizeCode(request.Code);
        var cycle = request.Cycle.Trim().ToUpperInvariant();
        if (request.Name.Trim().Length is < 2 or > 120 || request.Price <= 0 || cycle is not ("MONTHLY" or "YEARLY"))
            throw new LicenseServerException("plan_invalid", "Dados do plano invalidos.", 400);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await EnsureLibraryAsync(connection, transaction, libraryId, ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO atlas_library_plans(id,library_id,code,name,price,cycle,active)
            VALUES ($1,$2,$3,$4,$5,$6,$7)
            ON CONFLICT (library_id,code) DO UPDATE SET name=EXCLUDED.name,price=EXCLUDED.price,cycle=EXCLUDED.cycle,
                active=EXCLUDED.active,updated_at=now()
            """, connection, transaction);
        Add(command, Guid.NewGuid(), libraryId, code, request.Name.Trim(), request.Price, cycle, request.Active);
        await command.ExecuteNonQueryAsync(ct);
        await AuditAsync(connection, transaction, admin, "library_plan_upserted", libraryId.ToString(), code, ip, ct);
        await transaction.CommitAsync(ct);
        return await GetAdminAsync(libraryId, ct);
    }

    public async Task<LibraryView> UpsertDocAsync(AdminPrincipal admin, Guid libraryId, LibraryDocUpsertRequest request, string? ip, CancellationToken ct)
    {
        var slug = NormalizeSlug(request.Slug);
        var status = request.Status.Trim().ToLowerInvariant();
        if (request.Title.Trim().Length is < 2 or > 160 || request.Summary.Trim().Length is < 2 or > 300 ||
            request.Group.Trim().Length is < 2 or > 100 || request.Markdown.Length is < 1 or > 200_000 || status is not ("draft" or "published"))
            throw new LicenseServerException("document_invalid", "Dados da documentacao invalidos.", 400);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await EnsureLibraryAsync(connection, transaction, libraryId, ct);
        await using var command = new NpgsqlCommand("""
            INSERT INTO atlas_library_docs(id,library_id,slug,title,summary,group_name,position,markdown,status)
            VALUES ($1,$2,$3,$4,$5,$6,$7,$8,$9)
            ON CONFLICT (library_id,slug) DO UPDATE SET title=EXCLUDED.title,summary=EXCLUDED.summary,
                group_name=EXCLUDED.group_name,position=EXCLUDED.position,markdown=EXCLUDED.markdown,status=EXCLUDED.status,updated_at=now()
            """, connection, transaction);
        Add(command, Guid.NewGuid(), libraryId, slug, request.Title.Trim(), request.Summary.Trim(), request.Group.Trim(),
            request.Position, NormalizeMultiline(request.Markdown), status);
        await command.ExecuteNonQueryAsync(ct);
        await AuditAsync(connection, transaction, admin, "library_doc_upserted", libraryId.ToString(), slug, ip, ct);
        await transaction.CommitAsync(ct);
        return await GetAdminAsync(libraryId, ct);
    }

    public async Task<LibraryView> DeletePlanAsync(AdminPrincipal admin, Guid libraryId, string code, string? ip, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("DELETE FROM atlas_library_plans WHERE library_id=$1 AND code=$2", connection, transaction);
        Add(command, libraryId, NormalizeCode(code));
        if (await command.ExecuteNonQueryAsync(ct) == 0) throw new LicenseServerException("plan_not_found", "Plano nao encontrado.", 404);
        await EnsurePublishedContentAsync(connection, transaction, libraryId, "atlas_library_plans", "active", "A biblioteca publicada precisa manter um plano ativo.", ct);
        await AuditAsync(connection, transaction, admin, "library_plan_deleted", libraryId.ToString(), code, ip, ct);
        await transaction.CommitAsync(ct);
        return await GetAdminAsync(libraryId, ct);
    }

    public async Task<LibraryView> DeleteDocAsync(AdminPrincipal admin, Guid libraryId, string slug, string? ip, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("DELETE FROM atlas_library_docs WHERE library_id=$1 AND slug=$2", connection, transaction);
        Add(command, libraryId, NormalizeSlug(slug));
        if (await command.ExecuteNonQueryAsync(ct) == 0) throw new LicenseServerException("document_not_found", "Pagina nao encontrada.", 404);
        await EnsurePublishedContentAsync(connection, transaction, libraryId, "atlas_library_docs", "status='published'", "A biblioteca publicada precisa manter uma pagina publicada.", ct);
        await AuditAsync(connection, transaction, admin, "library_doc_deleted", libraryId.ToString(), slug, ip, ct);
        await transaction.CommitAsync(ct);
        return await GetAdminAsync(libraryId, ct);
    }

    public async Task<LibraryView> UploadReleaseAsync(AdminPrincipal admin, Guid libraryId, string version,
        IFormFile file, string? ip, CancellationToken ct)
    {
        if (!VersionRegex().IsMatch(version)) throw new LicenseServerException("release_version_invalid", "Use versao major.minor.patch.", 400);
        if (file.Length < 512 || file.Length > options.MaximumUploadBytes ||
            !Path.GetExtension(file.FileName).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new LicenseServerException("release_file_invalid", "Envie um executavel dentro do limite permitido.", 400);
        var root = Path.GetFullPath(options.StoragePath);
        Directory.CreateDirectory(root);
        var staging = Path.Combine(root, ".upload-" + Guid.NewGuid().ToString("N") + ".tmp");
        string? destination = null;
        var createdDestination = false;
        var committed = false;
        try
        {
            await using (var output = new FileStream(staging, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920, FileOptions.Asynchronous))
                await file.CopyToAsync(output, ct);
            var (hash, signed) = ValidateExecutable(staging);
            if (!signed) throw new LicenseServerException("release_unsigned", "O instalador precisa conter assinatura Authenticode.", 400);
            var folder = Path.Combine(root, libraryId.ToString("N"));
            Directory.CreateDirectory(folder);
            destination = Path.Combine(folder, version + "-" + hash[..16] + ".exe");
            File.Move(staging, destination, false);
            createdDestination = true;
            var releaseId = Guid.NewGuid();
            await using var connection = await dataSource.OpenConnectionAsync(ct);
            await using var transaction = await connection.BeginTransactionAsync(ct);
            await EnsureLibraryAsync(connection, transaction, libraryId, ct);
            await using var command = new NpgsqlCommand("""
                INSERT INTO atlas_library_releases(id,library_id,version,artifact_path,sha256,size_bytes,signature_present)
                VALUES ($1,$2,$3,$4,$5,$6,true)
                """, connection, transaction);
            Add(command, releaseId, libraryId, version, destination, hash, file.Length);
            await command.ExecuteNonQueryAsync(ct);
            await AuditAsync(connection, transaction, admin, "library_release_uploaded", releaseId.ToString(), version, ip, ct);
            await transaction.CommitAsync(ct);
            committed = true;
            return await GetAdminAsync(libraryId, ct);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        { throw new LicenseServerException("release_version_conflict", "Esta versao ja foi enviada.", 409); }
        catch (UnauthorizedAccessException)
        { throw new LicenseServerException("release_storage_unavailable", "O armazenamento de releases nao permite gravacao.", 503); }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
            if (!committed && createdDestination && destination is not null && File.Exists(destination)) File.Delete(destination);
        }
    }

    public async Task<LibraryView> PublishAsync(AdminPrincipal admin, Guid libraryId, Guid releaseId, string? ip, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await EnsureLibraryAsync(connection, transaction, libraryId, ct);
        await using var checks = new NpgsqlCommand("""
            SELECT EXISTS(SELECT 1 FROM atlas_library_plans WHERE library_id=$1 AND active),
                   EXISTS(SELECT 1 FROM atlas_library_docs WHERE library_id=$1 AND status='published'),
                   EXISTS(SELECT 1 FROM atlas_library_releases WHERE id=$2 AND library_id=$1)
            """, connection, transaction);
        checks.Parameters.AddWithValue(libraryId); checks.Parameters.AddWithValue(releaseId);
        await using var reader = await checks.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct); var valid = reader.GetBoolean(0) && reader.GetBoolean(1) && reader.GetBoolean(2); await reader.DisposeAsync();
        if (!valid) throw new LicenseServerException("library_publish_incomplete", "Publique uma pagina, ative um plano e envie uma versao valida.", 409);
        await using var command = new NpgsqlCommand("""
            UPDATE atlas_libraries SET status='published',active_release_id=$1,published_at=COALESCE(published_at,now()),updated_at=now() WHERE id=$2;
            UPDATE atlas_library_releases SET published_at=COALESCE(published_at,now()) WHERE id=$1;
            """, connection, transaction);
        Add(command, releaseId, libraryId); await command.ExecuteNonQueryAsync(ct);
        await AuditAsync(connection, transaction, admin, "library_published", libraryId.ToString(), releaseId.ToString(), ip, ct);
        await transaction.CommitAsync(ct);
        return await GetAdminAsync(libraryId, ct);
    }

    public async Task<LibraryView> ArchiveAsync(AdminPrincipal admin, Guid libraryId, string? ip, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var command = new NpgsqlCommand("UPDATE atlas_libraries SET status='archived',updated_at=now() WHERE id=$1", connection, transaction);
        command.Parameters.AddWithValue(libraryId);
        if (await command.ExecuteNonQueryAsync(ct) == 0) throw new LicenseServerException("library_not_found", "Biblioteca nao encontrada.", 404);
        await AuditAsync(connection, transaction, admin, "library_archived", libraryId.ToString(), "Biblioteca arquivada.", ip, ct);
        await transaction.CommitAsync(ct);
        return await GetAdminAsync(libraryId, ct);
    }

    public async Task<LibraryDownload> RequireDownloadAsync(Guid customerId, string slug, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("""
            SELECT r.artifact_path,l.name,r.sha256
            FROM atlas_libraries l
            LEFT JOIN atlas_library_releases r ON r.id=l.active_release_id
            JOIN atlas_subscriptions s ON s.library_id=l.id AND s.customer_id=$1
            LEFT JOIN atlas_licenses x ON x.subscription_id=s.id
            WHERE l.slug=$2 AND l.status='published' AND x.revoked_at IS NULL
              AND ((s.status='active' AND s.paid_through>now()) OR x.manual_valid_until>now())
            ORDER BY s.created_at DESC LIMIT 1
            """, connection);
        command.Parameters.AddWithValue(customerId); command.Parameters.AddWithValue(NormalizeSlug(slug));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new LicenseServerException("download_not_entitled", "Download nao liberado para esta biblioteca.", 403);
        var path = reader.IsDBNull(0) && slug.Equals("steelflow", StringComparison.OrdinalIgnoreCase)
            ? commerceOptions.InstallerPath : reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
        var name = reader.GetString(1);
        var hash = reader.IsDBNull(2) && File.Exists(path)
            ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))
            : reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
        if (!File.Exists(path)) throw new LicenseServerException("download_unavailable", "Instalador indisponivel.", 503);
        return new LibraryDownload(path, $"Instalador-{SafeFileName(name)}.exe", hash);
    }

    public async Task<(Guid Id, string ProductId, string ModuleCode)> ResolveAsync(string slug, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand("SELECT id,product_id,module_code FROM atlas_libraries WHERE slug=$1 AND status='published'", connection);
        command.Parameters.AddWithValue(NormalizeSlug(slug));
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new LicenseServerException("library_not_found", "Biblioteca nao encontrada.", 404);
        return (reader.GetGuid(0), reader.GetString(1), reader.GetString(2));
    }

    private async Task<IReadOnlyList<LibraryView>> ListAsync(string where, bool publicOnly, CancellationToken ct, params object[] values)
    {
        var result = new List<LibraryView>();
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var command = new NpgsqlCommand($"""
            SELECT l.id,l.slug,l.name,l.product_id,l.module_code,l.catalog_id,l.summary,l.description,l.features,l.requirements,
                   l.status,l.image_path,l.published_at
            FROM atlas_libraries l {where} ORDER BY l.name
            """, connection);
        Add(command, values);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var rows = new List<(Guid,string,string,string,string,string?,string,string,string,string,string,string?,DateTimeOffset?)>();
        while (await reader.ReadAsync(ct)) rows.Add((reader.GetGuid(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),reader.GetString(4),
            reader.IsDBNull(5)?null:reader.GetString(5),reader.GetString(6),reader.GetString(7),reader.GetString(8),reader.GetString(9),reader.GetString(10),
            reader.IsDBNull(11)?null:reader.GetString(11),reader.IsDBNull(12)?null:reader.GetFieldValue<DateTimeOffset>(12)));
        await reader.DisposeAsync();
        foreach (var row in rows)
        {
            var plans = new List<LibraryPlanView>();
            await using (var c = new NpgsqlCommand("SELECT id,code,name,price,cycle,active FROM atlas_library_plans WHERE library_id=$1" + (publicOnly ? " AND active" : "") + " ORDER BY price", connection))
            { c.Parameters.AddWithValue(row.Item1); await using var r=await c.ExecuteReaderAsync(ct); while(await r.ReadAsync(ct)) plans.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetDecimal(3),r.GetString(4),r.GetBoolean(5))); }
            var docs = new List<LibraryDocView>();
            await using (var c = new NpgsqlCommand("SELECT id,slug,title,summary,group_name,position,markdown,status FROM atlas_library_docs WHERE library_id=$1" + (publicOnly ? " AND status='published'" : "") + " ORDER BY position,title", connection))
            { c.Parameters.AddWithValue(row.Item1); await using var r=await c.ExecuteReaderAsync(ct); while(await r.ReadAsync(ct)) docs.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetInt32(5),NormalizeMultiline(r.GetString(6)),r.GetString(7))); }
            var releases = new List<LibraryReleaseView>();
            if (!publicOnly) await using (var c = new NpgsqlCommand("SELECT id,version,sha256,size_bytes,signature_present,created_at,published_at,id=(SELECT active_release_id FROM atlas_libraries WHERE id=$1) FROM atlas_library_releases WHERE library_id=$1 ORDER BY created_at DESC", connection))
            { c.Parameters.AddWithValue(row.Item1); await using var r=await c.ExecuteReaderAsync(ct); while(await r.ReadAsync(ct)) releases.Add(new(r.GetGuid(0),r.GetString(1),r.GetString(2),r.GetInt64(3),r.GetBoolean(4),r.GetFieldValue<DateTimeOffset>(5),r.IsDBNull(6)?null:r.GetFieldValue<DateTimeOffset>(6),r.GetBoolean(7))); }
            result.Add(new(row.Item1,row.Item2,row.Item3,row.Item4,row.Item5,row.Item6,row.Item7,NormalizeMultiline(row.Item8),
                (JsonSerializer.Deserialize<List<string>>(row.Item9,Json)??[]).Select(NormalizeMultiline).ToList(),
                (JsonSerializer.Deserialize<List<string>>(row.Item10,Json)??[]).Select(NormalizeMultiline).ToList(),
                row.Item11,row.Item12 is null?null:$"/api/library-assets/{row.Item1:N}",row.Item13,plans,docs,releases));
        }
        return result;
    }

    private static (string Hash, bool Signed) ValidateExecutable(string path)
    {
        using var stream = File.OpenRead(path);
        if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z') throw new LicenseServerException("release_file_invalid", "Executavel PE invalido.", 400);
        stream.Position = 0;
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var signed = pe.PEHeaders.PEHeader?.CertificateTableDirectory.Size > 0;
        stream.Position = 0;
        return (Convert.ToHexString(SHA256.HashData(stream)), signed);
    }

    private static async Task EnsureLibraryAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid id, CancellationToken ct)
    { await using var q=new NpgsqlCommand("SELECT 1 FROM atlas_libraries WHERE id=$1 FOR UPDATE",c,t);q.Parameters.AddWithValue(id);if(await q.ExecuteScalarAsync(ct) is null)throw new LicenseServerException("library_not_found","Biblioteca nao encontrada.",404); }
    private static async Task EnsurePublishedContentAsync(NpgsqlConnection c, NpgsqlTransaction t, Guid id, string table, string predicate, string message, CancellationToken ct)
    { await using var q=new NpgsqlCommand($"SELECT status,(SELECT count(*) FROM {table} WHERE library_id=$1 AND {predicate}) FROM atlas_libraries WHERE id=$1",c,t);q.Parameters.AddWithValue(id);await using var r=await q.ExecuteReaderAsync(ct);await r.ReadAsync(ct);if(r.GetString(0)=="published"&&r.GetInt64(1)==0)throw new LicenseServerException("library_publish_incomplete",message,409); }
    private static async Task AuditAsync(NpgsqlConnection c,NpgsqlTransaction t,AdminPrincipal admin,string action,string target,string reason,string? ip,CancellationToken ct)
    { await using var q=new NpgsqlCommand("INSERT INTO atlas_admin_audit_logs(id,admin_user_id,admin_email,action,target_type,target_id,reason,outcome,ip_address) VALUES ($1,$2,$3,$4,'library',$5,$6,'succeeded',$7)",c,t);Add(q,Guid.NewGuid(),admin.Id,admin.Email,action,target,reason,ip??string.Empty);await q.ExecuteNonQueryAsync(ct); }
    private static void Add(NpgsqlCommand command, params object?[] values) { foreach(var value in values) command.Parameters.AddWithValue(value ?? DBNull.Value); }
    private static void Validate(LibraryUpsertRequest r) { if(r is null||r.Name.Trim().Length is <2 or >120||r.Summary.Trim().Length is <2 or >300||r.Description.Trim().Length is <2 or >5000)throw new LicenseServerException("library_invalid","Dados da biblioteca invalidos.",400);_ = NormalizeSlug(r.Slug);_ = NormalizeCode(r.ProductId);_ = NormalizeCode(r.ModuleCode); }
    private static string NormalizeSlug(string value) { var v=(value??string.Empty).Trim().ToLowerInvariant();if(!SlugRegex().IsMatch(v))throw new LicenseServerException("slug_invalid","Use apenas letras minusculas, numeros e hifens.",400);return v; }
    private static string NormalizeCode(string value) { var v=(value??string.Empty).Trim().ToLowerInvariant();if(!SlugRegex().IsMatch(v))throw new LicenseServerException("code_invalid","Identificador invalido.",400);return v; }
    private static string NormalizeMultiline(string value) => (value ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Replace("\\n", "\n");
    private static string? EmptyToNull(string? value)=>string.IsNullOrWhiteSpace(value)?null:NormalizeCode(value);
    private static string SafeFileName(string value)=>Regex.Replace(value,"[^A-Za-z0-9_-]","-");
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$",RegexOptions.CultureInvariant)] private static partial Regex SlugRegex();
    [GeneratedRegex("^[0-9]+\\.[0-9]+\\.[0-9]+$",RegexOptions.CultureInvariant)] private static partial Regex VersionRegex();
}
