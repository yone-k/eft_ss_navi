using System.Text.Json;
using System.Text.Json.Serialization;

namespace EftSsNavi.Launcher.Transactions;

[JsonConverter(typeof(JsonStringEnumConverter<UpdatePhase>))]
public enum UpdatePhase { Prepared, CurrentVersionBackedUp, NewVersionPlaced, AwaitingStartup, RollingBack, Completed }

public sealed record UpdateJournal(string TargetVersion, string DistributionRoot, string StagingDirectory, UpdatePhase Phase)
{
    public static async Task SaveAsync(string path, UpdateJournal journal, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(journal), cancellationToken);
        File.Move(temporary, path, true);
    }
    public static async Task<UpdateJournal?> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        try { return JsonSerializer.Deserialize<UpdateJournal>(await File.ReadAllTextAsync(path, cancellationToken)); }
        catch (FileNotFoundException) { return null; }
        catch (JsonException) { return null; }
    }
}

public sealed record RecoveryResult(bool Succeeded, string? Error = null);

public sealed class UpdateRecovery(string updatesRoot, string? expectedDistributionRoot = null)
{
    public RecoveryResult Recover(UpdateJournal journal)
    {
        try
        {
            var distribution = Path.GetFullPath(journal.DistributionRoot);
            var staging = Path.GetFullPath(journal.StagingDirectory);
            if (!IsWithin(staging, updatesRoot)) return new(false, "The staging directory is outside the update root.");
            if (expectedDistributionRoot is not null && !string.Equals(distribution, Path.GetFullPath(expectedDistributionRoot), StringComparison.OrdinalIgnoreCase))
                return new(false, "The journal targets a different distribution root.");
            if (journal.Phase == UpdatePhase.Completed) return new(true);
            RestoreDirectory(distribution, "app");
            RestoreFile(distribution, "EftSsNavi.exe");
            RestoreFile(distribution, "README.md");
            DeleteIfExists(Path.Combine(distribution, "app.new"));
            DeleteIfExists(Path.Combine(distribution, "EftSsNavi.exe.new"));
            DeleteIfExists(Path.Combine(distribution, "README.md.new"));
            if (!Directory.Exists(Path.Combine(distribution, "app")) ||
                !File.Exists(Path.Combine(distribution, "EftSsNavi.exe")) ||
                !File.Exists(Path.Combine(distribution, "README.md")))
            {
                return new(false, "A complete distribution could not be restored.");
            }
            return new(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return new(false, ex.Message); }
    }
    private static void RestoreDirectory(string root, string name)
    {
        var backup = Path.Combine(root, name + ".old"); if (!Directory.Exists(backup)) return;
        var current = Path.Combine(root, name); if (Directory.Exists(current)) Directory.Delete(current, true);
        Directory.Move(backup, current);
    }
    private static void RestoreFile(string root, string name)
    {
        var backup = Path.Combine(root, name + ".old"); if (!File.Exists(backup)) return;
        var current = Path.Combine(root, name); File.Move(backup, current, true);
    }
    private static void DeleteIfExists(string path) { if (Directory.Exists(path)) Directory.Delete(path, true); else if (File.Exists(path)) File.Delete(path); }
    private static bool IsWithin(string path, string root) => path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
