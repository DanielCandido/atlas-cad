using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Atlas.Licensing;
using Atlas.Licensing.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Features;
using Npgsql;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var licensingOptions = LicensingServerOptions.FromEnvironment(builder.Environment.ContentRootPath);
var commerceOptions = CommerceOptions.FromEnvironment(builder.Environment.ContentRootPath);
var libraryOptions = LibraryOptions.FromEnvironment(builder.Environment.ContentRootPath);
builder.Services.AddSingleton(licensingOptions);
builder.Services.AddSingleton(commerceOptions);
builder.Services.AddSingleton(libraryOptions);
builder.Services.Configure<FormOptions>(value => value.MultipartBodyLengthLimit = libraryOptions.MaximumUploadBytes);
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(commerceOptions.ConnectionString));
builder.Services.AddHostedService<DatabaseInitializer>();
builder.Services.AddSingleton<SigningKeyProvider>();
builder.Services.AddSingleton<PostgresLicensingStore>();
builder.Services.AddSingleton<CommerceStore>();
builder.Services.AddSingleton<InstallerPairingStore>();
builder.Services.AddSingleton<CatalogKeyProvider>();
builder.Services.AddSingleton<AdminStore>();
builder.Services.AddSingleton<LibraryStore>();
builder.Services.AddHttpClient<AsaasClient>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("license-write", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientPartitionKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("admin-login", context => RateLimitPartition.GetFixedWindowLimiter(
        ClientPartitionKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0
        }));
});

var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (LicenseServerException exception)
    {
        context.Response.StatusCode = exception.StatusCode;
        await context.Response.WriteAsJsonAsync(new LicenseApiError { ErrorCode = exception.Code, Message = exception.Message });
    }
});
app.UseRateLimiter();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "atlas-licensing-api" }));
app.MapGet("/api/products", async (LibraryStore libraries, CancellationToken ct) =>
    Results.Ok((await libraries.ListPublicAsync(ct)).Select(value => new { id = value.ProductId, name = value.Name, status = "active" })));
app.MapGet("/api/v1/libraries", async (LibraryStore libraries, CancellationToken ct) =>
    Results.Ok(await libraries.ListPublicAsync(ct)));
app.MapGet("/api/v1/libraries/{slug}", async (string slug, LibraryStore libraries, CancellationToken ct) =>
    Results.Ok(await libraries.GetPublicAsync(slug, ct)));
app.MapGet("/api/v1/libraries/{slug}/plans", async (string slug, LibraryStore libraries, CancellationToken ct) =>
    Results.Ok((await libraries.GetPublicAsync(slug, ct)).Plans));
app.MapGet("/api/v1/libraries/{slug}/docs", async (string slug, LibraryStore libraries, CancellationToken ct) =>
    Results.Ok((await libraries.GetPublicAsync(slug, ct)).Docs));
app.MapGet("/api/v1/libraries/{slug}/docs/{docSlug}", async (string slug, string docSlug, LibraryStore libraries, CancellationToken ct) =>
{
    var document = (await libraries.GetPublicAsync(slug, ct)).Docs.FirstOrDefault(value => value.Slug == docSlug);
    return document is null ? Results.NotFound() : Results.Ok(document);
});
app.MapGet("/api/v1/plans", async (LibraryStore libraries, CancellationToken ct) =>
    Results.Ok((await libraries.GetPublicAsync("steelflow", ct)).Plans));
app.MapGet("/api/v1/signing-key", (SigningKeyProvider signing) => Results.Ok(signing.PublicDescriptor));

app.MapPost("/api/v1/admin/auth/login", async (
    AdminLoginRequest request, AdminStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.LoginAsync(request, cancellationToken))).RequireRateLimiting("admin-login");

app.MapGet("/api/v1/admin/auth/me", async (
    HttpContext context, AdminStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken)));

app.MapPost("/api/v1/admin/auth/logout", async (
    HttpContext context, AdminStore store, CancellationToken cancellationToken) =>
{
    await store.LogoutAsync(context.Request.Headers.Authorization, cancellationToken);
    return Results.NoContent();
});

app.MapPut("/api/v1/admin/operators", async (
    HttpContext context, AdminBootstrapRequest request, AdminStore store,
    LicensingServerOptions options, CancellationToken cancellationToken) =>
{
    if (!AdminKeyMatches(context, options.AdminApiKey)) return Results.Unauthorized();
    await store.BootstrapAsync(request, cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapGet("/api/v1/admin/users", async (
    HttpContext context, AdminStore store, string? q, string? plan, string? subscriptionStatus,
    string? accessStatus, string? device, string? expiry, string? cursor, int? pageSize,
    CancellationToken cancellationToken) =>
{
    await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    return Results.Ok(await store.ListUsersAsync(q, plan, subscriptionStatus, accessStatus, device, expiry,
        cursor, pageSize ?? 50, cancellationToken));
});

app.MapGet("/api/v1/admin/users/{customerId:guid}", async (
    HttpContext context, Guid customerId, AdminStore store, CancellationToken cancellationToken) =>
{
    await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    return Results.Ok(await store.GetUserAsync(customerId, cancellationToken));
});

app.MapGet("/api/v1/admin/libraries", async (HttpContext context, AdminStore admins, LibraryStore libraries, CancellationToken ct) =>
{
    await admins.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    return Results.Ok(await libraries.ListAdminAsync(ct));
});
app.MapGet("/api/v1/admin/libraries/{id:guid}", async (HttpContext context, Guid id, AdminStore admins, LibraryStore libraries, CancellationToken ct) =>
{
    await admins.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    return Results.Ok(await libraries.GetAdminAsync(id, ct));
});
app.MapPost("/api/v1/admin/libraries", async (HttpContext context, LibraryUpsertRequest request, AdminStore admins, LibraryStore libraries, CancellationToken ct) =>
{
    var admin = await admins.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    return Results.Ok(await libraries.CreateAsync(admin, request, ClientIp(context), ct));
}).RequireRateLimiting("license-write");
app.MapPut("/api/v1/admin/libraries/{id:guid}", async (HttpContext context, Guid id, LibraryUpsertRequest request, AdminStore admins, LibraryStore libraries, CancellationToken ct) =>
{
    var admin = await admins.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    return Results.Ok(await libraries.UpdateAsync(admin, id, request, ClientIp(context), ct));
}).RequireRateLimiting("license-write");
app.MapPut("/api/v1/admin/libraries/{id:guid}/plans", async (HttpContext context, Guid id, LibraryPlanUpsertRequest request, AdminStore admins, LibraryStore libraries, CancellationToken ct) =>
{
    var admin = await admins.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    return Results.Ok(await libraries.UpsertPlanAsync(admin, id, request, ClientIp(context), ct));
}).RequireRateLimiting("license-write");
app.MapPut("/api/v1/admin/libraries/{id:guid}/docs", async (HttpContext context, Guid id, LibraryDocUpsertRequest request, AdminStore admins, LibraryStore libraries, CancellationToken ct) =>
{
    var admin = await admins.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    return Results.Ok(await libraries.UpsertDocAsync(admin, id, request, ClientIp(context), ct));
}).RequireRateLimiting("license-write");
app.MapDelete("/api/v1/admin/libraries/{id:guid}/plans/{code}", async (HttpContext context, Guid id, string code, AdminStore admins, LibraryStore libraries, CancellationToken ct) =>
{
    var admin = await admins.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    return Results.Ok(await libraries.DeletePlanAsync(admin, id, code, ClientIp(context), ct));
}).RequireRateLimiting("license-write");
app.MapDelete("/api/v1/admin/libraries/{id:guid}/docs/{slug}", async (HttpContext context, Guid id, string slug, AdminStore admins, LibraryStore libraries, CancellationToken ct) =>
{
    var admin = await admins.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    return Results.Ok(await libraries.DeleteDocAsync(admin, id, slug, ClientIp(context), ct));
}).RequireRateLimiting("license-write");
app.MapPost("/api/v1/admin/libraries/{id:guid}/releases", async (HttpContext context, Guid id, AdminStore admins, LibraryStore libraries, CancellationToken ct) =>
{
    var admin = await admins.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    var form = await context.Request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file") ?? throw new LicenseServerException("release_file_missing", "Selecione o instalador.", 400);
    return Results.Ok(await libraries.UploadReleaseAsync(admin, id, form["version"].ToString(), file, ClientIp(context), ct));
}).RequireRateLimiting("license-write");
app.MapPost("/api/v1/admin/libraries/{id:guid}/publish/{releaseId:guid}", async (HttpContext context, Guid id, Guid releaseId, AdminStore admins, LibraryStore libraries, CancellationToken ct) =>
{
    var admin = await admins.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    return Results.Ok(await libraries.PublishAsync(admin, id, releaseId, ClientIp(context), ct));
}).RequireRateLimiting("license-write");
app.MapPost("/api/v1/admin/libraries/{id:guid}/archive", async (HttpContext context, Guid id, AdminStore admins, LibraryStore libraries, CancellationToken ct) =>
{
    var admin = await admins.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    return Results.Ok(await libraries.ArchiveAsync(admin, id, ClientIp(context), ct));
}).RequireRateLimiting("license-write");

app.MapPut("/api/v1/admin/licenses/{licenseId}/manual-grant", async (
    HttpContext context, string licenseId, AdminManualGrantRequest request, AdminStore store, CancellationToken cancellationToken) =>
{
    var admin = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await store.GrantManualAsync(admin, licenseId, request, ClientIp(context), cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapDelete("/api/v1/admin/licenses/{licenseId}/manual-grant", async (
    HttpContext context, string licenseId, [FromBody] AdminReasonRequest request, AdminStore store, CancellationToken cancellationToken) =>
{
    var admin = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await store.ClearManualAsync(admin, licenseId, request, ClientIp(context), cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/admin/licenses/{licenseId}/revoke", async (
    HttpContext context, string licenseId, AdminReasonRequest request, AdminStore store, CancellationToken cancellationToken) =>
{
    var admin = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await store.RevokeLicenseAsync(admin, licenseId, request, ClientIp(context), cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/admin/licenses/{licenseId}/restore", async (
    HttpContext context, string licenseId, AdminReasonRequest request, AdminStore store, CancellationToken cancellationToken) =>
{
    var admin = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await store.RestoreLicenseAsync(admin, licenseId, request, ClientIp(context), cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/admin/activations/{activationId}/revoke", async (
    HttpContext context, string activationId, AdminReasonRequest request, AdminStore store, CancellationToken cancellationToken) =>
{
    var admin = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await store.RevokeActivationAsync(admin, activationId, request, ClientIp(context), cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/admin/users/{customerId:guid}/subscription/cancel", async (
    HttpContext context, Guid customerId, string? librarySlug, AdminReasonRequest request, AdminStore store, CancellationToken cancellationToken) =>
{
    var admin = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await store.CancelSubscriptionAsync(admin, customerId, librarySlug ?? "steelflow", request, ClientIp(context), cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/activations", async (
    ActivationRequest request, PostgresLicensingStore store, SigningKeyProvider signing, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(signing.Issue(await store.ActivateAsync(request, cancellationToken))); }
    catch (LicenseServerException exception)
    { return Results.Json(new LicenseApiError { ErrorCode = exception.Code, Message = exception.Message }, statusCode: exception.StatusCode); }
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/installer/pairings", async (
    InstallerPairingCreateRequest request, InstallerPairingStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.CreateAsync(request, cancellationToken))).RequireRateLimiting("license-write");

app.MapPost("/api/v1/installer/pairings/{pairingId}/poll", async (
    string pairingId, InstallerPairingProofRequest request, InstallerPairingStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.PollAsync(pairingId, request, cancellationToken))).RequireRateLimiting("license-write");

app.MapPost("/api/v1/installer/pairings/{pairingId}/complete", async (
    string pairingId, InstallerPairingProofRequest request, InstallerPairingStore store,
    SigningKeyProvider signing, CancellationToken cancellationToken) =>
    Results.Ok(signing.Issue(await store.CompleteAsync(pairingId, request, cancellationToken))))
    .RequireRateLimiting("license-write");

app.MapPost("/api/v1/renewals", async (
    RenewalRequest request, PostgresLicensingStore store, SigningKeyProvider signing, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(signing.Issue(await store.RenewAsync(request, cancellationToken))); }
    catch (LicenseServerException exception)
    { return Results.Json(new LicenseApiError { ErrorCode = exception.Code, Message = exception.Message }, statusCode: exception.StatusCode); }
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/catalog-keys", async (
    CatalogKeyRequest request, PostgresLicensingStore store, CatalogKeyProvider keys, CancellationToken cancellationToken) =>
{
    try
    {
        var expires = await store.AuthorizeCatalogKeyAsync(request, cancellationToken);
        return Results.Ok(keys.Release(request.KeyId, expires));
    }
    catch (LicenseServerException exception)
    { return Results.Json(new LicenseApiError { ErrorCode = exception.Code, Message = exception.Message }, statusCode: exception.StatusCode); }
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/admin/licenses", async (
    HttpContext context, CreateLicenseRequest request, PostgresLicensingStore store,
    LicensingServerOptions options, CancellationToken cancellationToken) =>
{
    if (!AdminKeyMatches(context, options.AdminApiKey)) return Results.Unauthorized();
    try { return Results.Ok(await store.CreateLicenseAsync(request, cancellationToken)); }
    catch (LicenseServerException exception)
    { return Results.Json(new LicenseApiError { ErrorCode = exception.Code, Message = exception.Message }, statusCode: exception.StatusCode); }
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/admin/licenses/{licenseId}/transfer", async (
    HttpContext context, string licenseId, PostgresLicensingStore store,
    LicensingServerOptions options, CancellationToken cancellationToken) =>
{
    if (!AdminKeyMatches(context, options.AdminApiKey)) return Results.Unauthorized();
    try { return Results.Ok(await store.TransferAsync(licenseId, cancellationToken)); }
    catch (LicenseServerException exception)
    { return Results.Json(new LicenseApiError { ErrorCode = exception.Code, Message = exception.Message }, statusCode: exception.StatusCode); }
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/commerce/checkout", async (RegisterSubscriptionRequest request, CommerceStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.RegisterAsync(request, cancellationToken))).RequireRateLimiting("license-write");

app.MapPost("/api/v1/commerce/login", async (LoginRequest request, CommerceStore store, CancellationToken cancellationToken) =>
    Results.Ok(await store.LoginAsync(request, cancellationToken))).RequireRateLimiting("license-write");

app.MapGet("/api/v1/portal", async (HttpContext context, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    return Results.Ok(await store.GetPortalAsync(customerId, cancellationToken));
});

app.MapPut("/api/v1/portal/plan", async (HttpContext context, ChangePlanRequest request, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    return Results.Ok(await store.ChangePlanAsync(customerId, request.LibrarySlug, request.PlanCode, cancellationToken));
}).RequireRateLimiting("license-write");

app.MapDelete("/api/v1/portal/subscription", async (HttpContext context, string? librarySlug, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await store.CancelAsync(customerId, librarySlug ?? "steelflow", cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/portal/subscription/retry", async (
    HttpContext context, RetrySubscriptionRequest request, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    return Results.Ok(await store.RetrySubscriptionAsync(customerId, request, cancellationToken));
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/portal/machines", async (HttpContext context, RegisterMachineRequest request, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    return Results.Ok(await store.RegisterMachineAsync(customerId, request.LibrarySlug, request.MachineIdentifier, cancellationToken));
}).RequireRateLimiting("license-write");

app.MapDelete("/api/v1/portal/machines", async (HttpContext context, string? librarySlug, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await store.DeactivateMachineAsync(customerId, librarySlug ?? "steelflow", cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapGet("/api/v1/portal/pairings/{userCode}", async (
    HttpContext context, string userCode, CommerceStore commerce, InstallerPairingStore pairings, CancellationToken cancellationToken) =>
{
    var customerId = await commerce.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    return Results.Ok(await pairings.GetPortalAsync(customerId, userCode, cancellationToken));
});

app.MapPost("/api/v1/portal/pairings/{userCode}/approve", async (
    HttpContext context, string userCode, PairingDecisionRequest request, CommerceStore commerce,
    InstallerPairingStore pairings, CancellationToken cancellationToken) =>
{
    var customerId = await commerce.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await pairings.ApproveAsync(customerId, userCode, request.ReplaceExisting, cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/portal/pairings/{userCode}/reject", async (
    HttpContext context, string userCode, CommerceStore commerce, InstallerPairingStore pairings, CancellationToken cancellationToken) =>
{
    var customerId = await commerce.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await pairings.RejectAsync(customerId, userCode, cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapGet("/api/v1/portal/installer", async (
    HttpContext context, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    var path = await store.RequireInstallerDownloadAsync(customerId, cancellationToken);
    return Results.File(path, "application/vnd.microsoft.portable-executable", "Instalador-AtlasCad.exe", enableRangeProcessing: true);
});

app.MapGet("/api/v1/portal/libraries/{slug}/installer", async (
    HttpContext context, string slug, CommerceStore commerce, LibraryStore libraries, CancellationToken ct) =>
{
    var customerId = await commerce.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    var download = await libraries.RequireDownloadAsync(customerId, slug, ct);
    context.Response.Headers.Append("X-Checksum-Sha256", download.Sha256);
    return Results.File(download.Path, "application/vnd.microsoft.portable-executable", download.DownloadName, enableRangeProcessing: true);
});

app.MapGet("/api/v1/portal/libraries/{slug}/uninstaller", async (
    HttpContext context, string slug, CommerceStore commerce, LibraryStore libraries, CancellationToken ct) =>
{
    var customerId = await commerce.AuthenticateAsync(context.Request.Headers.Authorization, ct);
    var download = await libraries.RequireDownloadAsync(customerId, slug, ct);
    return Results.File(download.Path, "application/vnd.microsoft.portable-executable", download.DownloadName.Replace("Instalador-", "Desinstalar-"), enableRangeProcessing: true);
});

app.MapGet("/api/v1/portal/uninstaller", async (
    HttpContext context, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    var path = await store.RequireUninstallerDownloadAsync(customerId, cancellationToken);
    return Results.File(path, "application/vnd.microsoft.portable-executable", "Desinstalar-AtlasCad.exe", enableRangeProcessing: true);
});

app.MapPost("/api/v1/webhooks/asaas", async (HttpContext context, CommerceStore store, CommerceOptions options, CancellationToken cancellationToken) =>
{
    if (!WebhookTokenMatches(context, options.AsaasWebhookToken)) return Results.Unauthorized();
    using var reader = new StreamReader(context.Request.Body);
    var raw = await reader.ReadToEndAsync(cancellationToken);
    var webhook = JsonSerializer.Deserialize<AsaasWebhook>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ??
        throw new LicenseServerException("webhook_invalid", "Webhook inválido.", 400);
    await store.ProcessWebhookAsync(raw, webhook, cancellationToken);
    return Results.Ok(new { received = true });
}).RequireRateLimiting("license-write");

app.Run();

static bool AdminKeyMatches(HttpContext context, string expected)
{
    if (!context.Request.Headers.TryGetValue("X-Atlas-Admin-Key", out var supplied)) return false;
    var left = System.Text.Encoding.UTF8.GetBytes(supplied.ToString());
    var right = System.Text.Encoding.UTF8.GetBytes(expected);
    return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
}

static bool WebhookTokenMatches(HttpContext context, string expected)
{
    if (!context.Request.Headers.TryGetValue("asaas-access-token", out var supplied)) return false;
    var left = System.Text.Encoding.UTF8.GetBytes(supplied.ToString());
    var right = System.Text.Encoding.UTF8.GetBytes(expected);
    return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
}

static string ClientPartitionKey(HttpContext context)
{
    if (context.Request.Headers.TryGetValue("CF-Connecting-IP", out var cloudflareIp) &&
        System.Net.IPAddress.TryParse(cloudflareIp.ToString(), out var parsed))
        return parsed.ToString();
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}

static string? ClientIp(HttpContext context)
{
    if (context.Request.Headers.TryGetValue("CF-Connecting-IP", out var cloudflareIp) &&
        System.Net.IPAddress.TryParse(cloudflareIp.ToString(), out var parsed)) return parsed.ToString();
    return context.Connection.RemoteIpAddress?.ToString();
}
