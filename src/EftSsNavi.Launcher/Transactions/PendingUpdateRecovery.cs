namespace EftSsNavi.Launcher.Transactions;

public sealed class PendingUpdateRecovery(string updatesRoot, string distributionRoot, CleanupManager cleanupManager)
{
    public async Task<bool> HasPendingAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(updatesRoot)) return false;
        foreach (var journalPath in Directory.EnumerateFiles(updatesRoot, "journal.json", SearchOption.AllDirectories).ToArray())
        {
            var journal = await UpdateJournal.LoadAsync(journalPath, cancellationToken);
            if (journal is not null && journal.Phase != UpdatePhase.Completed) return true;
        }
        return false;
    }

    public async Task<bool> RecoverAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(updatesRoot)) return true;
        foreach (var journalPath in Directory.EnumerateFiles(updatesRoot, "journal.json", SearchOption.AllDirectories).ToArray())
        {
            var journal = await UpdateJournal.LoadAsync(journalPath, cancellationToken);
            if (journal is null || journal.Phase == UpdatePhase.Completed) continue;
            var result = new UpdateRecovery(updatesRoot, distributionRoot).Recover(journal);
            if (!result.Succeeded) return false;
            await UpdateJournal.SaveAsync(journalPath, journal with { Phase = UpdatePhase.Completed }, cancellationToken);
            await cleanupManager.TryCleanupAsync(Path.GetDirectoryName(journalPath)!, cancellationToken);
        }
        return true;
    }
}
