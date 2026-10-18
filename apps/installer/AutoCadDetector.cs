using System.Diagnostics;
using Microsoft.Win32;

namespace Atlas.Installer;

internal sealed record AutoCadInstallation(int Year, string DisplayVersion, string ExecutablePath, string PayloadSlug, string BundleName)
{
    public string Label => $"AutoCAD {Year} ({DisplayVersion})";
}

internal static class AutoCadDetector
{
    public static IReadOnlyList<AutoCadInstallation> Detect()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var year in Enumerable.Range(2022, 5))
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Autodesk", $"AutoCAD {year}", "acad.exe");
            if (File.Exists(path)) paths.Add(path);
        }
        ReadUninstall(RegistryHive.LocalMachine, RegistryView.Registry64, paths);
        ReadUninstall(RegistryHive.CurrentUser, RegistryView.Registry64, paths);
        return paths.Select(Create).Where(value => value != null).Cast<AutoCadInstallation>()
            .OrderBy(value => value.Year).ToArray();
    }

    internal static AutoCadInstallation? Create(string executable)
    {
        var match = System.Text.RegularExpressions.Regex.Match(executable, @"AutoCAD\s+(20\d{2})", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var year) || year is < 2022 or > 2026) return null;
        var raw = FileVersionInfo.GetVersionInfo(executable).FileVersion ?? "0.0.0.0";
        var numeric = raw.Split(' ', '-', '+')[0];
        Version.TryParse(numeric, out var version);
        return Classify(year, version, raw, executable);
    }

    internal static AutoCadInstallation Classify(int year, Version? version, string displayVersion = "test", string executable = "acad.exe")
    {
        if (year <= 2024) return new(year, displayVersion, executable, "autocad-2022-2024", "AtlasCad.R24.bundle");
        if (year == 2025) return new(year, displayVersion, executable, "autocad-2025-2026-1-1", "AtlasCad.R25.0.bundle");
        var modern = version != null && version >= new Version(25, 1, 179, 0);
        return new(year, displayVersion, executable, modern ? "autocad-2026-1-2-plus" : "autocad-2025-2026-1-1", "AtlasCad.R25.1.bundle");
    }

    private static void ReadUninstall(RegistryHive hive, RegistryView view, HashSet<string> paths)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var root = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
            if (root == null) return;
            foreach (var name in root.GetSubKeyNames())
            {
                using var key = root.OpenSubKey(name);
                var display = key?.GetValue("DisplayName") as string;
                var location = key?.GetValue("InstallLocation") as string;
                if (display?.Contains("AutoCAD 20", StringComparison.OrdinalIgnoreCase) != true || string.IsNullOrWhiteSpace(location)) continue;
                var executable = Path.Combine(location, "acad.exe");
                if (File.Exists(executable)) paths.Add(executable);
            }
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or System.Security.SecurityException) { }
    }
}
