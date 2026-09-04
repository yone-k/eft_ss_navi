using System.Diagnostics;

namespace EftSsNavi.Launcher.Launching;

public sealed record ProcessIdentity(int SessionId, string ExecutablePath);

public sealed class ManualLaunchValidator
{
    private readonly Func<int, ProcessIdentity> inspectProcess;
    private readonly Func<int> getCurrentSessionId;
    public ManualLaunchValidator() : this(pid =>
    {
        using var process = Process.GetProcessById(pid);
        return new(process.SessionId, process.MainModule?.FileName ?? string.Empty);
    }, () => Process.GetCurrentProcess().SessionId)
    { }
    public ManualLaunchValidator(Func<int, ProcessIdentity> inspectProcess, Func<int> getCurrentSessionId)
    {
        this.inspectProcess = inspectProcess;
        this.getCurrentSessionId = getCurrentSessionId;
    }

    public bool IsValid(LaunchArguments arguments, string distributionRoot)
    {
        if (arguments.Mode != LaunchMode.Manual || arguments.CallerPid is null || arguments.CallerSessionId is null) return false;
        try
        {
            var expected = Path.GetFullPath(Path.Combine(distributionRoot, "app", "EftSsNavi.App.exe"));
            var claimed = Path.GetFullPath(arguments.CallerPath!);
            var actual = inspectProcess(arguments.CallerPid.Value);
            return actual.SessionId == arguments.CallerSessionId
                && actual.SessionId == getCurrentSessionId()
                && string.Equals(claimed, expected, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetFullPath(actual.ExecutablePath), expected, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(arguments.EventName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { return false; }
    }
}
