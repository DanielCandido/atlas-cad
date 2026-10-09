using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Atlas.Cad.Core.Styles;

namespace Atlas.Cad.Core.Bridge;

[DataContract]
public sealed class EnsureStyleRequest
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)]
    public int ProtocolVersion { get; set; }

    [DataMember(Name = "requestId", IsRequired = true, Order = 2)]
    public string? RequestId { get; set; }

    [DataMember(Name = "expectedDrawingIdentity", IsRequired = true, Order = 3)]
    public string? ExpectedDrawingIdentity { get; set; }

    [DataMember(Name = "style", EmitDefaultValue = false, Order = 4)]
    public MlineStyleDefinition? Style { get; set; }

    [DataMember(Name = "styleName", EmitDefaultValue = false, Order = 5)]
    public string? StyleName { get; set; }
}

[DataContract]
public sealed class EnsureStyleResponse
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)]
    public int ProtocolVersion { get; set; } = BridgeProtocol.CurrentVersion;

    [DataMember(Name = "requestId", IsRequired = true, Order = 2)]
    public string RequestId { get; set; } = string.Empty;

    [DataMember(Name = "success", IsRequired = true, Order = 3)]
    public bool Success { get; set; }

    [DataMember(Name = "status", EmitDefaultValue = false, Order = 4)]
    public string? Status { get; set; }

    [DataMember(Name = "styleName", EmitDefaultValue = false, Order = 5)]
    public string? StyleName { get; set; }

    [DataMember(Name = "errorCode", EmitDefaultValue = false, Order = 6)]
    public string? ErrorCode { get; set; }

    [DataMember(Name = "message", EmitDefaultValue = false, Order = 7)]
    public string? Message { get; set; }

    public static EnsureStyleResponse Ok(string requestId, string styleName, string status) => new EnsureStyleResponse
    {
        RequestId = requestId,
        Success = true,
        StyleName = styleName,
        Status = status
    };

    public static EnsureStyleResponse Fail(string requestId, string errorCode, string message) => new EnsureStyleResponse
    {
        RequestId = requestId,
        Success = false,
        ErrorCode = errorCode,
        Message = message
    };
}

public static class EnsureStyleRequestValidator
{
    public static IReadOnlyList<string> Validate(EnsureStyleRequest? request)
    {
        var errors = new List<string>();
        if (request == null)
        {
            errors.Add("request.required");
            return errors;
        }
        if (request.ProtocolVersion != BridgeProtocol.CurrentVersion) errors.Add("protocolVersion.unsupported");
        if (!BridgeProtocol.IsValidRequestId(request.RequestId)) errors.Add("requestId.invalid");
        if (string.IsNullOrWhiteSpace(request.ExpectedDrawingIdentity) || request.ExpectedDrawingIdentity!.Length > 1024 || HasControlCharacter(request.ExpectedDrawingIdentity))
            errors.Add("expectedDrawingIdentity.invalid");
        var hasDefinition = request.Style != null;
        var hasCatalogName = !string.IsNullOrWhiteSpace(request.StyleName);
        if (hasDefinition == hasCatalogName)
            errors.Add("styleSource.invalid");
        else if (hasDefinition)
            foreach (var error in MlineStyleDefinitionValidator.Validate(request.Style)) errors.Add(error);
        else if (request.StyleName!.Length > 255 || HasControlCharacter(request.StyleName))
            errors.Add("styleName.invalid");
        return errors;
    }

    private static bool HasControlCharacter(string value)
    {
        foreach (var character in value)
            if (char.IsControl(character)) return true;
        return false;
    }
}
