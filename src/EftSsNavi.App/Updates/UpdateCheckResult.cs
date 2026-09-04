namespace EftSsNavi.App.Updates;

public enum UpdateCheckStatus
{
    UpdateAvailable,
    UpToDate,
    Failed,
    Canceled,
    Suppressed,
}

public sealed record UpdateCheckResult
{
    private UpdateCheckResult(UpdateCheckStatus status, UpdateCandidate? candidate)
    {
        Status = status;
        Candidate = candidate;
    }

    public UpdateCheckStatus Status { get; }

    public UpdateCandidate? Candidate { get; }

    public static UpdateCheckResult UpToDate { get; } =
        new(UpdateCheckStatus.UpToDate, candidate: null);

    public static UpdateCheckResult Failed { get; } =
        new(UpdateCheckStatus.Failed, candidate: null);

    public static UpdateCheckResult Canceled { get; } =
        new(UpdateCheckStatus.Canceled, candidate: null);

    public static UpdateCheckResult Suppressed { get; } =
        new(UpdateCheckStatus.Suppressed, candidate: null);

    public static UpdateCheckResult Available(UpdateCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, candidate);
    }
}
