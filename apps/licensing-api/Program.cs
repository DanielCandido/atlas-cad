using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Atlas.Licensing;
using Atlas.Licensing.Api;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var licensingOptions = LicensingServerOptions.FromEnvironment(builder.Environment.ContentRootPath);
var commerceOptions = CommerceOptions.FromEnvironment(builder.Environment.ContentRootPath);
builder.Services.AddSingleton(licensingOptions);
builder.Services.AddSingleton(commerceOptions);
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(commerceOptions.ConnectionString));
builder.Services.AddHostedService<DatabaseInitializer>();
builder.Services.AddSingleton<SigningKeyProvider>();
builder.Services.AddSingleton<PostgresLicensingStore>();
builder.Services.AddSingleton<CommerceStore>();
builder.Services.AddSingleton<CatalogKeyProvider>();
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
app.MapGet("/api/products", () => Results.Ok(new[]
{
    new { id = LicensingProtocol.ProductId, name = "ATLAS MLINE PRO", status = "active" }
}));
app.MapGet("/api/v1/plans", (CommerceOptions options) => Results.Ok(options.Plans.Values.Select(plan => new
{
    code = plan.Code, name = plan.Name, price = plan.Price, cycle = plan.Cycle
})));
app.MapGet("/api/v1/signing-key", (SigningKeyProvider signing) => Results.Ok(signing.PublicDescriptor));

app.MapPost("/api/v1/activations", async (
    ActivationRequest request, PostgresLicensingStore store, SigningKeyProvider signing, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(signing.Issue(await store.ActivateAsync(request, cancellationToken))); }
    catch (LicenseServerException exception)
    { return Results.Json(new LicenseApiError { ErrorCode = exception.Code, Message = exception.Message }, statusCode: exception.StatusCode); }
}).RequireRateLimiting("license-write");

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
    return Results.Ok(await store.ChangePlanAsync(customerId, request.PlanCode, cancellationToken));
}).RequireRateLimiting("license-write");

app.MapDelete("/api/v1/portal/subscription", async (HttpContext context, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await store.CancelAsync(customerId, cancellationToken);
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
    return Results.Ok(await store.RegisterMachineAsync(customerId, request.MachineIdentifier, cancellationToken));
}).RequireRateLimiting("license-write");

app.MapDelete("/api/v1/portal/machines", async (HttpContext context, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    await store.DeactivateMachineAsync(customerId, cancellationToken);
    return Results.NoContent();
}).RequireRateLimiting("license-write");

app.MapGet("/api/v1/portal/download", async (HttpContext context, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    var path = await store.RequireDownloadAsync(customerId, cancellationToken);
    return Results.File(path, "application/zip", Path.GetFileName(path), enableRangeProcessing: true);
});

app.MapGet("/api/v1/portal/download/{edition}", async (
    HttpContext context, string edition, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    var path = await store.RequireDownloadAsync(customerId, edition, cancellationToken);
    return Results.File(path, "application/zip", Path.GetFileName(path), enableRangeProcessing: true);
});

app.MapGet("/api/v1/portal/machine-identifier", async (
    HttpContext context, CommerceStore store, CancellationToken cancellationToken) =>
{
    var customerId = await store.AuthenticateAsync(context.Request.Headers.Authorization, cancellationToken);
    var path = await store.RequireMachineIdentifierDownloadAsync(customerId, cancellationToken);
    return Results.File(
        path,
        "application/vnd.microsoft.portable-executable",
        "AtlasCad-Identificador.exe",
        enableRangeProcessing: true);
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
