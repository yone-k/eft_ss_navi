using System.Diagnostics;

namespace EftSsNavi.Launcher.Launching;

public sealed class ApplyLaunchValidator
{
    private readonly string currentExecutablePath;
    private readonly Func<int, ProcessIdentity> getProcessIdentity;
    private readonly Func<int> getCurrentSessionId;

    public ApplyLaunchValidator()
        : this(
            Environment.ProcessPath ?? string.Empty,
            pid =>
            {
                using var process = Process.GetProcessById(pid);
                return new ProcessIdentity(process.SessionId, process.MainModule?.FileName ?? string.Empty);
            },
            () => Process.GetCurrentProcess().SessionId)
    {
    }

    public ApplyLaunchValidator(
        string currentExecutablePath,
        Func<int, ProcessIdentity> getProcessIdentity,
        Func<int> getCurrentSessionId)
    {
        this.currentExecutablePath = currentExecutablePath;
        this.getProcessIdentity = getProcessIdentity;
        this.getCurrentSessionId = getCurrentSessionId;
    }

    public bool IsValid(LaunchArguments arguments, string updatesRoot)
    {
        try
        {
            if (arguments.Mode != LaunchMode.ApplyUpdate ||
                arguments.CallerPid is null || arguments.CallerSessionId is null ||
                string.IsNullOrWhiteSpace(arguments.CallerPath) ||
                string.IsNullOrWhiteSpace(arguments.TransactionDirectory) ||
                string.IsNullOrWhiteSpace(arguments.DistributionRoot))
            {
                return false;
            }

            var transaction = Path.GetFullPath(arguments.TransactionDirectory);
            var distribution = Path.GetFullPath(arguments.DistributionRoot);
            var expectedLauncher = Path.Combine(transaction, "EftSsNavi.Update.exe");
            if (!IsChildPath(transaction, updatesRoot) || !PathsEqual(currentExecutablePath, expectedLauncher))
            {
                return false;
            }

            var actualCaller = getProcessIdentity(arguments.CallerPid.Value);
            if (arguments.CallerSessionId.Value != getCurrentSessionId() ||
                actualCaller.SessionId != arguments.CallerSessionId.Value ||
                !PathsEqual(actualCaller.ExecutablePath, arguments.CallerPath))
            {
                return false;
            }

            var rootLauncher = Path.Combine(distribution, "EftSsNavi.exe");
            var application = Path.Combine(distribution, "app", "EftSsNavi.App.exe");
            return PathsEqual(arguments.CallerPath, rootLauncher) || PathsEqual(arguments.CallerPath, application);
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
