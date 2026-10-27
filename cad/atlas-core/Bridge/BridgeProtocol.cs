using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace Atlas.Cad.Core.Bridge;

public static class BridgeProtocol
{
    public const int CurrentVersion = 1;
    public const int MaximumPointCount = 10000;
    public const long MaximumRequestBytes = 1024 * 1024;
    public const string RequestSuffix = ".request.json";
    public const string ResponseSuffix = ".response.json";
    public const string SyntheticStyleName = "ATLAS_TEST_MLINE";

    private static readonly Regex RequestIdPattern =
        new Regex("^[a-f0-9]{32}$", RegexOptions.CultureInvariant);

    public static bool IsValidRequestId(string? value) =>
        value != null && RequestIdPattern.IsMatch(value);

    public static string NewRequestId() => Guid.NewGuid().ToString("N");
}

[DataContract]
public sealed class Point3Dto
{
    public Point3Dto()
    {
    }

    public Point3Dto(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    [DataMember(Name = "x", IsRequired = true, Order = 1)]
    public double X { get; set; }

    [DataMember(Name = "y", IsRequired = true, Order = 2)]
    public double Y { get; set; }

    [DataMember(Name = "z", IsRequired = true, Order = 3)]
    public double Z { get; set; }
}

[DataContract]
public sealed class DrawMlineRequest
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)]
    public int ProtocolVersion { get; set; }

    [DataMember(Name = "requestId", IsRequired = true, Order = 2)]
    public string? RequestId { get; set; }

    [DataMember(Name = "styleName", IsRequired = true, Order = 3)]
    public string? StyleName { get; set; }

    [DataMember(Name = "pointsWcs", IsRequired = true, Order = 4)]
    public List<Point3Dto>? PointsWcs { get; set; }

    [DataMember(Name = "normalWcs", IsRequired = true, Order = 5)]
    public Point3Dto? NormalWcs { get; set; }

    [DataMember(Name = "layerId", EmitDefaultValue = false, Order = 6)]
    public string? LayerId { get; set; }

    [DataMember(Name = "layerName", EmitDefaultValue = false, Order = 7)]
    public string? LayerName { get; set; }

    [DataMember(Name = "closed", IsRequired = true, Order = 8)]
    public bool Closed { get; set; }

    [DataMember(Name = "sourceHandle", EmitDefaultValue = false, Order = 9)]
    public string? SourceHandle { get; set; }

    [DataMember(Name = "expectedDrawingIdentity", IsRequired = true, Order = 10)]
    public string? ExpectedDrawingIdentity { get; set; }

    // AutoCAD enum values: Top=0, Zero=1, Bottom=2.
    [DataMember(Name = "justification", IsRequired = true, Order = 11)]
    public int Justification { get; set; } = 1;

    [DataMember(Name = "scale", IsRequired = true, Order = 12)]
    public double Scale { get; set; } = 1.0;
}

[DataContract]
public sealed class DrawMlineResponse
{
    [DataMember(Name = "protocolVersion", IsRequired = true, Order = 1)]
    public int ProtocolVersion { get; set; } = BridgeProtocol.CurrentVersion;

    [DataMember(Name = "requestId", IsRequired = true, Order = 2)]
    public string RequestId { get; set; } = string.Empty;

    [DataMember(Name = "success", IsRequired = true, Order = 3)]
    public bool Success { get; set; }

    [DataMember(Name = "newEntityHandle", EmitDefaultValue = false, Order = 4)]
    public string? NewEntityHandle { get; set; }

    [DataMember(Name = "errorCode", EmitDefaultValue = false, Order = 5)]
    public string? ErrorCode { get; set; }

    [DataMember(Name = "message", EmitDefaultValue = false, Order = 6)]
    public string? Message { get; set; }

    public static DrawMlineResponse Ok(string requestId, string handle) => new DrawMlineResponse
    {
        RequestId = requestId,
        Success = true,
        NewEntityHandle = handle
    };

    public static DrawMlineResponse Fail(string requestId, string errorCode, string message) => new DrawMlineResponse
    {
        RequestId = requestId,
        Success = false,
        ErrorCode = errorCode,
        Message = message
    };
}

public static class BridgeContractValidator
{
    public static IReadOnlyList<string> Validate(DrawMlineRequest? request)
    {
        var errors = new List<string>();
        if (request == null)
        {
            errors.Add("request.required");
            return errors;
        }

        if (request.ProtocolVersion != BridgeProtocol.CurrentVersion)
            errors.Add("protocolVersion.unsupported");
        if (!BridgeProtocol.IsValidRequestId(request.RequestId))
            errors.Add("requestId.invalid");
        if (string.IsNullOrWhiteSpace(request.StyleName) || request.StyleName!.Length > 255 || HasControlCharacter(request.StyleName))
            errors.Add("styleName.invalid");
        if (string.IsNullOrWhiteSpace(request.ExpectedDrawingIdentity) || request.ExpectedDrawingIdentity!.Length > 1024 || HasControlCharacter(request.ExpectedDrawingIdentity))
            errors.Add("expectedDrawingIdentity.invalid");
        if (string.IsNullOrWhiteSpace(request.LayerId) && string.IsNullOrWhiteSpace(request.LayerName))
            errors.Add("layer.required");
        if (!string.IsNullOrEmpty(request.LayerId) && !IsHexHandle(request.LayerId!))
            errors.Add("layerId.invalid");
        if (!string.IsNullOrEmpty(request.LayerName) &&
            (request.LayerName!.Length > 255 || HasControlCharacter(request.LayerName)))
            errors.Add("layerName.invalid");
        if (!string.IsNullOrEmpty(request.SourceHandle) && !IsHexHandle(request.SourceHandle!))
            errors.Add("sourceHandle.invalid");
        if (request.Justification < 0 || request.Justification > 2)
            errors.Add("justification.invalid");
        if (!IsFinite(request.Scale) || request.Scale <= 0.0)
            errors.Add("scale.invalid");

        ValidateGeometry(request, errors);
        return errors;
    }

    private static void ValidateGeometry(DrawMlineRequest request, List<string> errors)
    {
        var points = request.PointsWcs;
        if (points == null || points.Count < 2 || points.Count > BridgeProtocol.MaximumPointCount)
        {
            errors.Add("pointsWcs.count");
            return;
        }

        if (request.NormalWcs == null || !IsFinite(request.NormalWcs))
        {
            errors.Add("normalWcs.invalid");
            return;
        }

        var normalLength = Length(request.NormalWcs);
        if (normalLength <= 1e-12)
        {
            errors.Add("normalWcs.zero");
            return;
        }

        var first = points[0];
        if (first == null || !IsFinite(first))
        {
            errors.Add("pointsWcs.nonFinite");
            return;
        }

        var hasDistinctPoint = false;
        foreach (var point in points)
        {
            if (point == null || !IsFinite(point))
            {
                errors.Add("pointsWcs.nonFinite");
                return;
            }

            var distance = Distance(first, point);
            if (distance > 1e-9)
                hasDistinctPoint = true;

            var planeDistance = Math.Abs(
                ((point.X - first.X) * request.NormalWcs.X +
                 (point.Y - first.Y) * request.NormalWcs.Y +
                 (point.Z - first.Z) * request.NormalWcs.Z) / normalLength);
            if (planeDistance > 1e-8 * (1.0 + distance))
            {
                errors.Add("pointsWcs.notPlanarToNormal");
                return;
            }
        }

        if (!hasDistinctPoint)
            errors.Add("pointsWcs.distinct");
    }

    private static bool IsFinite(Point3Dto point) =>
        IsFinite(point.X) && IsFinite(point.Y) && IsFinite(point.Z);

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static double Length(Point3Dto point) => Math.Sqrt(point.X * point.X + point.Y * point.Y + point.Z * point.Z);

    private static double Distance(Point3Dto left, Point3Dto right)
    {
        var x = right.X - left.X;
        var y = right.Y - left.Y;
        var z = right.Z - left.Z;
        return Math.Sqrt(x * x + y * y + z * z);
    }

    private static bool HasControlCharacter(string value)
    {
        foreach (var character in value)
            if (char.IsControl(character))
                return true;
        return false;
    }

    private static bool IsHexHandle(string value)
    {
        if (value.Length == 0 || value.Length > 16)
            return false;
        foreach (var character in value)
            if (!((character >= '0' && character <= '9') ||
                  (character >= 'a' && character <= 'f') ||
                  (character >= 'A' && character <= 'F')))
                return false;
        return true;
    }
}

public static class BridgeJson
{
    public static DrawMlineRequest ReadRequest(Stream input)
    {
        return (DrawMlineRequest)ReadRequestPayload(input, typeof(DrawMlineRequest));
    }

    public static void WriteRequest(Stream output, DrawMlineRequest request) =>
        Write(output, request, typeof(DrawMlineRequest));

    public static DrawMlineResponse ReadResponse(Stream input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var serializer = new DataContractJsonSerializer(typeof(DrawMlineResponse));
        return (DrawMlineResponse)(serializer.ReadObject(input) ??
            throw new SerializationException("Response payload is empty."));
    }

    public static void WriteResponse(Stream output, DrawMlineResponse response) =>
        Write(output, response, typeof(DrawMlineResponse));

    public static EnsureStyleRequest ReadEnsureStyleRequest(Stream input)
    {
        return (EnsureStyleRequest)ReadRequestPayload(input, typeof(EnsureStyleRequest));
    }

    public static void WriteEnsureStyleRequest(Stream output, EnsureStyleRequest request) =>
        Write(output, request, typeof(EnsureStyleRequest));

    public static EnsureStyleResponse ReadEnsureStyleResponse(Stream input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        var serializer = new DataContractJsonSerializer(typeof(EnsureStyleResponse));
        return (EnsureStyleResponse)(serializer.ReadObject(input) ??
            throw new SerializationException("Response payload is empty."));
    }

    public static void WriteEnsureStyleResponse(Stream output, EnsureStyleResponse response) =>
        Write(output, response, typeof(EnsureStyleResponse));

    public static string SerializeRequest(DrawMlineRequest request)
    {
        using var stream = new MemoryStream();
        WriteRequest(stream, request);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void Write(Stream output, object value, Type type)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        if (value == null) throw new ArgumentNullException(nameof(value));
        new DataContractJsonSerializer(type).WriteObject(output, value);
    }

    private static object ReadRequestPayload(Stream input, Type type)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (input.CanSeek && input.Length > BridgeProtocol.MaximumRequestBytes)
            throw new SerializationException("Request payload exceeds the size limit.");

        using var raw = new MemoryStream();
        input.CopyTo(raw);
        if (raw.Length == 0)
            throw new SerializationException("Request payload is empty.");
        if (raw.Length > BridgeProtocol.MaximumRequestBytes)
            throw new SerializationException("Request payload exceeds the size limit.");

        string json;
        try
        {
            json = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(raw.ToArray());
        }
        catch (DecoderFallbackException)
        {
            json = Windows1252().GetString(raw.ToArray());
        }

        using var normalized = new MemoryStream(Encoding.UTF8.GetBytes(json), writable: false);
        var serializer = new DataContractJsonSerializer(type);
        return serializer.ReadObject(normalized) ??
            throw new SerializationException("Request payload is empty.");
    }

    private static Encoding Windows1252()
    {
#if NETFRAMEWORK
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
#else
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
#endif
    }
}
