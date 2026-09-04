namespace EftSsNavi.Launcher.Launching;

public sealed class DistributionInspector
{
    private readonly Func<string, string?> readFileVersion;
    public DistributionInspector() : this(path => System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileVersion) { }
    public DistributionInspector(Func<string, string?> readFileVersion) => this.readFileVersion = readFileVersion;

    public bool VersionsMatch(string distributionRoot)
    {
        try
        {
            var launcher = Normalize(readFileVersion(Path.Combine(distributionRoot, "EftSsNavi.exe")));
            var application = Normalize(readFileVersion(Path.Combine(distributionRoot, "app", "EftSsNavi.App.exe")));
            return launcher is not null && string.Equals(launcher, application, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    public bool CanWrite(string distributionRoot)
    {
        if (!Directory.Exists(distributionRoot)) return false;
        var probe = Path.Combine(Path.GetFullPath(distributionRoot), $".eftssnavi-write-{Guid.NewGuid():N}.tmp");
        try { using (File.Create(probe, 1, FileOptions.DeleteOnClose)) { } return !File.Exists(probe); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
        finally { try { if (File.Exists(probe)) File.Delete(probe); } catch { } }
    }
    private static string? Normalize(string? value) => Version.TryParse(value, out var version) && version.Build >= 0 ? new Version(version.Major, version.Minor, version.Build).ToString(3) : null;
}
