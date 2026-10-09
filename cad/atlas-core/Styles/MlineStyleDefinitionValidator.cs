using System;
using System.Collections.Generic;

namespace Atlas.Cad.Core.Styles;

public static class MlineStyleDefinitionValidator
{
    private const MlineStyleFlags SupportedFlags =
        MlineStyleFlags.Filled |
        MlineStyleFlags.ShowMiters |
        MlineStyleFlags.StartSquareCap |
        MlineStyleFlags.StartInnerArcs |
        MlineStyleFlags.StartRoundCap |
        MlineStyleFlags.EndSquareCap |
        MlineStyleFlags.EndInnerArcs |
        MlineStyleFlags.EndRoundCap;

    public static IReadOnlyList<string> Validate(MlineStyleDefinition? style)
    {
        var errors = new List<string>();
        if (style == null)
        {
            errors.Add("style.required");
            return errors;
        }

        if (InvalidText(style.Name, 32)) errors.Add("style.name.invalid");
        if (style.Description == null || style.Description.Length > 255 || HasControlCharacter(style.Description))
            errors.Add("style.description.invalid");
        if ((style.Flags & ~SupportedFlags) != 0) errors.Add("style.flags.unsupported");
        if (!ValidColor(style.FillColorIndex)) errors.Add("style.fillColor.invalid");
        if (!ValidAngle(style.StartAngleDegrees)) errors.Add("style.startAngle.invalid");
        if (!ValidAngle(style.EndAngleDegrees)) errors.Add("style.endAngle.invalid");
        if (style.Elements == null || style.Elements.Count < 1 || style.Elements.Count > MlnStyleParser.MaximumElementsPerStyle)
        {
            errors.Add("style.elements.count");
            return errors;
        }

        foreach (var element in style.Elements)
        {
            if (element == null)
            {
                errors.Add("style.element.required");
                continue;
            }
            if (!IsFinite(element.Offset) || Math.Abs(element.Offset) > 1.0e9)
                errors.Add("style.element.offset.invalid");
            if (!ValidColor(element.ColorIndex)) errors.Add("style.element.color.invalid");
            if (InvalidText(element.LinetypeName, 255)) errors.Add("style.element.linetype.invalid");
        }
        return errors;
    }

    private static bool ValidColor(int value) => value >= 0 && value <= 256;
    private static bool ValidAngle(double value) => IsFinite(value) && value > 0.0 && value < 180.0;
    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool InvalidText(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value) || value!.Length > maximumLength || HasControlCharacter(value);

    private static bool HasControlCharacter(string value)
    {
        foreach (var character in value)
            if (char.IsControl(character)) return true;
        return false;
    }
}

