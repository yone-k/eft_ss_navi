using EftSsNavi.Launcher.Transactions;

namespace EftSsNavi.Launcher.Tests;

public sealed class UpdateRecoveryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "EftSsNaviRecovery", Guid.NewGuid().ToString("N"));

    [Fact]
    public void ShouldRestoreBackupsAfterInterruptedReplacement()
    {
        Directory.CreateDirectory(root);
        var backup = Path.Combine(root, "app.old"); Directory.CreateDirectory(backup); File.WriteAllText(Path.Combine(backup, "old.txt"), "old");
        var current = Path.Combine(root, "app"); Directory.CreateDirectory(current); File.WriteAllText(Path.Combine(current, "new.txt"), "new");
        File.WriteAllText(Path.Combine(root, "EftSsNavi.exe"), "launcher");
        File.WriteAllText(Path.Combine(root, "README.md"), "readme");
        var journal = new UpdateJournal("1.2.3", root, Path.Combine(root, "staging"), UpdatePhase.NewVersionPlaced);
        var result = new UpdateRecovery(root).Recover(journal);
        Assert.True(result.Succeeded);
        Assert.True(File.Exists(Path.Combine(current, "old.txt")));
        Assert.False(Directory.Exists(backup));
    }

    [Fact]
    public void ShouldRejectJournalOutsideDistributionRoot()
    {
        Directory.CreateDirectory(root);
        var updates = Path.Combine(root, "updates"); Directory.CreateDirectory(updates);
        var journal = new UpdateJournal("1.2.3", Path.Combine(root, "dist"), Path.Combine(root, "outside"), UpdatePhase.Prepared);
        Assert.False(new UpdateRecovery(updates).Recover(journal).Succeeded);
    }

    [Theory]
    [InlineData(UpdatePhase.Prepared)]
    [InlineData(UpdatePhase.CurrentVersionBackedUp)]
    [InlineData(UpdatePhase.NewVersionPlaced)]
    [InlineData(UpdatePhase.AwaitingStartup)]
    [InlineData(UpdatePhase.RollingBack)]
    public void ShouldRestoreACompleteOldDistributionFromEveryIncompletePhase(UpdatePhase phase)
    {
        var updates = Path.Combine(root, "updates");
        var staging = Path.Combine(updates, "tx", "staging");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(Path.Combine(root, "app.old"));
        File.WriteAllText(Path.Combine(root, "app.old", "old.txt"), "old");
        File.WriteAllText(Path.Combine(root, "EftSsNavi.exe.old"), "old-launcher");
        File.WriteAllText(Path.Combine(root, "README.md.old"), "old-readme");

        var result = new UpdateRecovery(updates, root).Recover(new("1.2.3", root, staging, phase));

        Assert.True(result.Succeeded);
        Assert.True(File.Exists(Path.Combine(root, "app", "old.txt")));
        Assert.True(File.Exists(Path.Combine(root, "EftSsNavi.exe")));
        Assert.True(File.Exists(Path.Combine(root, "README.md")));
    }

    [Fact]
    public void ShouldFailRecoveryWhenNeitherCurrentFilesNorCompleteBackupsExist()
    {
        var updates = Path.Combine(root, "updates");
        var staging = Path.Combine(updates, "tx", "staging");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(Path.Combine(root, "app.old"));
        File.WriteAllText(Path.Combine(root, "app.old", "old.txt"), "old");

        var result = new UpdateRecovery(updates, root).Recover(new("1.2.3", root, staging, UpdatePhase.CurrentVersionBackedUp));

        Assert.False(result.Succeeded);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
