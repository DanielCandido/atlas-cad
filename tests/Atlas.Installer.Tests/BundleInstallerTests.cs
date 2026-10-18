using System.IO.Compression;
using Atlas.Installer;
using Xunit;

namespace Atlas.Installer.Tests;

public sealed class BundleInstallerTests
{
    [Theory]
    [InlineData("autocad-2026-1-2-plus/AtlasCad.bundle/PackageContents.xml")]
    [InlineData("autocad-2026-1-2-plus\\AtlasCad.bundle\\PackageContents.xml")]
    public void ExtractBundleAcceptsBothZipPathSeparators(string entryName)
    {
        using var archiveStream = new MemoryStream();
        using (var writer = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = writer.CreateEntry(entryName);
            using var output = new StreamWriter(entry.Open());
            output.Write("<ApplicationPackage />");
        }

        archiveStream.Position = 0;
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);
        var target = Path.Combine(Path.GetTempPath(), "AtlasCad-installer-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            BundleInstaller.ExtractBundle(archive, "autocad-2026-1-2-plus/AtlasCad.bundle/", target);
            Assert.True(File.Exists(Path.Combine(target, "PackageContents.xml")));
        }
        finally
        {
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
        }
    }

    [Fact]
    public void RemoveManagedBundlesRemovesPreviousVersionsAndPreservesRequiredBundle()
    {
        var root = Path.Combine(Path.GetTempPath(), "AtlasCad-cleanup-test-" + Guid.NewGuid().ToString("N"));
        var managed = new[] { "AtlasCad.bundle", "AtlasCad.R24.bundle", "AtlasCad.R25.0.bundle", "AtlasCad.R25.1.bundle" };
        try
        {
            foreach (var name in managed) Directory.CreateDirectory(Path.Combine(root, name));
            Directory.CreateDirectory(Path.Combine(root, "AnotherVendor.bundle"));

            BundleInstaller.RemoveManagedBundles(root, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AtlasCad.R25.1.bundle" });

            Assert.True(Directory.Exists(Path.Combine(root, "AtlasCad.R25.1.bundle")));
            Assert.True(Directory.Exists(Path.Combine(root, "AnotherVendor.bundle")));
            Assert.False(Directory.Exists(Path.Combine(root, "AtlasCad.bundle")));
            Assert.False(Directory.Exists(Path.Combine(root, "AtlasCad.R24.bundle")));
            Assert.False(Directory.Exists(Path.Combine(root, "AtlasCad.R25.0.bundle")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
