namespace EftSsNavi.Launcher.Transactions;

public sealed record TransactionResult(bool Succeeded, string? Error = null);

public sealed class UpdateTransaction
{
    public async Task<TransactionResult> ApplyAsync(
        string targetVersion,
        string distributionRoot,
        string stagingDirectory,
        string journalPath,
        Func<string, Task<bool>> verifyStartupAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verifyStartupAsync);
        var distribution = Path.GetFullPath(distributionRoot);
        var staging = Path.GetFullPath(stagingDirectory);
        try
        {
            EnsureStagingLayout(staging);
            await PrepareAsync(targetVersion, distribution, staging, journalPath, cancellationToken);
            await BackupAsync(targetVersion, distribution, staging, journalPath, cancellationToken);
            await PlaceAsync(targetVersion, distribution, staging, journalPath, cancellationToken);
            await UpdateJournal.SaveAsync(journalPath, new(targetVersion, distribution, staging, UpdatePhase.AwaitingStartup), cancellationToken);
            if (!await verifyStartupAsync(distribution))
            {
                await UpdateJournal.SaveAsync(journalPath, new(targetVersion, distribution, staging, UpdatePhase.RollingBack), cancellationToken);
                var recovery = new UpdateRecovery(FindUpdatesRoot(staging)).Recover(new(targetVersion, distribution, staging, UpdatePhase.RollingBack));
                return new(false, recovery.Error ?? "The updated application did not report a successful startup.");
            }
            await UpdateJournal.SaveAsync(journalPath, new(targetVersion, distribution, staging, UpdatePhase.Completed), cancellationToken);
            try
            {
                DeleteBackups(distribution);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Startup verification is the commit boundary. Cleanup failure must not
                // turn a working new version into a partial rollback.
            }
            return new(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            _ = new UpdateRecovery(FindUpdatesRoot(staging)).Recover(new(targetVersion, distribution, staging, UpdatePhase.RollingBack));
            return new(false, ex.Message);
        }
    }

    private static async Task PrepareAsync(string version, string distribution, string staging, string journal, CancellationToken token)
    {
        await UpdateJournal.SaveAsync(journal, new(version, distribution, staging, UpdatePhase.Prepared), token);
        CopyDirectory(Path.Combine(staging, "app"), Path.Combine(distribution, "app.new"));
        File.Copy(Path.Combine(staging, "EftSsNavi.exe"), Path.Combine(distribution, "EftSsNavi.exe.new"), true);
        File.Copy(Path.Combine(staging, "README.md"), Path.Combine(distribution, "README.md.new"), true);
    }
    private static async Task BackupAsync(string version, string distribution, string staging, string journal, CancellationToken token)
    {
        MoveDirectory(distribution, "app", "app.old");
        MoveFile(distribution, "EftSsNavi.exe", "EftSsNavi.exe.old");
        MoveFile(distribution, "README.md", "README.md.old");
        await UpdateJournal.SaveAsync(journal, new(version, distribution, staging, UpdatePhase.CurrentVersionBackedUp), token);
    }
    private static async Task PlaceAsync(string version, string distribution, string staging, string journal, CancellationToken token)
    {
        Directory.Move(Path.Combine(distribution, "app.new"), Path.Combine(distribution, "app"));
        File.Move(Path.Combine(distribution, "EftSsNavi.exe.new"), Path.Combine(distribution, "EftSsNavi.exe"));
        File.Move(Path.Combine(distribution, "README.md.new"), Path.Combine(distribution, "README.md"));
        await UpdateJournal.SaveAsync(journal, new(version, distribution, staging, UpdatePhase.NewVersionPlaced), token);
    }
    private static void EnsureStagingLayout(string staging)
    {
        if (!Directory.Exists(Path.Combine(staging, "app")) || !File.Exists(Path.Combine(staging, "EftSsNavi.exe")) || !File.Exists(Path.Combine(staging, "README.md")))
            throw new IOException("The staging layout is incomplete.");
    }
    private static void CopyDirectory(string source, string destination)
    {
        if (Directory.Exists(destination)) Directory.Delete(destination, true);
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), true);
    }
    private static void MoveDirectory(string root, string sourceName, string targetName)
    {
        var target = Path.Combine(root, targetName); if (Directory.Exists(target)) Directory.Delete(target, true);
        Directory.Move(Path.Combine(root, sourceName), target);
    }
    private static void MoveFile(string root, string sourceName, string targetName)
    {
        var target = Path.Combine(root, targetName); if (File.Exists(target)) File.Delete(target);
        File.Move(Path.Combine(root, sourceName), target);
    }
    private static void DeleteBackups(string root)
    {
        var app = Path.Combine(root, "app.old"); if (Directory.Exists(app)) Directory.Delete(app, true);
        foreach (var name in new[] { "EftSsNavi.exe.old", "README.md.old" }) { var path = Path.Combine(root, name); if (File.Exists(path)) File.Delete(path); }
    }
    private static string FindUpdatesRoot(string staging)
    {
        var transaction = Directory.GetParent(Path.TrimEndingDirectorySeparator(staging));
        return transaction?.Parent?.FullName ?? throw new IOException("Invalid staging directory.");
    }
}
