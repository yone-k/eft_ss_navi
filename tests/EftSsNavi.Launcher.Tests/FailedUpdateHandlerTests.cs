using EftSsNavi.Launcher.State;
using EftSsNavi.Launcher.Transactions;

namespace EftSsNavi.Launcher.Tests;

public sealed class FailedUpdateHandlerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EftSsNaviFailed", Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task ShouldRecordCooldownAndStartRolledBackApplication()
    {
        var app = Path.Combine(root, "app"); Directory.CreateDirectory(app); File.WriteAllText(Path.Combine(app, "EftSsNavi.App.exe"), "app");
        var state = new MemoryState(); string? started = null; var now = DateTimeOffset.UnixEpoch;
        await new FailedUpdateHandler(state, path => started = path, () => now).HandleAsync("1.2.3", root);
        Assert.Equal("1.2.3", state.Value.FailedVersion); Assert.Equal(now, state.Value.FailedAt);
        Assert.Equal(Path.GetFullPath(Path.Combine(root, "app", "EftSsNavi.App.exe")), Path.GetFullPath(started!));
    }
    private sealed class MemoryState : ILauncherStateStore
    {
        public LauncherState Value { get; private set; } = new();
        public Task<LauncherState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task SaveAsync(LauncherState state, CancellationToken cancellationToken = default) { Value = state; return Task.CompletedTask; }
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
