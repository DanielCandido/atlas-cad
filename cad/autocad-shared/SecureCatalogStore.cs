using System;
using System.Collections.Generic;
using System.IO;
using Atlas.Cad.Core.Styles;
using Atlas.Licensing;

namespace Atlas.AutoCAD.Adapter;

internal sealed class SecureCatalogStore
{
    private const string CatalogFileName = "steel.atlascatalog";
    private readonly string _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AtlasCad", "catalog", "v2");

    public string CatalogPath => Path.Combine(EnsureRoot(), CatalogFileName);

    public CatalogPackageHeader Inspect() => InspectPath(CatalogPath, installed: true);

    public IReadOnlyList<MlineStyleDefinition> Read(byte[] key)
    {
        var path = CatalogPath;
        InspectPath(path, installed: true);
        byte[]? plaintext = null;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            plaintext = CatalogPackage.Decrypt(stream, key);
            using var catalog = new MemoryStream(plaintext, writable: false);
            return MlineStyleCatalog.Read(catalog);
        }
        catch (BridgeCommandException) { throw; }
        catch (InvalidDataException)
        {
            throw new BridgeCommandException("catalog_authentication_failed", "Protected catalog authentication failed.");
        }
        catch (System.Exception)
        {
            throw new BridgeCommandException("catalog_invalid", "Installed protected catalog is invalid.");
        }
        finally { if (plaintext != null) Array.Clear(plaintext, 0, plaintext.Length); }
    }

    public int Install(string sourcePath, byte[] key)
    {
        InspectPath(sourcePath, installed: false);
        byte[]? plaintext = null;
        IReadOnlyList<MlineStyleDefinition> styles;
        try
        {
            using var input = new FileStream(Path.GetFullPath(sourcePath), FileMode.Open, FileAccess.Read, FileShare.Read);
            plaintext = CatalogPackage.Decrypt(input, key);
            using var validation = new MemoryStream(plaintext, writable: false);
            styles = MlineStyleCatalog.Read(validation);
        }
        catch (InvalidDataException)
        {
            throw new BridgeCommandException("catalog_authentication_failed", "Protected catalog authentication failed.");
        }
        catch (System.Exception)
        {
            throw new BridgeCommandException("catalog_invalid", "Protected catalog payload is invalid.");
        }
        finally { if (plaintext != null) Array.Clear(plaintext, 0, plaintext.Length); }

        var destination = CatalogPath;
        var temporary = destination + ".tmp";
        if (File.Exists(temporary)) File.Delete(temporary);
        try
        {
            File.Copy(Path.GetFullPath(sourcePath), temporary, overwrite: false);
            if (File.Exists(destination))
            {
#if NETFRAMEWORK
                File.Delete(destination);
                File.Move(temporary, destination);
#else
                File.Move(temporary, destination, true);
#endif
            }
            else File.Move(temporary, destination);
            DeleteLegacyPlaintextCatalog();
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return styles.Count;
    }

    private static void DeleteLegacyPlaintextCatalog()
    {
        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtlasCad", "catalog", "v1", "steel.atlascatalog.json");
        if (File.Exists(legacy)) File.Delete(legacy);
    }

    public CatalogPackageHeader InspectSource(string sourcePath) => InspectPath(sourcePath, installed: false);

    private static CatalogPackageHeader InspectPath(string path, bool installed)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".atlascatalog", StringComparison.OrdinalIgnoreCase))
            throw new BridgeCommandException("catalog_path_invalid", "Select an encrypted .atlascatalog package.");
        var file = new FileInfo(Path.GetFullPath(path));
        if (!file.Exists)
            throw new BridgeCommandException(installed ? "catalog_not_installed" : "catalog_not_found", "Protected catalog was not found.");
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0 || file.Length <= 0 || file.Length > CatalogPackage.MaximumPackageBytes)
            throw new BridgeCommandException("catalog_invalid", "Protected catalog path or size is invalid.");
        try
        {
            using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
            return CatalogPackage.ReadHeader(stream);
        }
        catch (BridgeCommandException) { throw; }
        catch (System.Exception) { throw new BridgeCommandException("catalog_invalid", "Protected catalog envelope is invalid."); }
    }

    private string EnsureRoot()
    {
        var directory = Directory.CreateDirectory(_root);
        if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new BridgeCommandException("catalog_path_invalid", "Catalog directory cannot be a reparse point.");
        SecureBridgeFileStore.RestrictToCurrentUser(directory);
        return directory.FullName;
    }
}
