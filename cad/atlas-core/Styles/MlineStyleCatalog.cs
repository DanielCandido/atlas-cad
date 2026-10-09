using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Atlas.Cad.Core.Styles;

public static class MlineStyleCatalog
{
    public const long MaximumBytes = 16 * 1024 * 1024;
    public const int MaximumStyleCount = 10000;

    public static IReadOnlyList<MlineStyleDefinition> Read(Stream input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));
        if (input.CanSeek && (input.Length <= 0 || input.Length > MaximumBytes))
            throw new SerializationException("Catalog size is invalid.");

        var serializer = new DataContractJsonSerializer(typeof(List<MlineStyleDefinition>));
        var styles = (List<MlineStyleDefinition>?)serializer.ReadObject(input) ??
            throw new SerializationException("Catalog is empty.");
        Validate(styles);
        return styles;
    }

    public static void Write(Stream output, IReadOnlyList<MlineStyleDefinition> styles)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        if (styles == null) throw new ArgumentNullException(nameof(styles));
        var copy = new List<MlineStyleDefinition>(styles);
        Validate(copy);
        new DataContractJsonSerializer(typeof(List<MlineStyleDefinition>)).WriteObject(output, copy);
    }

    public static MlineStyleDefinition Find(
        IReadOnlyList<MlineStyleDefinition> styles,
        string styleName)
    {
        if (styles == null) throw new ArgumentNullException(nameof(styles));
        if (string.IsNullOrWhiteSpace(styleName))
            throw new ArgumentException("Style name is required.", nameof(styleName));

        foreach (var style in styles)
            if (string.Equals(style.Name, styleName, StringComparison.OrdinalIgnoreCase))
                return style;
        throw new KeyNotFoundException("The requested style is not present in the installed catalog.");
    }

    private static void Validate(IReadOnlyList<MlineStyleDefinition> styles)
    {
        if (styles.Count == 0 || styles.Count > MaximumStyleCount)
            throw new SerializationException("Catalog style count is invalid.");

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var style in styles)
        {
            var errors = MlineStyleDefinitionValidator.Validate(style);
            if (errors.Count > 0)
                throw new SerializationException("Catalog contains an invalid style: " + string.Join(",", errors));
            if (!names.Add(style.Name))
                throw new SerializationException("Catalog contains a duplicate style name: " + style.Name);
        }
    }
}
