using System.Text.Json.Serialization;

namespace Atlas.Licensing.Api;

public sealed class RegisterSubscriptionRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PlanCode { get; set; } = string.Empty;
    public string BillingType { get; set; } = "PIX";
    public string Phone { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string AddressNumber { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class ChangePlanRequest { public string PlanCode { get; set; } = string.Empty; }
public sealed class RetrySubscriptionRequest
{
    public string TaxId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string AddressNumber { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
}
public sealed class RegisterMachineRequest { public string MachineIdentifier { get; set; } = string.Empty; }

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
public sealed class LicenseServerException : Exception
{
    public LicenseServerException(string code, string message, int statusCode) : base(message)
    { Code = code; StatusCode = statusCode; }
    public string Code { get; }
    public int StatusCode { get; }
}

public sealed record AuthResponse(string SessionToken, DateTimeOffset ExpiresAt, PortalSnapshot Portal);
public sealed record CheckoutResponse(string SessionToken, DateTimeOffset ExpiresAt, string? PaymentUrl, PortalSnapshot Portal);
public sealed record MachineActivationResponse(string ActivationCode, DateTimeOffset ExpiresAt, string MachineFingerprint);
public sealed record SubscriptionSummary(
    Guid Id,
    string PlanCode,
    string PlanName,
    string Status,
    string BillingType,
    DateTimeOffset? PaidThrough,
    string? PaymentUrl,
    DateTimeOffset CreatedAt,
    bool IsCurrent,
    bool CanRetry);
public sealed record PortalSnapshot(
    string CustomerName,
    string Email,
    string PlanCode,
    string PlanName,
    string SubscriptionStatus,
    string? BillingType,
    DateTimeOffset? PaidThrough,
    bool CanDownload,
    bool CanActivate,
    string? PaymentUrl,
    string? LicenseId,
    string? MachineFingerprint,
    IReadOnlyList<SubscriptionSummary> Subscriptions);

public sealed class AsaasWebhook
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("event")] public string Event { get; set; } = string.Empty;
    [JsonPropertyName("payment")] public AsaasPayment? Payment { get; set; }
    [JsonPropertyName("subscription")] public AsaasSubscription? Subscription { get; set; }
    [JsonPropertyName("checkout")] public AsaasCheckout? Checkout { get; set; }
}

public sealed class AsaasPayment
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("subscription")] public string? SubscriptionId { get; set; }
    [JsonPropertyName("customer")] public string? CustomerId { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("invoiceUrl")] public string? InvoiceUrl { get; set; }
    [JsonPropertyName("dueDate")] public string? DueDate { get; set; }
    [JsonPropertyName("billingType")] public string? BillingType { get; set; }
}

public sealed class AsaasSubscription
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("customer")] public string? CustomerId { get; set; }
    [JsonPropertyName("billingType")] public string? BillingType { get; set; }
    [JsonPropertyName("externalReference")] public string? ExternalReference { get; set; }
}

public sealed class AsaasCheckout
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("customer")] public string? CustomerId { get; set; }
    [JsonPropertyName("externalReference")] public string? ExternalReference { get; set; }
}

public sealed class MachineIdentifierPayload
{
    [JsonPropertyName("v")] public int Version { get; set; }
    [JsonPropertyName("x")] public string X { get; set; } = string.Empty;
    [JsonPropertyName("y")] public string Y { get; set; } = string.Empty;
    [JsonPropertyName("n")] public string Nonce { get; set; } = string.Empty;
    [JsonPropertyName("p")] public string Proof { get; set; } = string.Empty;
}
