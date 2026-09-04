namespace EftSsNavi.App.Updates;

public interface IUpdateChecker
{
    Task<UpdateCheckResult> CheckAsync(
        Version currentVersion,
        string? ignoredVersion,
        CancellationToken cancellationToken = default);
}
