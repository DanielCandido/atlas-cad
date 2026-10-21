namespace Atlas.Licensing.Api;

public sealed class AdminLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class AdminBootstrapRequest
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AdminReasonRequest { public string Reason { get; set; } = string.Empty; }
public sealed class AdminManualGrantRequest : AdminReasonRequest { public DateTimeOffset ValidUntil { get; set; } }

public sealed record AdminPrincipal(Guid Id, string Name, string Email);
public sealed record AdminAuthResponse(string SessionToken, DateTimeOffset ExpiresAt, AdminPrincipal Admin);
public sealed record AdminUserListResponse(IReadOnlyList<AdminUserSummary> Items, string? NextCursor);
public sealed record AdminUserSummary(
    Guid Id, string Name, string Email, DateTimeOffset CreatedAt,
    string? PlanCode, string? PlanName, string? SubscriptionStatus,
    string? LicenseId, string AccessStatus, DateTimeOffset? AccessExpiresAt,
    string? AccessSource, string? DeviceFingerprint, DateTimeOffset? DeviceLastRenewedAt);
public sealed record AdminSubscriptionSummary(
    Guid Id, string LibrarySlug, string LibraryName, string PlanCode, string PlanName, string Status, string BillingType,
    DateTimeOffset? PaidThrough, DateTimeOffset CreatedAt, bool IsCurrent);
public sealed record AdminLicenseSummary(
    string Id, int Generation, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt,
    DateTimeOffset? ManualValidUntil, string? ManualGrantReason,
    string AccessStatus, string? AccessSource, DateTimeOffset? AccessExpiresAt);
public sealed record AdminActivationSummary(
    string Id, string DeviceFingerprint, DateTimeOffset ActivatedAt,
    DateTimeOffset LastRenewedAt, DateTimeOffset? RevokedAt, bool IsCurrent);
public sealed record AdminAuditSummary(
    Guid Id, string AdminEmail, string Action, string TargetType, string TargetId,
    string Reason, string Outcome, string? IpAddress, DateTimeOffset CreatedAt);
public sealed record AdminUserDetail(
    Guid Id, string Name, string Email, DateTimeOffset CreatedAt,
    IReadOnlyList<AdminSubscriptionSummary> Subscriptions,
    AdminLicenseSummary? License,
    IReadOnlyList<AdminActivationSummary> Activations,
    IReadOnlyList<AdminAuditSummary> Audit);
