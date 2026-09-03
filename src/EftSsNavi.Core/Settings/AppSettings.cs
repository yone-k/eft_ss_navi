using System.Collections.ObjectModel;
using EftSsNavi.Core.Calibration;

namespace EftSsNavi.Core.Settings;

/// <summary>
/// Values persisted between application sessions.
/// </summary>
public sealed class AppSettings
{
    public AppSettings(
        string? watchDirectory,
        IReadOnlyList<MapProfile> mapProfiles,
        string? lastSelectedProfileName,
        int bundledMapCatalogVersion = 0)
    {
        ArgumentNullException.ThrowIfNull(mapProfiles);

        WatchDirectory = watchDirectory;
        MapProfiles = new ReadOnlyCollection<MapProfile>(mapProfiles.ToArray());
        LastSelectedProfileName = lastSelectedProfileName;
        BundledMapCatalogVersion = bundledMapCatalogVersion;
    }

    public string? WatchDirectory { get; }

    public IReadOnlyList<MapProfile> MapProfiles { get; }

    public string? LastSelectedProfileName { get; }

    public int BundledMapCatalogVersion { get; }
}
