namespace EftSsNavi.Launcher.Launching;

public enum LaunchMode { Normal, Manual, ApplyUpdate, Cleanup, VerifyStartup }

public sealed record LaunchArguments(
    LaunchMode Mode,
    int? CallerPid = null,
    int? CallerSessionId = null,
    string? CallerPath = null,
    string? EventName = null,
    string? TransactionDirectory = null,
    string? DistributionRoot = null,
    string? TargetVersion = null,
    string? HandoffEventName = null)
{
    public static LaunchArguments Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0) return new(LaunchMode.Normal);
        var mode = args[0] switch
        {
            "--manual-update" => LaunchMode.Manual,
            "--apply-update" => LaunchMode.ApplyUpdate,
            "--cleanup" => LaunchMode.Cleanup,
            "--verify-startup" => LaunchMode.VerifyStartup,
            _ => throw new ArgumentException("Unknown launcher mode."),
        };
        var values = ParsePairs(args.Skip(1).ToArray());
        int? pid = ReadInt(values, "--caller-pid");
        int? session = ReadInt(values, "--caller-session-id");
        values.TryGetValue("--caller-path", out var callerPath);
        values.TryGetValue("--shutdown-event", out var eventName);
        values.TryGetValue("--handoff-ready-event", out var handoffEventName);
        values.TryGetValue("--transaction-dir", out var transaction);
        values.TryGetValue("--distribution-root", out var distribution);
        values.TryGetValue("--target-version", out var targetVersion);
        if (mode == LaunchMode.Manual && (pid is null || session is null || string.IsNullOrWhiteSpace(callerPath) || string.IsNullOrWhiteSpace(eventName)))
            throw new ArgumentException("Manual mode requires authenticated caller data.");
        if (mode is LaunchMode.ApplyUpdate or LaunchMode.Cleanup && (pid is null || string.IsNullOrWhiteSpace(transaction)))
            throw new ArgumentException("Transaction mode requires a caller and transaction directory.");
        if (mode == LaunchMode.Cleanup &&
            (session is null || string.IsNullOrWhiteSpace(callerPath) || string.IsNullOrWhiteSpace(handoffEventName)))
            throw new ArgumentException("Cleanup mode requires authenticated caller data.");
        if (mode == LaunchMode.ApplyUpdate &&
            (session is null || string.IsNullOrWhiteSpace(callerPath) || string.IsNullOrWhiteSpace(handoffEventName) ||
             string.IsNullOrWhiteSpace(distribution) || !Version.TryParse(targetVersion, out var version) || version.Build < 0))
            throw new ArgumentException("Apply mode requires authenticated caller data, a distribution root, and a target version.");
        return new(mode, pid, session, callerPath, eventName, transaction, distribution, targetVersion, handoffEventName);
    }
    private static Dictionary<string, string> ParsePairs(string[] args)
    {
        if (args.Length % 2 != 0) throw new ArgumentException("Options require values.");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2) if (!result.TryAdd(args[i], args[i + 1])) throw new ArgumentException("Duplicate option.");
        return result;
    }
    private static int? ReadInt(Dictionary<string, string> values, string key) => values.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;
}
