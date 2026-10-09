using System;
using System.Collections.Generic;
using System.IO;
using Atlas.Cad.Core.Styles;

namespace Atlas.AutoCAD.Adapter;

internal sealed class SecureCatalogStore
{
    private const string CatalogFileName = "steel.atlascatalog.json";
    private readonly string _root;

    public SecureCatalogStore()
    {
        _root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AtlasCad",
            "catalog",
            "v1");
    }

    public string CatalogPath => Path.Combine(EnsureRoot(), CatalogFileName);

    public IReadOnlyList<MlineStyleDefinition> Read()
    {
        var path = CatalogPath;
        var file = new FileInfo(path);
        if (!file.Exists)
            throw new BridgeCommandException("catalog_not_installed", "No Atlas style catalog is installed.");
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new BridgeCommandException("catalog_path_invalid", "The installed catalog cannot be a reparse point.");
        if (file.Length <= 0 || file.Length > MlineStyleCatalog.MaximumBytes)
            throw new BridgeCommandException("catalog_invalid", "The installed catalog size is invalid.");

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return MlineStyleCatalog.Read(stream);
        }
        catch (BridgeCommandException)
        {
            throw;
        }
        catch (System.Exception)
        {
            throw new BridgeCommandException("catalog_invalid", "The installed style catalog is invalid.");
        }
    }

    public int Install(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) ||
            !sourcePath.EndsWith(".atlascatalog.json", StringComparison.OrdinalIgnoreCase))
            throw new BridgeCommandException("catalog_path_invalid", "Select an .atlascatalog.json file.");

        var source = new FileInfo(Path.GetFullPath(sourcePath));
        if (!source.Exists)
            throw new BridgeCommandException("catalog_not_found", "The selected catalog was not found.");
        if ((source.Attributes & FileAttributes.ReparsePoint) != 0 ||
            source.Length <= 0 || source.Length > MlineStyleCatalog.MaximumBytes)
            throw new BridgeCommandException("catalog_invalid", "The selected catalog path or size is invalid.");

        byte[] payload;
        using (var stream = new FileStream(source.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            payload = new byte[checked((int)stream.Length)];
            var offset = 0;
            while (offset < payload.Length)
            {
                var read = stream.Read(payload, offset, payload.Length - offset);
                if (read == 0) throw new EndOfStreamException();
                offset += read;
            }
        }

        IReadOnlyList<MlineStyleDefinition> styles;
        try
        {
            using var validation = new MemoryStream(payload, writable: false);
            styles = MlineStyleCatalog.Read(validation);
        }
        catch (System.Exception)
        {
            throw new BridgeCommandException("catalog_invalid", "The selected style catalog is invalid.");
        }

        var destination = CatalogPath;
        var temporary = destination + ".tmp";
        if (File.Exists(temporary)) File.Delete(temporary);
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                output.Write(payload, 0, payload.Length);
                output.Flush();
            }

            if (File.Exists(destination))
                File.Replace(temporary, destination, null);
            else
                File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
        return styles.Count;
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
