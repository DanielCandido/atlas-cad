using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Atlas.Licensing;
using Atlas.Licensing.Api;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(LicensingServerOptions.FromEnvironment(builder.Environment.ContentRootPath));
builder.Services.AddSingleton<SigningKeyProvider>();
builder.Services.AddSingleton<LicensingStateStore>();
builder.Services.AddSingleton<CatalogKeyProvider>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("license-write", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();
app.UseRateLimiter();
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "atlas-licensing-api" }));
app.MapGet("/api/products", () => Results.Ok(new[]
{
    new { id = LicensingProtocol.ProductId, name = "ATLAS MLINE PRO", status = "active" }
}));
app.MapGet("/api/v1/signing-key", (SigningKeyProvider signing) => Results.Ok(signing.PublicDescriptor));

app.MapPost("/api/v1/activations", async (
    ActivationRequest request, LicensingStateStore store, SigningKeyProvider signing, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(signing.Issue(await store.ActivateAsync(request, cancellationToken))); }
    catch (LicenseServerException exception)
    { return Results.Json(new LicenseApiError { ErrorCode = exception.Code, Message = exception.Message }, statusCode: exception.StatusCode); }
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/renewals", async (
    RenewalRequest request, LicensingStateStore store, SigningKeyProvider signing, CancellationToken cancellationToken) =>
{
    try { return Results.Ok(signing.Issue(await store.RenewAsync(request, cancellationToken))); }
    catch (LicenseServerException exception)
    { return Results.Json(new LicenseApiError { ErrorCode = exception.Code, Message = exception.Message }, statusCode: exception.StatusCode); }
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/catalog-keys", async (
    CatalogKeyRequest request, LicensingStateStore store, CatalogKeyProvider keys, CancellationToken cancellationToken) =>
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
    HttpContext context, CreateLicenseRequest request, LicensingStateStore store,
    LicensingServerOptions options, CancellationToken cancellationToken) =>
{
    if (!AdminKeyMatches(context, options.AdminApiKey)) return Results.Unauthorized();
    try { return Results.Ok(await store.CreateLicenseAsync(request, cancellationToken)); }
    catch (LicenseServerException exception)
    { return Results.Json(new LicenseApiError { ErrorCode = exception.Code, Message = exception.Message }, statusCode: exception.StatusCode); }
}).RequireRateLimiting("license-write");

app.MapPost("/api/v1/admin/licenses/{licenseId}/transfer", async (
    HttpContext context, string licenseId, LicensingStateStore store,
    LicensingServerOptions options, CancellationToken cancellationToken) =>
{
    if (!AdminKeyMatches(context, options.AdminApiKey)) return Results.Unauthorized();
    try { return Results.Ok(await store.TransferAsync(licenseId, cancellationToken)); }
    catch (LicenseServerException exception)
    { return Results.Json(new LicenseApiError { ErrorCode = exception.Code, Message = exception.Message }, statusCode: exception.StatusCode); }
}).RequireRateLimiting("license-write");

app.Run();

static bool AdminKeyMatches(HttpContext context, string expected)
{
    if (!context.Request.Headers.TryGetValue("X-Atlas-Admin-Key", out var supplied)) return false;
    var left = System.Text.Encoding.UTF8.GetBytes(supplied.ToString());
    var right = System.Text.Encoding.UTF8.GetBytes(expected);
    return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
}
