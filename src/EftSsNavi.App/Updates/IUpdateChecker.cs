namespace EftSsNavi.App.Updates;

public interface IUpdateChecker
{
    Task<UpdateCandidate?> CheckAsync(
        Version currentVersion,
        string? ignoredVersion,
        CancellationToken cancellationToken = default);
}
