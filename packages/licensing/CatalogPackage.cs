using System;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace Atlas.Licensing;

[DataContract]
public sealed class CatalogPackageHeader
{
    [DataMember(Name = "packageVersion", IsRequired = true, Order = 1)] public int PackageVersion { get; set; }
    [DataMember(Name = "productId", IsRequired = true, Order = 2)] public string ProductId { get; set; } = string.Empty;
    [DataMember(Name = "catalogId", IsRequired = true, Order = 3)] public string CatalogId { get; set; } = string.Empty;
    [DataMember(Name = "catalogVersion", IsRequired = true, Order = 4)] public int CatalogVersion { get; set; }
    [DataMember(Name = "keyId", IsRequired = true, Order = 5)] public string KeyId { get; set; } = string.Empty;
    [DataMember(Name = "algorithm", IsRequired = true, Order = 6)] public string Algorithm { get; set; } = string.Empty;
    [DataMember(Name = "nonce", IsRequired = true, Order = 7)] public string Nonce { get; set; } = string.Empty;
    [DataMember(Name = "plaintextSha256", IsRequired = true, Order = 8)] public string PlaintextSha256 { get; set; } = string.Empty;
    [DataMember(Name = "createdAtUnix", IsRequired = true, Order = 9)] public long CreatedAtUnix { get; set; }
}

public sealed class CatalogKeyRing
{
    public int Version { get; set; } = 1;
    public string CurrentKeyId { get; set; } = string.Empty;
    public System.Collections.Generic.List<CatalogKeyEntry> Keys { get; set; } = new();
}

public sealed class CatalogKeyEntry
{
    public string KeyId { get; set; } = string.Empty;
    public string KeyMaterial { get; set; } = string.Empty;
}

public static class CatalogPackage
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("ATLCAT01");
    private const int TagBytes = 16;
    private const int NonceBytes = 12;
    public const long MaximumPackageBytes = 32L * 1024L * 1024L;

    public static void Protect(Stream output, byte[] plaintext, byte[] key, string keyId, int catalogVersion)
    {
#if NETFRAMEWORK
        throw new PlatformNotSupportedException("Protected catalog creation requires the modern .NET adapter.");
#else
        ValidateKey(key);
        if (output == null) throw new ArgumentNullException(nameof(output));
        if (plaintext == null || plaintext.Length == 0 || plaintext.Length > MaximumPackageBytes)
            throw new InvalidDataException("Catalog plaintext size is invalid.");
        if (!IsIdentifier(keyId) || catalogVersion <= 0) throw new InvalidDataException("Catalog metadata is invalid.");
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var header = new CatalogPackageHeader
        {
            PackageVersion = LicensingProtocol.CatalogPackageVersion,
            ProductId = LicensingProtocol.ProductId,
            CatalogId = "steel-mlines",
            CatalogVersion = catalogVersion,
            KeyId = keyId,
            Algorithm = LicensingProtocol.CatalogAlgorithm,
            Nonce = LicenseCrypto.Base64UrlEncode(nonce),
            PlaintextSha256 = LicenseCrypto.Base64UrlEncode(SHA256.HashData(plaintext)),
            CreatedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        var headerBytes = LicenseCrypto.Serialize(header, typeof(CatalogPackageHeader));
        var prefix = Prefix(headerBytes.Length, plaintext.Length);
        var aad = Join(prefix, headerBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagBytes];
        using (var aes = new AesGcm(key, TagBytes)) aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
        output.Write(prefix, 0, prefix.Length);
        output.Write(headerBytes, 0, headerBytes.Length);
        output.Write(ciphertext, 0, ciphertext.Length);
        output.Write(tag, 0, tag.Length);
#endif
    }

    public static CatalogPackageHeader ReadHeader(Stream input)
    {
        var parts = ReadParts(input, includeCiphertext: false);
        return parts.Header;
    }

    public static byte[] Decrypt(Stream input, byte[] key)
    {
#if NETFRAMEWORK
        throw new PlatformNotSupportedException("Protected catalog decryption requires the modern .NET adapter.");
#else
        ValidateKey(key);
        var parts = ReadParts(input, includeCiphertext: true);
        var plaintext = new byte[parts.Ciphertext!.Length];
        try
        {
            using var aes = new AesGcm(key, TagBytes);
            aes.Decrypt(LicenseCrypto.Base64UrlDecode(parts.Header.Nonce), parts.Ciphertext, parts.Tag!, plaintext, parts.Aad);
        }
        catch (CryptographicException)
        {
            throw new InvalidDataException("Catalog authentication failed.");
        }
        var hash = LicenseCrypto.Base64UrlEncode(SHA256.HashData(plaintext));
        if (!FixedTextEquals(hash, parts.Header.PlaintextSha256))
            throw new InvalidDataException("Catalog plaintext hash is invalid.");
        return plaintext;
#endif
    }

    private static PackageParts ReadParts(Stream input, bool includeCiphertext)
    {
        if (input == null || !input.CanRead) throw new ArgumentException("Catalog stream is not readable.", nameof(input));
        if (input.CanSeek && (input.Length <= 0 || input.Length > MaximumPackageBytes)) throw new InvalidDataException("Catalog package size is invalid.");
        var prefix = ReadExactly(input, 16);
        for (var index = 0; index < Magic.Length; index++) if (prefix[index] != Magic[index]) throw new InvalidDataException("Catalog package magic is invalid.");
        var headerLength = ReadInt32(prefix, 8);
        var ciphertextLength = ReadInt32(prefix, 12);
        if (headerLength <= 0 || headerLength > 64 * 1024 || ciphertextLength <= 0 || ciphertextLength > MaximumPackageBytes)
            throw new InvalidDataException("Catalog package lengths are invalid.");
        if (input.CanSeek && input.Length != 16L + headerLength + ciphertextLength + TagBytes)
            throw new InvalidDataException("Catalog package length does not match its envelope.");
        var headerBytes = ReadExactly(input, headerLength);
        var header = (CatalogPackageHeader)LicenseCrypto.Deserialize(headerBytes, typeof(CatalogPackageHeader));
        ValidateHeader(header);
        if (!includeCiphertext) return new PackageParts(header, Join(prefix, headerBytes), null, null);
        var ciphertext = ReadExactly(input, ciphertextLength);
        var tag = ReadExactly(input, TagBytes);
        if (input.ReadByte() != -1) throw new InvalidDataException("Catalog package has trailing data.");
        return new PackageParts(header, Join(prefix, headerBytes), ciphertext, tag);
    }

    private static void ValidateHeader(CatalogPackageHeader header)
    {
        if (header.PackageVersion != LicensingProtocol.CatalogPackageVersion ||
            !string.Equals(header.ProductId, LicensingProtocol.ProductId, StringComparison.Ordinal) ||
            !string.Equals(header.CatalogId, "steel-mlines", StringComparison.Ordinal) ||
            !string.Equals(header.Algorithm, LicensingProtocol.CatalogAlgorithm, StringComparison.Ordinal) ||
            header.CatalogVersion <= 0 || !IsIdentifier(header.KeyId) || header.CreatedAtUnix <= 0 ||
            LicenseCrypto.Base64UrlDecode(header.Nonce).Length != NonceBytes ||
            LicenseCrypto.Base64UrlDecode(header.PlaintextSha256).Length != 32)
            throw new InvalidDataException("Catalog package metadata is invalid.");
    }

    private static bool IsIdentifier(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 80 &&
        System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z0-9._-]+$");
    private static void ValidateKey(byte[] key) { if (key == null || key.Length != 32) throw new CryptographicException("Catalog key must contain 32 bytes."); }
    private static byte[] Prefix(int headerLength, int ciphertextLength)
    {
        var value = new byte[16]; Buffer.BlockCopy(Magic, 0, value, 0, 8); WriteInt32(value, 8, headerLength); WriteInt32(value, 12, ciphertextLength); return value;
    }
    private static void WriteInt32(byte[] value, int offset, int number) { value[offset] = (byte)(number >> 24); value[offset + 1] = (byte)(number >> 16); value[offset + 2] = (byte)(number >> 8); value[offset + 3] = (byte)number; }
    private static int ReadInt32(byte[] value, int offset) => (value[offset] << 24) | (value[offset + 1] << 16) | (value[offset + 2] << 8) | value[offset + 3];
    private static byte[] ReadExactly(Stream stream, int count) { var value = new byte[count]; var offset = 0; while (offset < count) { var read = stream.Read(value, offset, count - offset); if (read == 0) throw new EndOfStreamException(); offset += read; } return value; }
    private static byte[] Join(byte[] left, byte[] right) { var result = new byte[left.Length + right.Length]; Buffer.BlockCopy(left, 0, result, 0, left.Length); Buffer.BlockCopy(right, 0, result, left.Length, right.Length); return result; }
    private static bool FixedTextEquals(string left, string right)
    {
        var a = Encoding.ASCII.GetBytes(left); var b = Encoding.ASCII.GetBytes(right ?? string.Empty);
        if (a.Length != b.Length) return false;
#if NETFRAMEWORK
        var difference = 0; for (var index = 0; index < a.Length; index++) difference |= a[index] ^ b[index]; return difference == 0;
#else
        return CryptographicOperations.FixedTimeEquals(a, b);
#endif
    }
    private sealed class PackageParts { public PackageParts(CatalogPackageHeader header, byte[] aad, byte[]? ciphertext, byte[]? tag) { Header = header; Aad = aad; Ciphertext = ciphertext; Tag = tag; } public CatalogPackageHeader Header { get; } public byte[] Aad { get; } public byte[]? Ciphertext { get; } public byte[]? Tag { get; } }
}
