using System.Text;
using System.Text.Json;
using Atlas.Cad.Core.Styles;
using Atlas.Licensing;

if (args.Length > 0 && string.Equals(args[0], "protect", StringComparison.OrdinalIgnoreCase))
    return ProtectCatalog(args);

if (args.Length != 2 && args.Length != 4)
{
    Console.Error.WriteLine("Usage: Atlas.CatalogBuilder <MLN directory> <output.atlascatalog.json> [--overrides <file.json>]");
    return 2;
}
if (args.Length == 4 && !string.Equals(args[2], "--overrides", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Expected --overrides before the override file path.");
    return 2;
}

var inputDirectory = Path.GetFullPath(args[0]);
var outputPath = Path.GetFullPath(args[1]);
if (!Directory.Exists(inputDirectory))
{
    Console.Error.WriteLine("Input MLN directory does not exist.");
    return 2;
}
if (!outputPath.EndsWith(".atlascatalog.json", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Output must use the private .atlascatalog.json extension.");
    return 2;
}

var files = Directory.EnumerateFiles(inputDirectory, "*", SearchOption.TopDirectoryOnly)
    .Where(path => string.Equals(Path.GetExtension(path), ".mln", StringComparison.OrdinalIgnoreCase))
    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
    .ToArray();
if (files.Length == 0)
{
    Console.Error.WriteLine("No MLN files were found.");
    return 2;
}
if (files.Length > 10000)
{
    Console.Error.WriteLine("MLN file count exceeds the safety limit.");
    return 2;
}

try
{
    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var overrides = args.Length == 4 ? ReadOverrides(Path.GetFullPath(args[3])) : new CatalogOverrides();
    var overrideMap = overrides.Renames.ToDictionary(
        item => OverrideKey(item.SourceFile, item.OriginalName),
        item => item,
        StringComparer.OrdinalIgnoreCase);
    var usedOverrides = new HashSet<StyleRenameOverride>();
    var sources = new List<MlnParsedCatalogSource>();
    foreach (var path in files)
    {
        var sourceId = Path.GetFileName(path);
        var parsed = MlnStyleParser.Parse(ReadMlnText(path));
        foreach (var style in parsed)
        {
            if (!overrideMap.TryGetValue(OverrideKey(sourceId, style.Name), out var rename))
                continue;
            style.Name = rename.NewName.Trim();
            if (rename.NewDescription != null) style.Description = rename.NewDescription.Trim();
            var errors = MlineStyleDefinitionValidator.Validate(style);
            if (errors.Count > 0)
                throw new InvalidOperationException("Invalid collision override for " + sourceId + ": " + string.Join(",", errors));
            usedOverrides.Add(rename);
        }
        sources.Add(new MlnParsedCatalogSource(sourceId, parsed));
    }
    if (usedOverrides.Count != overrides.Renames.Count)
        throw new InvalidOperationException("One or more collision overrides did not match an MLN style.");
    var styles = MlnCatalogNormalizer.NormalizeParsed(sources).ToList();

    var outputDirectory = Path.GetDirectoryName(outputPath)!;
    Directory.CreateDirectory(outputDirectory);
    var temporaryPath = outputPath + ".tmp";
    if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
    try
    {
        using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            MlineStyleCatalog.Write(stream, styles);
        File.Move(temporaryPath, outputPath, overwrite: true);
    }
    finally
    {
        if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
    }

    Console.WriteLine($"Catalog normalized: files={files.Length}; styles={styles.Count}; output={outputPath}");
    return 0;
}
catch (MlnCatalogCollisionException exception)
{
    Console.Error.WriteLine($"Catalog rejected: {exception.Kind}; style={exception.StyleName}; sources={exception.FirstSourceId},{exception.SecondSourceId}");
    return 3;
}
catch (MlnParseException exception)
{
    Console.Error.WriteLine("Catalog rejected: " + exception.Message);
    return 3;
}
catch (InvalidOperationException exception)
{
    Console.Error.WriteLine("Catalog rejected: " + exception.Message);
    return 3;
}

static string ReadMlnText(string path)
{
    var bytes = File.ReadAllBytes(path);
    try
    {
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
    }
    catch (DecoderFallbackException)
    {
        return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback).GetString(bytes);
    }
}

static CatalogOverrides ReadOverrides(string path)
{
    if (!File.Exists(path)) throw new InvalidOperationException("Override file does not exist.");
    if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidOperationException("Override file exceeds the size limit.");
    var result = JsonSerializer.Deserialize<CatalogOverrides>(
        File.ReadAllText(path),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ??
        throw new InvalidOperationException("Override file is empty.");
    if (result.Version != 1) throw new InvalidOperationException("Unsupported override file version.");
    if (result.Renames == null) throw new InvalidOperationException("Override rename list is required.");
    foreach (var item in result.Renames)
        if (string.IsNullOrWhiteSpace(item.SourceFile) || string.IsNullOrWhiteSpace(item.OriginalName) || string.IsNullOrWhiteSpace(item.NewName))
            throw new InvalidOperationException("Every override requires sourceFile, originalName and newName.");
    return result;
}

static string OverrideKey(string sourceFile, string styleName) => sourceFile.Trim() + "\n" + styleName.Trim();

static int ProtectCatalog(string[] arguments)
{
    if (arguments.Length != 5 || !int.TryParse(arguments[4], out var catalogVersion) || catalogVersion <= 0)
    {
        Console.Error.WriteLine("Usage: Atlas.CatalogBuilder protect <input.atlascatalog.json> <output.atlascatalog> <key-ring.json> <catalog-version>");
        return 2;
    }
    var inputPath = Path.GetFullPath(arguments[1]);
    var outputPath = Path.GetFullPath(arguments[2]);
    var keyPath = Path.GetFullPath(arguments[3]);
    if (!File.Exists(inputPath) || !File.Exists(keyPath)) throw new InvalidOperationException("Catalog input or key ring was not found.");
    if (!inputPath.EndsWith(".atlascatalog.json", StringComparison.OrdinalIgnoreCase) ||
        !outputPath.EndsWith(".atlascatalog", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Protected output must use .atlascatalog and input must use .atlascatalog.json.");
    var plaintext = File.ReadAllBytes(inputPath);
    using (var validation = new MemoryStream(plaintext, writable: false)) MlineStyleCatalog.Read(validation);
    var ring = JsonSerializer.Deserialize<CatalogKeyRing>(File.ReadAllText(keyPath),
        new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true }) ??
        throw new InvalidOperationException("Catalog key ring is empty.");
    if (ring.Version != 1 || string.IsNullOrWhiteSpace(ring.CurrentKeyId)) throw new InvalidOperationException("Catalog key ring is invalid.");
    var entry = ring.Keys.SingleOrDefault(item => string.Equals(item.KeyId, ring.CurrentKeyId, StringComparison.Ordinal)) ??
        throw new InvalidOperationException("Current catalog key is missing from the key ring.");
    var key = LicenseCrypto.Base64UrlDecode(entry.KeyMaterial);
    var directory = Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    var temporary = outputPath + ".tmp";
    if (File.Exists(temporary)) File.Delete(temporary);
    try
    {
        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            CatalogPackage.Protect(output, plaintext, key, entry.KeyId, catalogVersion);
        File.Move(temporary, outputPath, overwrite: true);
    }
    finally { if (File.Exists(temporary)) File.Delete(temporary); }
    Console.WriteLine($"Catalog protected: version={catalogVersion}; key={entry.KeyId}; output={outputPath}");
    return 0;
}

public sealed class CatalogOverrides
{
    public int Version { get; set; } = 1;
    public List<StyleRenameOverride> Renames { get; set; } = new();
}

public sealed class StyleRenameOverride
{
    public string SourceFile { get; set; } = string.Empty;
    public string OriginalName { get; set; } = string.Empty;
    public string NewName { get; set; } = string.Empty;
    public string? NewDescription { get; set; }
}
