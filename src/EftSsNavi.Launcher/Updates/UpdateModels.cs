namespace EftSsNavi.Launcher.Updates;

public enum UpdateCheckStatus { UpdateAvailable, UpToDate, Failed, Canceled }

public sealed record UpdateCandidate(
    string DisplayVersion,
    string NormalizedVersion,
    Uri DownloadUri,
    string Sha256);

public sealed record UpdateCheckResult(UpdateCheckStatus Status, UpdateCandidate? Candidate = null)
{
    public static UpdateCheckResult Available(UpdateCandidate candidate) => new(UpdateCheckStatus.UpdateAvailable, candidate);
    public static UpdateCheckResult UpToDate { get; } = new(UpdateCheckStatus.UpToDate);
    public static UpdateCheckResult Failed { get; } = new(UpdateCheckStatus.Failed);
    public static UpdateCheckResult Canceled { get; } = new(UpdateCheckStatus.Canceled);
}

public interface IUpdateChecker
{
    Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default);
}
