using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Win32;

namespace Atlas.Installer;

internal static class BundleInstaller
{
    private static readonly string[] ManagedBundleNames =
    [
        "AtlasCad.R24.bundle",
        "AtlasCad.R25.0.bundle",
        "AtlasCad.R25.1.bundle",
        "AtlasCad.bundle"
    ];

    public static string Install(IReadOnlyList<AutoCadInstallation> installations)
    {
        using var payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("Atlas.Installer.Payload.zip") ??
            throw new InvalidOperationException("O instalador não contém os componentes do ATLAS CAD.");
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "AtlasCad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            using var archive = new ZipArchive(payload, ZipArchiveMode.Read);
            foreach (var installation in installations.GroupBy(value => value.BundleName).Select(group => group.First()))
            {
                var source = Path.Combine(temporaryRoot, installation.BundleName);
                ExtractBundle(archive, installation.PayloadSlug + "/AtlasCad.bundle/", source);
                ValidateInventory(source);
                TailorManifest(source, installation.BundleName);
                WriteInventory(source);
                InstallTransactional(source, installation.BundleName);
            }
            var requiredBundles = installations.Select(value => value.BundleName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            RemoveManagedBundles(ApplicationPluginsRoot(), requiredBundles);
            RegisterUninstall();
            return installations.Select(value => value.Label).Distinct().Aggregate((left, right) => left + ", " + right);
        }
        finally
        {
            try { Directory.Delete(temporaryRoot, recursive: true); } catch { }
        }
    }

    public static string FindInstalledCatalog(IReadOnlyList<AutoCadInstallation> installations)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "ApplicationPlugins");
        foreach (var item in installations)
        {
            var catalog = Directory.EnumerateFiles(Path.Combine(root, item.BundleName), "steel.atlascatalog", SearchOption.AllDirectories).FirstOrDefault();
            if (catalog != null) return catalog;
        }
        throw new FileNotFoundException("Catálogo protegido não encontrado após a instalação.");
    }

    public static void Uninstall()
    {
        if (System.Diagnostics.Process.GetProcessesByName("acad").Length > 0)
            throw new InvalidOperationException("Feche todas as janelas do AutoCAD antes de desinstalar.");
        RemoveManagedBundles(ApplicationPluginsRoot(), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\AtlasCad", throwOnMissingSubKey: false);
    }

    internal static void RemoveManagedBundles(string root, IReadOnlySet<string> keep)
    {
        foreach (var name in ManagedBundleNames)
        {
            var target = Path.GetFullPath(Path.Combine(root, name));
            var expected = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(expected, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Destino de desinstalação inválido.");
            if (!keep.Contains(name) && Directory.Exists(target)) Directory.Delete(target, true);
        }
    }

    internal static void ExtractBundle(ZipArchive archive, string prefix, string target)
    {
        prefix = prefix.Replace('\\', '/');
        var fullTarget = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var entry in archive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!entryPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || entryPath.EndsWith('/')) continue;
            var relative = entryPath[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var destination = Path.GetFullPath(Path.Combine(target, relative));
            if (!destination.StartsWith(fullTarget, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Caminho inválido no pacote.");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var input = entry.Open();
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
        if (!File.Exists(Path.Combine(target, "PackageContents.xml"))) throw new InvalidDataException("Bundle ausente no pacote.");
    }

    private static void ValidateInventory(string bundle)
    {
        var inventory = Path.Combine(bundle, "SHA256SUMS.txt");
        if (!File.Exists(inventory)) throw new InvalidDataException("Inventário SHA-256 ausente.");
        var prefix = Path.GetFullPath(bundle).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        foreach (var line in File.ReadLines(inventory))
        {
            var split = line.IndexOf("  ", StringComparison.Ordinal);
            if (split != 64) throw new InvalidDataException("Inventário SHA-256 inválido.");
            var path = Path.GetFullPath(Path.Combine(bundle, line[(split + 2)..].Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) throw new InvalidDataException("Arquivo do bundle ausente.");
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
            if (!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(actual), System.Text.Encoding.ASCII.GetBytes(line[..64])))
                throw new InvalidDataException("A integridade de um componente falhou.");
        }
    }

    private static void TailorManifest(string bundle, string bundleName)
    {
        var manifestPath = Path.Combine(bundle, "PackageContents.xml");
        var document = System.Xml.Linq.XDocument.Load(manifestPath, System.Xml.Linq.LoadOptions.PreserveWhitespace);
        var requirements = document.Descendants("RuntimeRequirements").Single();
        var range = bundleName switch
        {
            "AtlasCad.R24.bundle" => (Min: "R24.1", Max: "R24.3"),
            "AtlasCad.R25.0.bundle" => (Min: "R25.0", Max: "R25.0"),
            "AtlasCad.R25.1.bundle" => (Min: "R25.1", Max: "R25.1"),
            _ => throw new InvalidDataException("Família de bundle desconhecida.")
        };
        requirements.SetAttributeValue("SeriesMin", range.Min);
        requirements.SetAttributeValue("SeriesMax", range.Max);
        var productSeed = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("AtlasCad:" + bundleName));
        document.Root!.SetAttributeValue("ProductCode", "{" + new Guid(productSeed[..16]).ToString().ToUpperInvariant() + "}");
        document.Save(manifestPath, System.Xml.Linq.SaveOptions.DisableFormatting);
    }

    private static void WriteInventory(string bundle)
    {
        var lines = Directory.EnumerateFiles(bundle, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) + "  " +
                Path.GetRelativePath(bundle, path).Replace(Path.DirectorySeparatorChar, '/'));
        File.WriteAllLines(Path.Combine(bundle, "SHA256SUMS.txt"), lines, new System.Text.UTF8Encoding(false));
    }

    private static void InstallTransactional(string source, string bundleName)
    {
        var root = ApplicationPluginsRoot();
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, bundleName);
        var staging = Path.Combine(root, bundleName + ".installing-" + Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(root, bundleName + ".backup-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(source, staging);
            ValidateInventory(staging);
            if (Directory.Exists(target)) Directory.Move(target, backup);
            Directory.Move(staging, target);
            if (Directory.Exists(backup)) Directory.Delete(backup, true);
        }
        catch
        {
            if (!Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target);
            throw;
        }
        finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var directory in Directory.EnumerateDirectories(source)) CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    private static string ApplicationPluginsRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "ApplicationPlugins");

    private static void RegisterUninstall()
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\AtlasCad");
        var runningExecutable = Environment.ProcessPath ?? throw new InvalidOperationException("Caminho do instalador indisponível.");
        var installerDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtlasCad", "installer");
        SecureDirectory.Create(installerDirectory);
        var executable = Path.Combine(installerDirectory, "Instalador-AtlasCad.exe");
        if (!Path.GetFullPath(runningExecutable).Equals(Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase))
            File.Copy(runningExecutable, executable, true);
        key.SetValue("DisplayName", "ATLAS CAD");
        key.SetValue("DisplayVersion", Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0");
        key.SetValue("Publisher", "ATLAS CAD");
        key.SetValue("UninstallString", $"\"{executable}\" --uninstall");
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
    }
}
