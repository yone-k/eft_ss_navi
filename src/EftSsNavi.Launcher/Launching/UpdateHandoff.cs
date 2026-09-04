using System.Diagnostics;

namespace EftSsNavi.Launcher.Launching;

public sealed class UpdateHandoff
{
    private readonly Action<string, IReadOnlyList<string>> startProcess;
    public UpdateHandoff() : this((executable, arguments) =>
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = Path.GetDirectoryName(executable)!, UseShellExecute = false };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        Process.Start(info);
    })
    { }
    public UpdateHandoff(Action<string, IReadOnlyList<string>> startProcess) => this.startProcess = startProcess;

    public void Start(
        string distributionRoot,
        string transactionDirectory,
        string targetVersion,
        int callerPid,
        int callerSessionId,
        string callerPath,
        string? shutdownEvent)
    {
        var source = Path.Combine(Path.GetFullPath(distributionRoot), "EftSsNavi.exe");
        var temporary = Path.Combine(Path.GetFullPath(transactionDirectory), "EftSsNavi.Update.exe");
        File.Copy(source, temporary, true);
        var handoffEventName = $"Local\\EftSsNavi.Handoff.{Guid.NewGuid():N}";
        using var handoffReady = new EventWaitHandle(false, EventResetMode.ManualReset, handoffEventName);
        var arguments = new List<string>
        {
            "--apply-update", "--caller-pid", callerPid.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--caller-session-id", callerSessionId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "--caller-path", Path.GetFullPath(callerPath),
            "--handoff-ready-event", handoffEventName,
            "--transaction-dir", Path.GetFullPath(transactionDirectory), "--distribution-root", Path.GetFullPath(distributionRoot),
            "--target-version", targetVersion,
        };
        if (!string.IsNullOrWhiteSpace(shutdownEvent)) { arguments.Add("--shutdown-event"); arguments.Add(shutdownEvent); }
        startProcess(temporary, arguments);
        if (!handoffReady.WaitOne(TimeSpan.FromSeconds(10)))
        {
            throw new TimeoutException("The temporary launcher did not acknowledge the update handoff.");
        }
    }
}
