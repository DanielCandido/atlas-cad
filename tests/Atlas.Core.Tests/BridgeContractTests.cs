using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Atlas.Cad.Core.Bridge;
using Atlas.Cad.Core.Styles;
using Xunit;

public sealed class BridgeContractTests
{
    [Fact]
    public void AcceptsAValidVersionOneRequest()
    {
        var errors = BridgeContractValidator.Validate(ValidRequest());
        Assert.Empty(errors);
    }

    [Fact]
    public void RejectsUnsupportedProtocolAndUnsafeRequestId()
    {
        var request = ValidRequest();
        request.ProtocolVersion = 2;
        request.RequestId = "../../payload";

        var errors = BridgeContractValidator.Validate(request);

        Assert.Contains("protocolVersion.unsupported", errors);
        Assert.Contains("requestId.invalid", errors);
    }

    [Fact]
    public void RejectsNonFiniteAndCoincidentPoints()
    {
        var nonFinite = ValidRequest();
        nonFinite.PointsWcs![1].X = double.NaN;
        Assert.Contains("pointsWcs.nonFinite", BridgeContractValidator.Validate(nonFinite));

        var coincident = ValidRequest();
        coincident.PointsWcs = new List<Point3Dto>
        {
            new Point3Dto(1, 2, 3),
            new Point3Dto(1, 2, 3)
        };
        Assert.Contains("pointsWcs.distinct", BridgeContractValidator.Validate(coincident));
    }

    [Fact]
    public void RejectsPointsOutsideTheDeclaredPlane()
    {
        var request = ValidRequest();
        request.PointsWcs = new List<Point3Dto>
        {
            new Point3Dto(0, 0, 0),
            new Point3Dto(10, 0, 1)
        };

        Assert.Contains("pointsWcs.notPlanarToNormal", BridgeContractValidator.Validate(request));
    }

    [Fact]
    public void RejectsControlCharactersInLayerName()
    {
        var request = ValidRequest();
        request.LayerName = "STEEL\nINJECTED";

        Assert.Contains("layerName.invalid", BridgeContractValidator.Validate(request));
    }

    [Fact]
    public void RequestJsonRoundTripsWithoutLosingContractFields()
    {
        var original = ValidRequest();
        var json = BridgeJson.SerializeRequest(original);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var result = BridgeJson.ReadRequest(stream);

        Assert.Equal(BridgeProtocol.CurrentVersion, result.ProtocolVersion);
        Assert.Equal(original.RequestId, result.RequestId);
        Assert.Equal(original.StyleName, result.StyleName);
        Assert.Equal(2, result.PointsWcs!.Count);
        Assert.Equal(1, result.Justification);
        Assert.Equal(1.0, result.Scale);
    }

    [Fact]
    public void ResponseJsonRoundTripsSuccessHandle()
    {
        var response = DrawMlineResponse.Ok(BridgeProtocol.NewRequestId(), "1A2B");
        using var stream = new MemoryStream();
        BridgeJson.WriteResponse(stream, response);
        stream.Position = 0;

        var result = BridgeJson.ReadResponse(stream);

        Assert.True(result.Success);
        Assert.Equal("1A2B", result.NewEntityHandle);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void AcceptsCatalogBackedStyleRequestWithoutEmbeddedDefinition()
    {
        var request = new EnsureStyleRequest
        {
            ProtocolVersion = BridgeProtocol.CurrentVersion,
            RequestId = BridgeProtocol.NewRequestId(),
            ExpectedDrawingIdentity = @"C:\drawings\bridge-test.dwg",
            StyleName = "ATLAS_STYLE"
        };

        Assert.Empty(EnsureStyleRequestValidator.Validate(request));
    }

    [Fact]
    public void RejectsAmbiguousStyleSource()
    {
        var request = new EnsureStyleRequest
        {
            ProtocolVersion = BridgeProtocol.CurrentVersion,
            RequestId = BridgeProtocol.NewRequestId(),
            ExpectedDrawingIdentity = @"C:\drawings\bridge-test.dwg",
            StyleName = "ATLAS_STYLE",
            Style = new MlineStyleDefinition()
        };

        Assert.Contains("styleSource.invalid", EnsureStyleRequestValidator.Validate(request));
    }

    private static DrawMlineRequest ValidRequest() => new DrawMlineRequest
    {
        ProtocolVersion = BridgeProtocol.CurrentVersion,
        RequestId = BridgeProtocol.NewRequestId(),
        StyleName = BridgeProtocol.SyntheticStyleName,
        PointsWcs = new List<Point3Dto>
        {
            new Point3Dto(0, 0, 0),
            new Point3Dto(10, 0, 0)
        },
        NormalWcs = new Point3Dto(0, 0, 1),
        LayerName = "0",
        Closed = false,
        SourceHandle = "A1",
        ExpectedDrawingIdentity = @"C:\drawings\bridge-test.dwg",
        Justification = 1,
        Scale = 1.0
    };
}
