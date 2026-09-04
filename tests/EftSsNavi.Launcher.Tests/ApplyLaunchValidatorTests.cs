using EftSsNavi.Launcher.Launching;

namespace EftSsNavi.Launcher.Tests;

public sealed class ApplyLaunchValidatorTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EftSsNaviApplyValidation", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ShouldAcceptTemporaryLauncherAndAuthenticatedCallerForSameDistribution()
    {
        var updates = Path.Combine(root, "local", "updates");
        var transaction = Path.Combine(updates, "tx");
        var distribution = Path.Combine(root, "dist");
        var temporaryLauncher = Path.Combine(transaction, "EftSsNavi.Update.exe");
        var callerPath = Path.Combine(distribution, "EftSsNavi.exe");
        Directory.CreateDirectory(transaction);
        Directory.CreateDirectory(Path.Combine(distribution, "app"));
        File.WriteAllText(temporaryLauncher, "temp");
        File.WriteAllText(callerPath, "launcher");
        File.WriteAllText(Path.Combine(distribution, "app", "EftSsNavi.App.exe"), "app");
        var arguments = new LaunchArguments(LaunchMode.ApplyUpdate, 42, 7, callerPath, null, transaction, distribution, "1.2.3");
        var validator = new ApplyLaunchValidator(temporaryLauncher, _ => new ProcessIdentity(7, callerPath), () => 7);

        Assert.True(validator.IsValid(arguments, updates));
    }

    [Fact]
    public void ShouldRejectApplyModeWhenExecutableIsNotTheTransactionLauncher()
    {
        var updates = Path.Combine(root, "local", "updates");
        var transaction = Path.Combine(updates, "tx");
        var distribution = Path.Combine(root, "dist");
        var callerPath = Path.Combine(distribution, "EftSsNavi.exe");
        Directory.CreateDirectory(transaction);
        Directory.CreateDirectory(Path.Combine(distribution, "app"));
        var arguments = new LaunchArguments(LaunchMode.ApplyUpdate, 42, 7, callerPath, null, transaction, distribution, "1.2.3");
        var validator = new ApplyLaunchValidator(Path.Combine(distribution, "EftSsNavi.exe"), _ => new ProcessIdentity(7, callerPath), () => 7);

        Assert.False(validator.IsValid(arguments, updates));
    }

    [Fact]
    public void ShouldRejectApplyModeWhenDistributionDoesNotMatchCaller()
    {
        var updates = Path.Combine(root, "local", "updates");
        var transaction = Path.Combine(updates, "tx");
        var distribution = Path.Combine(root, "dist");
        var callerPath = Path.Combine(root, "other", "EftSsNavi.exe");
        Directory.CreateDirectory(transaction);
        var temporaryLauncher = Path.Combine(transaction, "EftSsNavi.Update.exe");
        var arguments = new LaunchArguments(LaunchMode.ApplyUpdate, 42, 7, callerPath, null, transaction, distribution, "1.2.3");
        var validator = new ApplyLaunchValidator(temporaryLauncher, _ => new ProcessIdentity(7, callerPath), () => 7);

        Assert.False(validator.IsValid(arguments, updates));
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
