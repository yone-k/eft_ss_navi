using System.Text.Json;

namespace EftSsNavi.Launcher.State;

public enum UpdateCheckMode { Automatic, Manual }

public sealed record LauncherState
{
    public DateTimeOffset? LastCheckedAt { get; init; }
    public string? IgnoredVersion { get; init; }
    public string? FailedVersion { get; init; }
    public DateTimeOffset? FailedAt { get; init; }
    public IReadOnlyList<string> PendingCleanupPaths { get; init; } = [];
}

public static class UpdateEligibility
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(24);
    public static bool ShouldCheck(UpdateCheckMode mode, LauncherState state, DateTimeOffset now) =>
        mode == UpdateCheckMode.Manual || state.LastCheckedAt is null || now - state.LastCheckedAt >= Cooldown;

    public static bool CanOffer(string version, UpdateCheckMode mode, LauncherState state, DateTimeOffset now)
    {
        if (state.FailedVersion == version && state.FailedAt is { } failed && now - failed < Cooldown) return false;
        return mode == UpdateCheckMode.Manual || state.IgnoredVersion != version;
    }
}

public interface ILauncherStateStore
{
    Task<LauncherState> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(LauncherState state, CancellationToken cancellationToken = default);
}

public sealed class LauncherStateStore(string path) : ILauncherStateStore
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public async Task<LauncherState> LoadAsync(CancellationToken cancellationToken = default)
    {
        try { await using var stream = File.OpenRead(path); return await JsonSerializer.DeserializeAsync<LauncherState>(stream, Options, cancellationToken) ?? new(); }
        catch (FileNotFoundException) { return new(); }
        catch (JsonException) { return new(); }
    }
    public async Task SaveAsync(LauncherState state, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { await JsonSerializer.SerializeAsync(stream, state, Options, cancellationToken); await stream.FlushAsync(cancellationToken); }
        File.Move(temporary, path, true);
    }
}
