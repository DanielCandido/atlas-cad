using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Atlas.Licensing;

public static class LicensingProtocol
{
    public const int CurrentVersion = 1;
    public const string ProductId = "atlas-mline-pro";
    public const string Issuer = "atlas-cad-licensing";
    public const string DevelopmentKeyId = "atlas-dev-2026-01";
    public const string TokenPrefix = "ATL1";
    public const int OfflineHours = 72;
    public const int RenewalThresholdHours = 24;
    public const int MaximumClockSkewMinutes = 5;
    public const int CatalogPackageVersion = 1;
    public const string CatalogAlgorithm = "A256GCM";
}

[DataContract]
public sealed class EntitlementClaims
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)]
    public int ProtocolVersion { get; set; }

    [DataMember(Name = "issuer", IsRequired = true, Order = 2)]
    public string Issuer { get; set; } = string.Empty;

    [DataMember(Name = "keyId", IsRequired = true, Order = 3)]
    public string KeyId { get; set; } = string.Empty;

    [DataMember(Name = "productId", IsRequired = true, Order = 4)]
    public string ProductId { get; set; } = string.Empty;

    [DataMember(Name = "licenseId", IsRequired = true, Order = 5)]
    public string LicenseId { get; set; } = string.Empty;

    [DataMember(Name = "activationId", IsRequired = true, Order = 6)]
    public string ActivationId { get; set; } = string.Empty;

    [DataMember(Name = "generation", IsRequired = true, Order = 7)]
    public int Generation { get; set; }

    [DataMember(Name = "deviceKeySha256", IsRequired = true, Order = 8)]
    public string DeviceKeySha256 { get; set; } = string.Empty;

    [DataMember(Name = "issuedAtUnix", IsRequired = true, Order = 9)]
    public long IssuedAtUnix { get; set; }

    [DataMember(Name = "expiresAtUnix", IsRequired = true, Order = 10)]
    public long ExpiresAtUnix { get; set; }

    [DataMember(Name = "modules", IsRequired = true, Order = 11)]
    public List<string> Modules { get; set; } = new List<string>();
}

[DataContract]
public sealed class ActivationRequest
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)]
    public int ProtocolVersion { get; set; }

    [DataMember(Name = "activationCode", IsRequired = true, Order = 2)]
    public string ActivationCode { get; set; } = string.Empty;

    [DataMember(Name = "devicePublicKeyX", IsRequired = true, Order = 3)]
    public string DevicePublicKeyX { get; set; } = string.Empty;

    [DataMember(Name = "devicePublicKeyY", IsRequired = true, Order = 4)]
    public string DevicePublicKeyY { get; set; } = string.Empty;

    [DataMember(Name = "proof", IsRequired = true, Order = 5)]
    public string Proof { get; set; } = string.Empty;
}

[DataContract]
public sealed class RenewalRequest
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)]
    public int ProtocolVersion { get; set; }

    [DataMember(Name = "activationId", IsRequired = true, Order = 2)]
    public string ActivationId { get; set; } = string.Empty;

    [DataMember(Name = "nonce", IsRequired = true, Order = 3)]
    public string Nonce { get; set; } = string.Empty;

    [DataMember(Name = "proof", IsRequired = true, Order = 4)]
    public string Proof { get; set; } = string.Empty;
}

[DataContract]
public sealed class EntitlementResponse
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)]
    public int ProtocolVersion { get; set; }

    [DataMember(Name = "activationId", IsRequired = true, Order = 2)]
    public string ActivationId { get; set; } = string.Empty;

    [DataMember(Name = "entitlementToken", IsRequired = true, Order = 3)]
    public string EntitlementToken { get; set; } = string.Empty;

    [DataMember(Name = "expiresAtUnix", IsRequired = true, Order = 4)]
    public long ExpiresAtUnix { get; set; }
}

[DataContract]
public sealed class LicenseApiError
{
    [DataMember(Name = "errorCode", IsRequired = true, Order = 1)]
    public string ErrorCode { get; set; } = string.Empty;

    [DataMember(Name = "message", IsRequired = true, Order = 2)]
    public string Message { get; set; } = string.Empty;
}

[DataContract]
public sealed class CatalogKeyRequest
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)]
    public int ProtocolVersion { get; set; }

    [DataMember(Name = "activationId", IsRequired = true, Order = 2)]
    public string ActivationId { get; set; } = string.Empty;

    [DataMember(Name = "keyId", IsRequired = true, Order = 3)]
    public string KeyId { get; set; } = string.Empty;

    [DataMember(Name = "nonce", IsRequired = true, Order = 4)]
    public string Nonce { get; set; } = string.Empty;

    [DataMember(Name = "proof", IsRequired = true, Order = 5)]
    public string Proof { get; set; } = string.Empty;
}

[DataContract]
public sealed class CatalogKeyResponse
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)]
    public int ProtocolVersion { get; set; }

    [DataMember(Name = "keyId", IsRequired = true, Order = 2)]
    public string KeyId { get; set; } = string.Empty;

    [DataMember(Name = "keyMaterial", IsRequired = true, Order = 3)]
    public string KeyMaterial { get; set; } = string.Empty;

    [DataMember(Name = "expiresAtUnix", IsRequired = true, Order = 4)]
    public long ExpiresAtUnix { get; set; }
}

[DataContract]
public sealed class InstallerPairingCreateRequest
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)] public int ProtocolVersion { get; set; }
    [DataMember(Name = "devicePublicKeyX", IsRequired = true, Order = 2)] public string DevicePublicKeyX { get; set; } = string.Empty;
    [DataMember(Name = "devicePublicKeyY", IsRequired = true, Order = 3)] public string DevicePublicKeyY { get; set; } = string.Empty;
    [DataMember(Name = "nonce", IsRequired = true, Order = 4)] public string Nonce { get; set; } = string.Empty;
    [DataMember(Name = "detectedVersions", IsRequired = true, Order = 5)] public List<string> DetectedVersions { get; set; } = new List<string>();
    [DataMember(Name = "proof", IsRequired = true, Order = 6)] public string Proof { get; set; } = string.Empty;
}

[DataContract]
public sealed class InstallerPairingProofRequest
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)] public int ProtocolVersion { get; set; }
    [DataMember(Name = "nonce", IsRequired = true, Order = 2)] public string Nonce { get; set; } = string.Empty;
    [DataMember(Name = "proof", IsRequired = true, Order = 3)] public string Proof { get; set; } = string.Empty;
}

[DataContract]
public sealed class InstallerPairingCreateResponse
{
    [DataMember(Name = "pairingId", IsRequired = true, Order = 1)] public string PairingId { get; set; } = string.Empty;
    [DataMember(Name = "userCode", IsRequired = true, Order = 2)] public string UserCode { get; set; } = string.Empty;
    [DataMember(Name = "verificationUri", IsRequired = true, Order = 3)] public string VerificationUri { get; set; } = string.Empty;
    [DataMember(Name = "expiresAtUnix", IsRequired = true, Order = 4)] public long ExpiresAtUnix { get; set; }
    [DataMember(Name = "pollIntervalSeconds", IsRequired = true, Order = 5)] public int PollIntervalSeconds { get; set; }
}

[DataContract]
public sealed class InstallerPairingStatusResponse
{
    [DataMember(Name = "status", IsRequired = true, Order = 1)] public string Status { get; set; } = string.Empty;
    [DataMember(Name = "expiresAtUnix", IsRequired = true, Order = 2)] public long ExpiresAtUnix { get; set; }
}

public sealed class EntitlementValidationResult
{
    public bool Success { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public EntitlementClaims? Claims { get; set; }

    public static EntitlementValidationResult Valid(EntitlementClaims claims) =>
        new EntitlementValidationResult { Success = true, Claims = claims };

    public static EntitlementValidationResult Invalid(string errorCode) =>
        new EntitlementValidationResult { Success = false, ErrorCode = errorCode };
}
