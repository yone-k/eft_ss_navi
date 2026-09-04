using EftSsNavi.Launcher.Transactions;

namespace EftSsNavi.Launcher.Tests;

public sealed class UpdateTransactionTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EftSsNaviTransaction", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ShouldCommitStagedFilesWhenStartupSucceeds()
    {
        var (distribution, staging, journal) = ArrangeVersions();
        var result = await new UpdateTransaction().ApplyAsync("1.2.3", distribution, staging, journal, _ => Task.FromResult(true));
        Assert.True(result.Succeeded);
        Assert.Equal("new-app", File.ReadAllText(Path.Combine(distribution, "app", "version.txt")));
        Assert.Equal("new-launcher", File.ReadAllText(Path.Combine(distribution, "EftSsNavi.exe")));
        Assert.False(Directory.Exists(Path.Combine(distribution, "app.old")));
        Assert.False(File.Exists(Path.Combine(distribution, "EftSsNavi.exe.old")));
    }

    [Fact]
    public async Task ShouldRollbackAllFilesWhenStartupFails()
    {
        var (distribution, staging, journal) = ArrangeVersions();
        var result = await new UpdateTransaction().ApplyAsync("1.2.3", distribution, staging, journal, _ => Task.FromResult(false));
        Assert.False(result.Succeeded);
        Assert.Equal("old-app", File.ReadAllText(Path.Combine(distribution, "app", "version.txt")));
        Assert.Equal("old-launcher", File.ReadAllText(Path.Combine(distribution, "EftSsNavi.exe")));
        Assert.Equal("old-readme", File.ReadAllText(Path.Combine(distribution, "README.md")));
    }

    [Fact]
    public async Task ShouldKeepVerifiedNewVersionWhenBackupCleanupFails()
    {
        var (distribution, staging, journal) = ArrangeVersions();
        FileStream? lockedBackup = null;

        var result = await new UpdateTransaction().ApplyAsync("1.2.3", distribution, staging, journal, _ =>
        {
            lockedBackup = new FileStream(
                Path.Combine(distribution, "EftSsNavi.exe.old"),
                FileMode.Open,
                FileAccess.Read,
                FileShare.None);
            return Task.FromResult(true);
        });
        lockedBackup?.Dispose();

        Assert.True(result.Succeeded);
        Assert.Equal("new-app", File.ReadAllText(Path.Combine(distribution, "app", "version.txt")));
        Assert.Equal("new-launcher", File.ReadAllText(Path.Combine(distribution, "EftSsNavi.exe")));
        Assert.Equal(UpdatePhase.Completed, (await UpdateJournal.LoadAsync(journal))?.Phase);
    }

    private (string Distribution, string Staging, string Journal) ArrangeVersions()
    {
        var distribution = Path.Combine(root, "dist"); var staging = Path.Combine(root, "updates", "tx", "staging");
        Directory.CreateDirectory(Path.Combine(distribution, "app")); Directory.CreateDirectory(Path.Combine(staging, "app"));
        File.WriteAllText(Path.Combine(distribution, "app", "version.txt"), "old-app");
        File.WriteAllText(Path.Combine(distribution, "EftSsNavi.exe"), "old-launcher"); File.WriteAllText(Path.Combine(distribution, "README.md"), "old-readme");
        File.WriteAllText(Path.Combine(staging, "app", "version.txt"), "new-app");
        File.WriteAllText(Path.Combine(staging, "EftSsNavi.exe"), "new-launcher"); File.WriteAllText(Path.Combine(staging, "README.md"), "new-readme");
        return (distribution, staging, Path.Combine(root, "updates", "tx", "journal.json"));
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
