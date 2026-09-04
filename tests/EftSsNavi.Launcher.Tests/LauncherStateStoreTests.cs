using EftSsNavi.Launcher.State;

namespace EftSsNavi.Launcher.Tests;

public sealed class LauncherStateStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EftSsNaviState", Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task ShouldRoundTripLauncherStateAtomically()
    {
        var path = Path.Combine(root, "launcher.json");
        var expected = new LauncherState { IgnoredVersion = "1.2.3", FailedVersion = "1.2.2", FailedAt = DateTimeOffset.UnixEpoch, PendingCleanupPaths = ["x"] };
        var store = new LauncherStateStore(path);
        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();
        Assert.Equal(expected.IgnoredVersion, actual.IgnoredVersion);
        Assert.Equal(expected.FailedVersion, actual.FailedVersion);
        Assert.Equal(expected.FailedAt, actual.FailedAt);
        Assert.Equal(expected.PendingCleanupPaths, actual.PendingCleanupPaths);
        Assert.False(File.Exists(path + ".tmp"));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
