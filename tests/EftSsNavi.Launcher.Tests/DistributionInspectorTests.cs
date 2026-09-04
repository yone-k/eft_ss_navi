using EftSsNavi.Launcher.Launching;

namespace EftSsNavi.Launcher.Tests;

public sealed class DistributionInspectorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EftSsNaviDist", Guid.NewGuid().ToString("N"));
    [Fact]
    public void ShouldReportWritableDistributionWithoutLeavingProbeFile()
    {
        Directory.CreateDirectory(root);
        Assert.True(new DistributionInspector().CanWrite(root));
        Assert.Empty(Directory.EnumerateFileSystemEntries(root));
    }
    [Fact]
    public void ShouldRejectMissingDistributionDirectory() => Assert.False(new DistributionInspector().CanWrite(root));
    [Fact]
    public void ShouldRequireMatchingThreePartLauncherAndApplicationVersions()
    {
        var inspector = new DistributionInspector(path => path.EndsWith("EftSsNavi.exe", StringComparison.Ordinal) ? "1.2.3.0" : "1.2.3");
        Assert.True(inspector.VersionsMatch(root));
        inspector = new DistributionInspector(path => path.EndsWith("EftSsNavi.exe", StringComparison.Ordinal) ? "1.2.3" : "1.2.4");
        Assert.False(inspector.VersionsMatch(root));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
