using EftSsNavi.Launcher.State;
using EftSsNavi.Launcher.Updates;

namespace EftSsNavi.Launcher.Tests;

public sealed class LauncherCoordinatorTests
{
    private static readonly UpdateCandidate Candidate = new("v1.2.3", "1.2.3", new Uri("https://example.test/file.zip"), new string('a', 64));

    [Fact]
    public async Task ShouldPersistIgnoredVersionAndStartApplicationForAutomaticChoice()
    {
        var state = new MemoryState(); var ui = new FakeUi(UpdateChoice.Ignore);
        var coordinator = new LauncherCoordinator(new Checker(UpdateCheckResult.Available(Candidate)), state, ui, () => DateTimeOffset.UnixEpoch);
        var result = await coordinator.RunAsync(UpdateCheckMode.Automatic, new Version(1, 2, 2));
        Assert.Equal(LauncherAction.StartApplication, result.Action);
        Assert.Equal("1.2.3", state.Value.IgnoredVersion);
    }

    [Fact]
    public async Task ShouldRequestApplyForManualUpdateChoiceWithoutStartingAnotherApplication()
    {
        var coordinator = new LauncherCoordinator(new Checker(UpdateCheckResult.Available(Candidate)), new MemoryState(), new FakeUi(UpdateChoice.Update), () => DateTimeOffset.UnixEpoch);
        var result = await coordinator.RunAsync(UpdateCheckMode.Manual, new Version(1, 2, 2));
        Assert.Equal(LauncherAction.ApplyUpdate, result.Action);
        Assert.Same(Candidate, result.Candidate);
    }

    [Fact]
    public async Task ShouldSilentlyStartApplicationWhenAutomaticCheckFails()
    {
        var ui = new FakeUi(UpdateChoice.Update);
        var coordinator = new LauncherCoordinator(new Checker(UpdateCheckResult.Failed), new MemoryState(), ui, () => DateTimeOffset.UnixEpoch);
        var result = await coordinator.RunAsync(UpdateCheckMode.Automatic, new Version(1, 2, 2));
        Assert.Equal(LauncherAction.StartApplication, result.Action);
        Assert.Empty(ui.Notifications);
    }

    private sealed class Checker(UpdateCheckResult result) : IUpdateChecker { public Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default) => Task.FromResult(result); }
    private sealed class MemoryState : ILauncherStateStore
    {
        public LauncherState Value { get; private set; } = new();
        public Task<LauncherState> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Value);
        public Task SaveAsync(LauncherState state, CancellationToken cancellationToken = default) { Value = state; return Task.CompletedTask; }
    }
    private sealed class FakeUi(UpdateChoice choice) : IUpdateUserInterface
    {
        public List<UpdateNotice> Notifications { get; } = [];
        public UpdateChoice Choose(UpdateCheckMode mode, UpdateCandidate candidate) => choice;
        public void Notify(UpdateNotice notice) => Notifications.Add(notice);
    }
}
