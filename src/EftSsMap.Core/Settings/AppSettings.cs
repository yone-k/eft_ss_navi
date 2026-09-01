using System.Collections.ObjectModel;
using EftSsMap.Core.Calibration;

namespace EftSsMap.Core.Settings;

/// <summary>
/// Values persisted between application sessions.
/// </summary>
public sealed class AppSettings
{
    public AppSettings(
        string? watchDirectory,
        IReadOnlyList<MapProfile> mapProfiles,
        string? lastSelectedProfileName)
    {
        ArgumentNullException.ThrowIfNull(mapProfiles);

        WatchDirectory = watchDirectory;
        MapProfiles = new ReadOnlyCollection<MapProfile>(mapProfiles.ToArray());
        LastSelectedProfileName = lastSelectedProfileName;
    }

    public string? WatchDirectory { get; }

    public IReadOnlyList<MapProfile> MapProfiles { get; }

    public string? LastSelectedProfileName { get; }
}
