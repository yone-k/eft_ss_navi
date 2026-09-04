using EftSsNavi.Launcher.State;

namespace EftSsNavi.Launcher.Transactions;

public sealed class CleanupManager
{
    private readonly string updatesRoot;
    private readonly ILauncherStateStore stateStore;
    private readonly Action<string> deleteDirectory;
    public CleanupManager(string updatesRoot, ILauncherStateStore stateStore)
        : this(updatesRoot, stateStore, path => { if (Directory.Exists(path)) Directory.Delete(path, true); }) { }
    public CleanupManager(string updatesRoot, ILauncherStateStore stateStore, Action<string> deleteDirectory)
    {
        this.updatesRoot = Path.GetFullPath(updatesRoot);
        this.stateStore = stateStore;
        this.deleteDirectory = deleteDirectory;
    }

    public async Task<bool> TryCleanupAsync(string path, CancellationToken cancellationToken = default)
    {
        var target = Path.GetFullPath(path);
        if (!IsWithin(target)) return false;
        try
        {
            deleteDirectory(target);
            await RemovePendingAsync(target, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            await AddPendingAsync(target, cancellationToken);
            return false;
        }
    }

    public async Task<bool> DeferAsync(string path, CancellationToken cancellationToken = default)
    {
        var target = Path.GetFullPath(path);
        if (!IsWithin(target)) return false;
        await AddPendingAsync(target, cancellationToken);
        return true;
    }

    public async Task RetryPendingAsync(CancellationToken cancellationToken = default)
    {
        var state = await stateStore.LoadAsync(cancellationToken);
        foreach (var path in state.PendingCleanupPaths.ToArray())
            if (IsWithin(Path.GetFullPath(path))) await TryCleanupAsync(path, cancellationToken);
    }

    private async Task RemovePendingAsync(string target, CancellationToken token)
    {
        var state = await stateStore.LoadAsync(token);
        var remaining = state.PendingCleanupPaths.Where(x => !string.Equals(Path.GetFullPath(x), target, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (remaining.Length != state.PendingCleanupPaths.Count) await stateStore.SaveAsync(state with { PendingCleanupPaths = remaining }, token);
    }

    private async Task AddPendingAsync(string target, CancellationToken token)
    {
        var state = await stateStore.LoadAsync(token);
        if (!state.PendingCleanupPaths.Contains(target, StringComparer.OrdinalIgnoreCase))
            await stateStore.SaveAsync(state with { PendingCleanupPaths = [.. state.PendingCleanupPaths, target] }, token);
    }
    private bool IsWithin(string path) => path.StartsWith(Path.TrimEndingDirectorySeparator(updatesRoot) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
