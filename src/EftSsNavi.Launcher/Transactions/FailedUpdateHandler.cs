using System.Diagnostics;
using EftSsNavi.Launcher.State;

namespace EftSsNavi.Launcher.Transactions;

public sealed class FailedUpdateHandler
{
    private readonly ILauncherStateStore stateStore;
    private readonly Action<string> startApplication;
    private readonly Func<DateTimeOffset> getNow;
    public FailedUpdateHandler(ILauncherStateStore stateStore)
        : this(stateStore, path => Process.Start(new ProcessStartInfo(path) { WorkingDirectory = Path.GetDirectoryName(path)!, UseShellExecute = true }), () => DateTimeOffset.UtcNow) { }
    public FailedUpdateHandler(ILauncherStateStore stateStore, Action<string> startApplication, Func<DateTimeOffset> getNow)
    { this.stateStore = stateStore; this.startApplication = startApplication; this.getNow = getNow; }
    public async Task HandleAsync(string failedVersion, string distributionRoot, CancellationToken cancellationToken = default)
    {
        var state = await stateStore.LoadAsync(cancellationToken);
        await stateStore.SaveAsync(state with { FailedVersion = failedVersion, FailedAt = getNow() }, cancellationToken);
        var application = Path.Combine(Path.GetFullPath(distributionRoot), "app", "EftSsNavi.App.exe");
        if (File.Exists(application)) startApplication(application);
    }
}
