using EftSsNavi.Launcher.Launching;

namespace EftSsNavi.Launcher.Tests;

public sealed class LaunchModeParserTests
{
    [Fact]
    public void ShouldUseNormalModeWithoutArguments() => Assert.Equal(LaunchMode.Normal, LaunchArguments.Parse([]).Mode);

    [Fact]
    public void ShouldRejectManualModeWithoutAuthenticatedCallerData() =>
        Assert.Throws<ArgumentException>(() => LaunchArguments.Parse(["--manual-update", "--caller-pid", "12"]));

    [Fact]
    public void ShouldParseCompleteManualMode()
    {
        var parsed = LaunchArguments.Parse(["--manual-update", "--caller-pid", "12", "--caller-session-id", "3", "--caller-path", @"C:\dist\app\EftSsNavi.App.exe", "--shutdown-event", "Local\\EftSsNavi-abc"]);
        Assert.Equal(LaunchMode.Manual, parsed.Mode);
        Assert.Equal(12, parsed.CallerPid);
        Assert.Equal(3, parsed.CallerSessionId);
    }

    [Fact]
    public void ShouldRejectCleanupWithoutTransactionDirectory() =>
        Assert.Throws<ArgumentException>(() => LaunchArguments.Parse(["--cleanup", "--caller-pid", "12"]));

    [Fact]
    public void ShouldParseAuthenticatedCleanupMode()
    {
        var parsed = LaunchArguments.Parse(["--cleanup", "--caller-pid", "12", "--caller-session-id", "3", "--caller-path", @"C:\updates\tx\EftSsNavi.Update.exe", "--handoff-ready-event", "Local\\EftSsNavi.Cleanup.test", "--transaction-dir", @"C:\updates\tx"]);

        Assert.Equal(LaunchMode.Cleanup, parsed.Mode);
        Assert.Equal(3, parsed.CallerSessionId);
        Assert.Equal(@"C:\updates\tx\EftSsNavi.Update.exe", parsed.CallerPath);
    }

    [Fact]
    public void ShouldParseCompleteApplyMode()
    {
        var parsed = LaunchArguments.Parse(["--apply-update", "--caller-pid", "12", "--caller-session-id", "3", "--caller-path", @"C:\dist\EftSsNavi.exe", "--handoff-ready-event", "Local\\EftSsNavi.Handoff.test", "--transaction-dir", @"C:\updates\tx", "--distribution-root", @"C:\dist", "--target-version", "1.2.3"]);
        Assert.Equal(LaunchMode.ApplyUpdate, parsed.Mode);
        Assert.Equal(@"C:\dist", parsed.DistributionRoot);
        Assert.Equal("1.2.3", parsed.TargetVersion);
    }

    [Fact]
    public void ShouldRejectApplyModeWithoutAuthenticatedCallerData() =>
        Assert.Throws<ArgumentException>(() => LaunchArguments.Parse(["--apply-update", "--caller-pid", "12", "--transaction-dir", @"C:\updates\tx", "--distribution-root", @"C:\dist", "--target-version", "1.2.3"]));

    [Fact]
    public void ShouldParseUpdatedLauncherStartupVerificationMode() =>
        Assert.Equal(LaunchMode.VerifyStartup, LaunchArguments.Parse(["--verify-startup"]).Mode);
}
