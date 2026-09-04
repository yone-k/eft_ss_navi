using EftSsNavi.Launcher.State;
using EftSsNavi.Launcher.Transactions;

namespace EftSsNavi.Launcher.Tests;

public sealed class CleanupManagerTests
{
    [Fact]
    public async Task ShouldRememberValidatedCleanupPathWhenDeletionFailsAndRetryLater()
    {
        var updates = Path.GetFullPath(@"C:\local\updates"); var target = Path.Combine(updates, "tx");
        var store = new MemoryState(); var attempts = 0;
        var manager = new CleanupManager(updates, store, _ => { attempts++; if (attempts == 1) throw new IOException("locked"); });
        Assert.False(await manager.TryCleanupAsync(target));
        Assert.Contains(target, store.Value.PendingCleanupPaths);
        await manager.RetryPendingAsync();
        Assert.DoesNotContain(target, store.Value.PendingCleanupPaths);
    }

    [Fact]
    public async Task ShouldRejectCleanupPathOutsideUpdatesRootWithoutDeleting()
    {
        var deleted = false; var store = new MemoryState();
        var manager = new CleanupManager(@"C:\local\updates", store, _ => deleted = true);
        Assert.False(await manager.TryCleanupAsync(@"C:\other\tx")); Assert.False(deleted); Assert.Empty(store.Value.PendingCleanupPaths);
    }

    [Fact]
    public async Task ShouldDeferValidatedPathWithoutAttemptingDeletion()
    {
        var updates = Path.GetFullPath(@"C:\local\updates"); var target = Path.Combine(updates, "tx"); var deleted = false; var store = new MemoryState();
        var manager = new CleanupManager(updates, store, _ => deleted = true);
        Assert.True(await manager.DeferAsync(target)); Assert.False(deleted); Assert.Contains(target, store.Value.PendingCleanupPaths);
    }
    private sealed class MemoryState : ILauncherStateStore
    {
        public LauncherState Value { get; private set; } = new();
        public Task<LauncherState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task SaveAsync(LauncherState state, CancellationToken cancellationToken = default) { Value = state; return Task.CompletedTask; }
    }
}
