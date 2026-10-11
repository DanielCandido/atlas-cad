using System.Security.Cryptography;

namespace Atlas.Licensing.Api;

internal static class PasswordHasher
{
    private const int Iterations = 210_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string encoded)
    {
        var fields = encoded.Split('$');
        if (fields.Length != 4 || fields[0] != "pbkdf2-sha256" || !int.TryParse(fields[1], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(fields[2]);
            var expected = Convert.FromBase64String(fields[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}
