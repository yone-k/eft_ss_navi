namespace EftSsNavi.App.Updates;

public static class UpdateCheckPolicy
{
#if DEBUG
    public const bool IsEnabled = false;
#else
    public const bool IsEnabled = true;
#endif

    public static bool ShouldRun(string? explicitDisableValue) =>
        IsEnabled && !string.Equals(explicitDisableValue, "1", StringComparison.Ordinal);
}
