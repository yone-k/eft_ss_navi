namespace EftSsNavi.App.Monitoring;

public sealed class DefaultScreenshotDirectoryProvider
{
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<string, bool> _directoryExists;

    public DefaultScreenshotDirectoryProvider()
        : this(Environment.GetEnvironmentVariable, Directory.Exists)
    {
    }

    public DefaultScreenshotDirectoryProvider(
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(directoryExists);

        _getEnvironmentVariable = getEnvironmentVariable;
        _directoryExists = directoryExists;
    }

    public string? GetDefaultDirectory()
    {
        var userProfile = _getEnvironmentVariable("USERPROFILE");
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return null;
        }

        var candidate = Path.Combine(
            userProfile,
            "Documents",
            "Escape from Tarkov",
            "Screenshots");

        return _directoryExists(candidate) ? candidate : null;
    }
}
