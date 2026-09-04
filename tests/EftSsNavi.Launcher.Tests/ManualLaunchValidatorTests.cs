using EftSsNavi.Launcher.Launching;

namespace EftSsNavi.Launcher.Tests;

public sealed class ManualLaunchValidatorTests
{
    [Fact]
    public void ShouldAcceptRunningApplicationFromSameSessionAndDistribution()
    {
        var root = Path.GetFullPath(@"C:\dist");
        var args = new LaunchArguments(LaunchMode.Manual, 12, 3, Path.Combine(root, "app", "EftSsNavi.App.exe"), "Local\\random-event-name");
        var validator = new ManualLaunchValidator(_ => new ProcessIdentity(3, args.CallerPath!), () => 3);
        Assert.True(validator.IsValid(args, root));
    }

    [Fact]
    public void ShouldRejectCallerFromDifferentSession()
    {
        var root = Path.GetFullPath(@"C:\dist");
        var args = new LaunchArguments(LaunchMode.Manual, 12, 3, Path.Combine(root, "app", "EftSsNavi.App.exe"), "Local\\random-event-name");
        var validator = new ManualLaunchValidator(_ => new ProcessIdentity(4, args.CallerPath!), () => 3);
        Assert.False(validator.IsValid(args, root));
    }

    [Fact]
    public void ShouldRejectCallerOutsideDistributionEvenWhenClaimedPathIsInside()
    {
        var root = Path.GetFullPath(@"C:\dist");
        var expected = Path.Combine(root, "app", "EftSsNavi.App.exe");
        var args = new LaunchArguments(LaunchMode.Manual, 12, 3, expected, "Local\\random-event-name");
        var validator = new ManualLaunchValidator(_ => new ProcessIdentity(3, @"C:\other\EftSsNavi.App.exe"), () => 3);
        Assert.False(validator.IsValid(args, root));
    }

    [Fact]
    public void ShouldRejectCallerWhenClaimedAndActualSessionDifferFromLauncherSession()
    {
        var root = Path.GetFullPath(@"C:\dist"); var path = Path.Combine(root, "app", "EftSsNavi.App.exe");
        var args = new LaunchArguments(LaunchMode.Manual, 12, 4, path, "Local\\random-event-name");
        var validator = new ManualLaunchValidator(_ => new ProcessIdentity(4, path), () => 3);
        Assert.False(validator.IsValid(args, root));
    }
}
