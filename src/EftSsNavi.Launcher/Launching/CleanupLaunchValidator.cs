using System.Diagnostics;

namespace EftSsNavi.Launcher.Launching;

public sealed class CleanupLaunchValidator
{
    private readonly Func<int, ProcessIdentity> getProcessIdentity;
    private readonly Func<int> getCurrentSessionId;

    public CleanupLaunchValidator()
        : this(
            pid =>
            {
                using var process = Process.GetProcessById(pid);
                return new ProcessIdentity(process.SessionId, process.MainModule?.FileName ?? string.Empty);
            },
            () => Process.GetCurrentProcess().SessionId)
    {
    }

    public CleanupLaunchValidator(Func<int, ProcessIdentity> getProcessIdentity, Func<int> getCurrentSessionId)
    {
        this.getProcessIdentity = getProcessIdentity;
        this.getCurrentSessionId = getCurrentSessionId;
    }

    public bool IsValid(LaunchArguments arguments, string updatesRoot)
    {
        try
        {
            if (arguments.Mode != LaunchMode.Cleanup || arguments.CallerPid is null ||
                arguments.CallerSessionId is null || string.IsNullOrWhiteSpace(arguments.CallerPath) ||
                string.IsNullOrWhiteSpace(arguments.TransactionDirectory))
            {
                return false;
            }

            var transaction = Path.GetFullPath(arguments.TransactionDirectory);
            var expectedCaller = Path.Combine(transaction, "EftSsNavi.Update.exe");
            var actualCaller = getProcessIdentity(arguments.CallerPid.Value);
            return IsChildPath(transaction, updatesRoot) &&
                actualCaller.SessionId == arguments.CallerSessionId.Value &&
                actualCaller.SessionId == getCurrentSessionId() &&
                PathsEqual(arguments.CallerPath, expectedCaller) &&
                PathsEqual(actualCaller.ExecutablePath, expectedCaller);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static bool IsChildPath(string path, string root) =>
        path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
