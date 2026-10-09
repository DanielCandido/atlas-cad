using System;
using System.Collections.Generic;

namespace Atlas.Cad.Core.Styles;

public sealed class MlnCatalogSource
{
    public MlnCatalogSource(string sourceId, string content)
    {
        SourceId = sourceId ?? throw new ArgumentNullException(nameof(sourceId));
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public string SourceId { get; }
    public string Content { get; }
}

public sealed class MlnParsedCatalogSource
{
    public MlnParsedCatalogSource(string sourceId, IReadOnlyList<MlineStyleDefinition> styles)
    {
        SourceId = sourceId ?? throw new ArgumentNullException(nameof(sourceId));
        Styles = styles ?? throw new ArgumentNullException(nameof(styles));
    }

    public string SourceId { get; }
    public IReadOnlyList<MlineStyleDefinition> Styles { get; }
}

public static class MlnCatalogNormalizer
{
    public static IReadOnlyList<MlineStyleDefinition> Normalize(IEnumerable<MlnCatalogSource> sources)
    {
        if (sources == null) throw new ArgumentNullException(nameof(sources));

        var parsed = new List<MlnParsedCatalogSource>();
        foreach (var source in sources)
            parsed.Add(new MlnParsedCatalogSource(source.SourceId, MlnStyleParser.Parse(source.Content)));
        return NormalizeParsed(parsed);
    }

    public static IReadOnlyList<MlineStyleDefinition> NormalizeParsed(IEnumerable<MlnParsedCatalogSource> sources)
    {
        if (sources == null) throw new ArgumentNullException(nameof(sources));

        var result = new List<MlineStyleDefinition>();
        var seen = new Dictionary<string, CatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (string.IsNullOrWhiteSpace(source.SourceId))
                throw new ArgumentException("Catalog source ID is required.", nameof(sources));

            foreach (var style in source.Styles)
            {
                if (seen.TryGetValue(style.Name, out var previous))
                {
                    if (MlineStyleDefinitionComparer.Equivalent(previous.Style, style))
                        continue;
                    throw new MlnCatalogCollisionException(
                        style.Name,
                        previous.SourceId,
                        source.SourceId,
                        MlnCatalogCollisionKind.ConflictingDefinition);
                }

                seen.Add(style.Name, new CatalogEntry(source.SourceId, style));
                result.Add(style);
            }
        }

        if (result.Count == 0)
            throw new ArgumentException("Catalog contains no MLN styles.", nameof(sources));
        return result;
    }

    private sealed class CatalogEntry
    {
        public CatalogEntry(string sourceId, MlineStyleDefinition style)
        {
            SourceId = sourceId;
            Style = style;
        }

        public string SourceId { get; }
        public MlineStyleDefinition Style { get; }
    }
}

public static class MlineStyleDefinitionComparer
{
    public static bool Equivalent(MlineStyleDefinition left, MlineStyleDefinition right, double tolerance = 1e-9)
    {
        if (left == null) throw new ArgumentNullException(nameof(left));
        if (right == null) throw new ArgumentNullException(nameof(right));
        if (!string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(left.Description, right.Description, StringComparison.Ordinal) ||
            left.Flags != right.Flags || left.FillColorIndex != right.FillColorIndex ||
            Math.Abs(left.StartAngleDegrees - right.StartAngleDegrees) > tolerance ||
            Math.Abs(left.EndAngleDegrees - right.EndAngleDegrees) > tolerance ||
            left.Elements.Count != right.Elements.Count)
            return false;

        for (var index = 0; index < left.Elements.Count; index++)
        {
            var a = left.Elements[index];
            var b = right.Elements[index];
            if (Math.Abs(a.Offset - b.Offset) > tolerance ||
                a.ColorIndex != b.ColorIndex ||
                !string.Equals(a.LinetypeName, b.LinetypeName, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }
}

public enum MlnCatalogCollisionKind
{
    ExactDuplicate,
    ConflictingDefinition
}

public sealed class MlnCatalogCollisionException : InvalidOperationException
{
    public MlnCatalogCollisionException(
        string styleName,
        string firstSourceId,
        string secondSourceId,
        MlnCatalogCollisionKind kind)
        : base("MLN style name collision for '" + styleName + "' between '" + firstSourceId + "' and '" + secondSourceId + "' (" + kind + ").")
    {
        StyleName = styleName;
        FirstSourceId = firstSourceId;
        SecondSourceId = secondSourceId;
        Kind = kind;
    }

    public string StyleName { get; }
    public string FirstSourceId { get; }
    public string SecondSourceId { get; }
    public MlnCatalogCollisionKind Kind { get; }
}
