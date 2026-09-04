using EftSsNavi.Launcher.Transactions;
using EftSsNavi.Launcher.State;

namespace EftSsNavi.Launcher.Tests;

public sealed class PendingUpdateRecoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EftSsNaviPending", Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task ShouldDiscoverJournalAndRestoreInterruptedUpdateOnNormalStartup()
    {
        var distribution = Path.Combine(root, "dist"); var updates = Path.Combine(root, "updates"); var tx = Path.Combine(updates, "tx");
        Directory.CreateDirectory(Path.Combine(distribution, "app")); Directory.CreateDirectory(Path.Combine(distribution, "app.old")); Directory.CreateDirectory(Path.Combine(tx, "staging"));
        File.WriteAllText(Path.Combine(distribution, "app", "new.txt"), "new"); File.WriteAllText(Path.Combine(distribution, "app.old", "old.txt"), "old");
        File.WriteAllText(Path.Combine(distribution, "EftSsNavi.exe"), "launcher"); File.WriteAllText(Path.Combine(distribution, "README.md"), "readme");
        await UpdateJournal.SaveAsync(Path.Combine(tx, "journal.json"), new("1.2.3", distribution, Path.Combine(tx, "staging"), UpdatePhase.NewVersionPlaced));
        var cleanup = new CleanupManager(updates, new MemoryStateStore());
        var result = await new PendingUpdateRecovery(updates, distribution, cleanup).RecoverAsync();
        Assert.True(result); Assert.True(File.Exists(Path.Combine(distribution, "app", "old.txt")));
        Assert.False(Directory.Exists(tx));
    }
    private sealed class MemoryStateStore : ILauncherStateStore
    {
        private LauncherState state = new();
        public Task<LauncherState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(state);
        public Task SaveAsync(LauncherState value, CancellationToken cancellationToken = default) { state = value; return Task.CompletedTask; }
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
