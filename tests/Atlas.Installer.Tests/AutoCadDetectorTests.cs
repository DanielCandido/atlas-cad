using Atlas.Installer;
using Xunit;

namespace Atlas.Installer.Tests;

public sealed class AutoCadDetectorTests
{
    [Theory]
    [InlineData(2022, 24, 1, 0, "autocad-2022-2024", "AtlasCad.R24.bundle")]
    [InlineData(2024, 24, 3, 0, "autocad-2022-2024", "AtlasCad.R24.bundle")]
    [InlineData(2025, 25, 0, 100, "autocad-2025-2026-1-1", "AtlasCad.R25.0.bundle")]
    [InlineData(2026, 25, 1, 178, "autocad-2025-2026-1-1", "AtlasCad.R25.1.bundle")]
    [InlineData(2026, 25, 1, 179, "autocad-2026-1-2-plus", "AtlasCad.R25.1.bundle")]
    public void SelectsExpectedPayload(int year, int major, int minor, int build, string slug, string bundle)
    {
        var result = AutoCadDetector.Classify(year, new Version(major, minor, build, 0));
        Assert.Equal(slug, result.PayloadSlug);
        Assert.Equal(bundle, result.BundleName);
    }
}
