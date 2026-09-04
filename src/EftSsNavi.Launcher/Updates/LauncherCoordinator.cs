using EftSsNavi.Launcher.State;

namespace EftSsNavi.Launcher.Updates;

public enum UpdateChoice { Update, Ignore, Later, Close }
public enum UpdateNotice { UpToDate, CheckFailed, FailedVersionOnCooldown, DistributionNotWritable, VersionMismatch, RecoveryFailed, ApplicationExitTimedOut }
public enum LauncherAction { StartApplication, KeepApplication, ApplyUpdate }
public sealed record LauncherRunResult(LauncherAction Action, UpdateCandidate? Candidate = null);

public interface IUpdateUserInterface
{
    UpdateChoice Choose(UpdateCheckMode mode, UpdateCandidate candidate);
    void Notify(UpdateNotice notice);
}

public sealed class LauncherCoordinator(
    IUpdateChecker checker,
    ILauncherStateStore stateStore,
    IUpdateUserInterface userInterface,
    Func<DateTimeOffset> getNow)
{
    public async Task<LauncherRunResult> RunAsync(UpdateCheckMode mode, Version currentVersion, CancellationToken cancellationToken = default)
    {
        var state = await stateStore.LoadAsync(cancellationToken);
        var fallback = mode == UpdateCheckMode.Automatic ? LauncherAction.StartApplication : LauncherAction.KeepApplication;
        var now = getNow();
        if (!UpdateEligibility.ShouldCheck(mode, state, now)) return new(fallback);
        var check = await checker.CheckAsync(currentVersion, cancellationToken);
        state = state with { LastCheckedAt = now };
        await stateStore.SaveAsync(state, cancellationToken);
        if (check.Status != UpdateCheckStatus.UpdateAvailable || check.Candidate is null)
        {
            if (mode == UpdateCheckMode.Manual && check.Status != UpdateCheckStatus.Canceled)
                userInterface.Notify(check.Status == UpdateCheckStatus.UpToDate ? UpdateNotice.UpToDate : UpdateNotice.CheckFailed);
            return new(fallback);
        }
        if (!UpdateEligibility.CanOffer(check.Candidate.NormalizedVersion, mode, state, now))
        {
            if (mode == UpdateCheckMode.Manual) userInterface.Notify(UpdateNotice.FailedVersionOnCooldown);
            return new(fallback);
        }
        var choice = userInterface.Choose(mode, check.Candidate);
        if (choice == UpdateChoice.Update) return new(LauncherAction.ApplyUpdate, check.Candidate);
        if (mode == UpdateCheckMode.Automatic && choice == UpdateChoice.Ignore)
            await stateStore.SaveAsync(state with { IgnoredVersion = check.Candidate.NormalizedVersion }, cancellationToken);
        return new(fallback);
    }
}
