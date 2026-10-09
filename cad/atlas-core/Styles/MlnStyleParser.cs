using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Atlas.Cad.Core.Styles;

public static class MlnStyleParser
{
    public const int MaximumStyles = 1024;
    public const int MaximumElementsPerStyle = 16;
    public const int MaximumInputCharacters = 1024 * 1024;

    private const MlineStyleFlags SupportedFlags =
        MlineStyleFlags.Filled |
        MlineStyleFlags.ShowMiters |
        MlineStyleFlags.StartSquareCap |
        MlineStyleFlags.StartInnerArcs |
        MlineStyleFlags.StartRoundCap |
        MlineStyleFlags.EndSquareCap |
        MlineStyleFlags.EndInnerArcs |
        MlineStyleFlags.EndRoundCap;

    public static IReadOnlyList<MlineStyleDefinition> Parse(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));
        if (text.Length == 0) throw new MlnParseException(1, "MLN input is empty.");
        if (text.Length > MaximumInputCharacters)
            throw new MlnParseException(1, "MLN input exceeds the size limit.");

        using var reader = new StringReader(text);
        return Parse(reader);
    }

    public static IReadOnlyList<MlineStyleDefinition> Parse(TextReader reader)
    {
        if (reader == null) throw new ArgumentNullException(nameof(reader));

        var cursor = new LineCursor(reader);
        var styles = new List<MlineStyleDefinition>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (cursor.TryReadNonEmpty(out var marker, out var markerLine))
        {
            if (markerLine == 1)
                marker = marker.TrimStart('\uFEFF');
            if (!string.Equals(marker.Trim(), "MLSTYLE", StringComparison.OrdinalIgnoreCase))
                throw new MlnParseException(markerLine, "Expected MLSTYLE marker.");
            if (styles.Count >= MaximumStyles)
                throw new MlnParseException(markerLine, "MLN contains too many styles.");

            var style = ParseStyle(cursor);
            if (!names.Add(style.Name))
                throw new MlnParseException(markerLine, "Duplicate or ambiguous style name: " + style.Name);
            styles.Add(style);
        }

        if (styles.Count == 0)
            throw new MlnParseException(1, "MLN contains no styles.");
        return styles;
    }

    private static MlineStyleDefinition ParseStyle(LineCursor cursor)
    {
        var style = new MlineStyleDefinition
        {
            Name = ReadRequiredText(cursor, 2, "style name", 32),
            Flags = (MlineStyleFlags)ReadRequiredInt(cursor, 70, "style flags"),
            Description = ReadRequiredText(cursor, 3, "description", 255, allowEmpty: true),
            FillColorIndex = ReadRequiredInt(cursor, 62, "fill color"),
            StartAngleDegrees = ReadRequiredDouble(cursor, 51, "start angle"),
            EndAngleDegrees = ReadRequiredDouble(cursor, 52, "end angle")
        };
        var elementCount = ReadRequiredInt(cursor, 71, "element count");

        ValidateStyleHeader(style, elementCount, cursor.LineNumber);
        for (var index = 0; index < elementCount; index++)
        {
            var element = new MlineStyleElementDefinition
            {
                Offset = ReadRequiredDouble(cursor, 49, "element offset"),
                ColorIndex = ReadRequiredInt(cursor, 62, "element color"),
                LinetypeName = ReadRequiredText(cursor, 6, "element linetype", 255)
            };
            ValidateElement(element, cursor.LineNumber);
            style.Elements.Add(element);
        }

        var terminator = cursor.ReadRequired("style terminator");
        if (!int.TryParse(terminator.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code) || code != 0)
            throw new MlnParseException(terminator.LineNumber, "Expected style terminator code 0.");

        return style;
    }

    private static string ReadRequiredText(
        LineCursor cursor,
        int expectedCode,
        string field,
        int maximumLength,
        bool allowEmpty = false)
    {
        ExpectCode(cursor, expectedCode, field);
        var value = cursor.ReadRequired(field).Value.Trim();
        if ((!allowEmpty && value.Length == 0) || value.Length > maximumLength || HasControlCharacter(value))
            throw new MlnParseException(cursor.LineNumber, "Invalid " + field + ".");
        return value;
    }

    private static int ReadRequiredInt(LineCursor cursor, int expectedCode, string field)
    {
        ExpectCode(cursor, expectedCode, field);
        var line = cursor.ReadRequired(field);
        if (!int.TryParse(line.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new MlnParseException(line.LineNumber, "Invalid integer for " + field + ".");
        return value;
    }

    private static double ReadRequiredDouble(LineCursor cursor, int expectedCode, string field)
    {
        ExpectCode(cursor, expectedCode, field);
        var line = cursor.ReadRequired(field);
        if (!double.TryParse(line.Value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ||
            double.IsNaN(value) || double.IsInfinity(value))
            throw new MlnParseException(line.LineNumber, "Invalid number for " + field + ".");
        return value;
    }

    private static void ExpectCode(LineCursor cursor, int expectedCode, string field)
    {
        var line = cursor.ReadRequired(field + " code");
        if (!int.TryParse(line.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var actualCode) ||
            actualCode != expectedCode)
            throw new MlnParseException(line.LineNumber, "Expected group code " + expectedCode + " for " + field + ".");
    }

    private static void ValidateStyleHeader(MlineStyleDefinition style, int elementCount, int lineNumber)
    {
        if ((style.Flags & ~SupportedFlags) != 0)
            throw new MlnParseException(lineNumber, "Style contains unsupported flag bits.");
        ValidateColor(style.FillColorIndex, lineNumber, "fill color");
        ValidateAngle(style.StartAngleDegrees, lineNumber, "start angle");
        ValidateAngle(style.EndAngleDegrees, lineNumber, "end angle");
        if (elementCount < 1 || elementCount > MaximumElementsPerStyle)
            throw new MlnParseException(lineNumber, "Element count is outside the supported range.");
    }

    private static void ValidateElement(MlineStyleElementDefinition element, int lineNumber)
    {
        if (Math.Abs(element.Offset) > 1.0e9)
            throw new MlnParseException(lineNumber, "Element offset is outside the supported range.");
        ValidateColor(element.ColorIndex, lineNumber, "element color");
    }

    private static void ValidateColor(int colorIndex, int lineNumber, string field)
    {
        if (colorIndex < 0 || colorIndex > 256)
            throw new MlnParseException(lineNumber, "Invalid ACI " + field + ".");
    }

    private static void ValidateAngle(double angle, int lineNumber, string field)
    {
        if (angle <= 0.0 || angle >= 180.0)
            throw new MlnParseException(lineNumber, "Invalid " + field + "; expected degrees between 0 and 180.");
    }

    private static bool HasControlCharacter(string value)
    {
        foreach (var character in value)
            if (char.IsControl(character))
                return true;
        return false;
    }

    private sealed class LineCursor
    {
        private readonly TextReader _reader;

        public LineCursor(TextReader reader) => _reader = reader;

        public int LineNumber { get; private set; }

        public (string Value, int LineNumber) ReadRequired(string field)
        {
            var value = _reader.ReadLine();
            LineNumber++;
            if (value == null)
                throw new MlnParseException(LineNumber, "Unexpected end of MLN while reading " + field + ".");
            return (value, LineNumber);
        }

        public bool TryReadNonEmpty(out string value, out int lineNumber)
        {
            string? line;
            while ((line = _reader.ReadLine()) != null)
            {
                LineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                value = line;
                lineNumber = LineNumber;
                return true;
            }
            value = string.Empty;
            lineNumber = LineNumber;
            return false;
        }
    }
}

public sealed class MlnParseException : FormatException
{
    public MlnParseException(int lineNumber, string message)
        : base("MLN line " + lineNumber.ToString(CultureInfo.InvariantCulture) + ": " + message)
    {
        LineNumber = lineNumber;
    }

    public int LineNumber { get; }
}
