using EftSsNavi.Launcher.State;
using EftSsNavi.Launcher.Updates;

namespace EftSsNavi.Launcher.Tests;

public sealed class LauncherFailureHandlerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EftSsNaviFailure", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ShouldStartCurrentApplicationAfterAutomaticUpdateInfrastructureFailure()
    {
        var app = Path.Combine(root, "app", "EftSsNavi.App.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(app)!);
        File.WriteAllText(app, "app");
        string? started = null;
        var ui = new FakeUi();

        new LauncherFailureHandler(path => started = path, ui).Handle(UpdateCheckMode.Automatic, app);

        Assert.Equal(app, started);
        Assert.Empty(ui.Notifications);
    }

    [Fact]
    public void ShouldKeepApplicationAndShowFailureAfterManualInfrastructureFailure()
    {
        var ui = new FakeUi();
        var started = false;

        new LauncherFailureHandler(_ => started = true, ui).Handle(UpdateCheckMode.Manual, "unused.exe");

        Assert.False(started);
        Assert.Equal([UpdateNotice.CheckFailed], ui.Notifications);
    }

    private sealed class FakeUi : IUpdateUserInterface
    {
        public List<UpdateNotice> Notifications { get; } = [];
        public UpdateChoice Choose(UpdateCheckMode mode, UpdateCandidate candidate) => UpdateChoice.Close;
        public void Notify(UpdateNotice notice) => Notifications.Add(notice);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
