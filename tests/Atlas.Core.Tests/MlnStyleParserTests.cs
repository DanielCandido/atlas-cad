using System;
using System.Collections.Generic;
using System.IO;
using Atlas.Cad.Core.Styles;
using Xunit;

public sealed class MlnStyleParserTests
{
    [Fact]
    public void ParsesSingleStyleWithFlagsElementsAndDegreeUnits()
    {
        var styles = ParseFixture("styles-single.mln");

        var style = Assert.Single(styles);
        Assert.Equal("ATLAS_GOLDEN_SINGLE", style.Name);
        Assert.Equal("Public synthetic golden style", style.Description);
        Assert.Equal(3, style.FillColorIndex);
        Assert.Equal(90.0, style.StartAngleDegrees);
        Assert.Equal(75.0, style.EndAngleDegrees);
        Assert.True(style.Flags.HasFlag(MlineStyleFlags.Filled));
        Assert.True(style.Flags.HasFlag(MlineStyleFlags.ShowMiters));
        Assert.True(style.Flags.HasFlag(MlineStyleFlags.StartSquareCap));
        Assert.True(style.Flags.HasFlag(MlineStyleFlags.EndSquareCap));
        Assert.True(style.Flags.HasFlag(MlineStyleFlags.EndRoundCap));
        Assert.Equal(2, style.Elements.Count);
        Assert.Equal(10.0, style.Elements[0].Offset);
        Assert.Equal("DASHED", style.Elements[1].LinetypeName);
    }

    [Fact]
    public void ParsesMultipleStylesWithoutMergingTheirElements()
    {
        var styles = ParseFixture("styles-multiple.mln");

        Assert.Equal(2, styles.Count);
        Assert.Equal("ATLAS_GOLDEN_A", styles[0].Name);
        Assert.Single(styles[0].Elements);
        Assert.Equal("ATLAS_GOLDEN_B", styles[1].Name);
        Assert.Equal(3, styles[1].Elements.Count);
    }

    [Fact]
    public void RejectsDuplicateNamesCaseInsensitively()
    {
        var text = ReadFixtureText("styles-single.mln");
        var duplicate = text.Replace("ATLAS_GOLDEN_SINGLE", "atlas_golden_single");

        var exception = Assert.Throws<MlnParseException>(() => MlnStyleParser.Parse(text + duplicate));

        Assert.Contains("Duplicate", exception.Message);
    }

    [Fact]
    public void RejectsElementCountMismatch()
    {
        var text = ReadFixtureText("styles-single.mln");
        text = text.Replace("71\n 2\n", "71\n 3\n");

        Assert.Throws<MlnParseException>(() => MlnStyleParser.Parse(text));
    }

    [Fact]
    public void RejectsUnsupportedFlagBits()
    {
        var text = ReadFixtureText("styles-single.mln");
        text = text.Replace("70\n 1299\n", "70\n 4096\n");

        var exception = Assert.Throws<MlnParseException>(() => MlnStyleParser.Parse(text));

        Assert.Contains("unsupported flag", exception.Message);
    }

    [Fact]
    public void CatalogConsolidatesExactDuplicatesAcrossSources()
    {
        var text = ReadFixtureText("styles-single.mln");

        var styles = MlnCatalogNormalizer.Normalize(new[]
            {
                new MlnCatalogSource("first", text),
                new MlnCatalogSource("second", text)
            });

        Assert.Single(styles);
    }

    [Fact]
    public void CatalogRejectsConflictingDefinitionsAcrossSources()
    {
        var text = ReadFixtureText("styles-single.mln");
        var conflicting = text.Replace("49\n 10.0\n", "49\n 12.0\n");

        var exception = Assert.Throws<MlnCatalogCollisionException>(() =>
            MlnCatalogNormalizer.Normalize(new List<MlnCatalogSource>
            {
                new MlnCatalogSource("first", text),
                new MlnCatalogSource("second", conflicting)
            }));

        Assert.Equal(MlnCatalogCollisionKind.ConflictingDefinition, exception.Kind);
    }

    private static System.Collections.Generic.IReadOnlyList<MlineStyleDefinition> ParseFixture(string name) =>
        MlnStyleParser.Parse(File.ReadAllText(FixturePath(name)));

    private static string ReadFixtureText(string name) =>
        File.ReadAllText(FixturePath(name)).Replace("\r\n", "\n");

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
