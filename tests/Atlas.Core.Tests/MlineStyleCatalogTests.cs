using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using Atlas.Cad.Core.Styles;
using Xunit;

public sealed class MlineStyleCatalogTests
{
    [Fact]
    public void CatalogRoundTripsAndFindsNamesCaseInsensitively()
    {
        var original = new List<MlineStyleDefinition> { ValidStyle("ATLAS_STYLE") };
        using var stream = new MemoryStream();
        MlineStyleCatalog.Write(stream, original);
        stream.Position = 0;

        var styles = MlineStyleCatalog.Read(stream);
        var found = MlineStyleCatalog.Find(styles, "atlas_style");

        Assert.Single(styles);
        Assert.Equal("ATLAS_STYLE", found.Name);
        Assert.Equal(2, found.Elements.Count);
    }

    [Fact]
    public void CatalogRejectsDuplicateNamesIgnoringCase()
    {
        var styles = new List<MlineStyleDefinition>
        {
            ValidStyle("ATLAS_STYLE"),
            ValidStyle("atlas_style")
        };

        using var stream = new MemoryStream();
        Assert.Throws<SerializationException>(() => MlineStyleCatalog.Write(stream, styles));
    }

    private static MlineStyleDefinition ValidStyle(string name) => new MlineStyleDefinition
    {
        Name = name,
        Description = "Synthetic catalog test style",
        Flags = MlineStyleFlags.ShowMiters,
        FillColorIndex = 256,
        StartAngleDegrees = 90,
        EndAngleDegrees = 90,
        Elements = new List<MlineStyleElementDefinition>
        {
            new MlineStyleElementDefinition { Offset = 5, ColorIndex = 1, LinetypeName = "CONTINUOUS" },
            new MlineStyleElementDefinition { Offset = -5, ColorIndex = 2, LinetypeName = "CONTINUOUS" }
        }
    };
}
