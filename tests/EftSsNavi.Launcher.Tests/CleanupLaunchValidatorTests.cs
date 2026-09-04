using EftSsNavi.Launcher.Launching;

namespace EftSsNavi.Launcher.Tests;

public sealed class CleanupLaunchValidatorTests
{
    [Fact]
    public void ShouldAcceptAuthenticatedTemporaryLauncherInTargetTransaction()
    {
        var updates = Path.Combine(Path.GetTempPath(), "updates");
        var transaction = Path.Combine(updates, "tx");
        var callerPath = Path.Combine(transaction, "EftSsNavi.Update.exe");
        var arguments = new LaunchArguments(LaunchMode.Cleanup, 42, 7, callerPath, TransactionDirectory: transaction);
        var validator = new CleanupLaunchValidator(_ => new ProcessIdentity(7, callerPath), () => 7);

        Assert.True(validator.IsValid(arguments, updates));
    }

    [Fact]
    public void ShouldRejectCallerOutsideTargetTransaction()
    {
        var updates = Path.Combine(Path.GetTempPath(), "updates");
        var transaction = Path.Combine(updates, "tx");
        var callerPath = Path.Combine(Path.GetTempPath(), "other", "EftSsNavi.Update.exe");
        var arguments = new LaunchArguments(LaunchMode.Cleanup, 42, 7, callerPath, TransactionDirectory: transaction);
        var validator = new CleanupLaunchValidator(_ => new ProcessIdentity(7, callerPath), () => 7);

        Assert.False(validator.IsValid(arguments, updates));
    }
}
