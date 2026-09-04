using EftSsNavi.Launcher.Launching;

namespace EftSsNavi.Launcher.Tests;

public sealed class UpdateHandoffTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EftSsNaviHandoff", Guid.NewGuid().ToString("N"));
    [Fact]
    public void ShouldCopyLauncherAndStartApplyModeWithValidatedPaths()
    {
        var distribution = Path.Combine(root, "dist"); var transaction = Path.Combine(root, "updates", "tx");
        Directory.CreateDirectory(distribution); Directory.CreateDirectory(transaction); File.WriteAllText(Path.Combine(distribution, "EftSsNavi.exe"), "launcher");
        string? executable = null; IReadOnlyList<string>? args = null;
        new UpdateHandoff((path, arguments) =>
        {
            executable = path;
            args = arguments;
            var eventName = arguments[arguments.ToList().IndexOf("--handoff-ready-event") + 1];
            using var ready = EventWaitHandle.OpenExisting(eventName);
            ready.Set();
        }).Start(distribution, transaction, "1.2.3", 42, 7, Path.Combine(distribution, "EftSsNavi.exe"), null);
        Assert.True(File.Exists(executable)); Assert.Equal("--apply-update", args?[0]);
        Assert.Contains(distribution, args!); Assert.Contains(transaction, args!); Assert.Contains("1.2.3", args!);
        Assert.Contains("--caller-session-id", args!); Assert.Contains("7", args!);
        Assert.Contains("--caller-path", args!); Assert.Contains(Path.Combine(distribution, "EftSsNavi.exe"), args!);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
