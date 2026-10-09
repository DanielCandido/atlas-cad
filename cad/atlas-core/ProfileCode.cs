using System;
using System.Text.RegularExpressions;
namespace Atlas.Cad.Core;
public static class ProfileCode
{
    // Normalização inicial; regras reais precisam ser extraídas dos LSP/MLN.
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("Perfil obrigatório", nameof(input));
        return Regex.Replace(input.ToUpperInvariant(), @"[\s"".,-]", "");
    }
}
