using System.Collections.ObjectModel;
using EftSsNavi.Core.Calibration;

namespace EftSsNavi.Core.Settings;

/// <summary>
/// Values persisted between application sessions.
/// </summary>
public sealed class AppSettings
{
    private const string DefaultStunServer = "stun:stun.l.google.com:19302";

    public AppSettings(
        string? watchDirectory,
        IReadOnlyList<MapProfile> mapProfiles,
        string? lastSelectedProfileName,
        int bundledMapCatalogVersion = 0,
        string? partyDisplayName = null,
        string? signalingWorkerUrl = null,
        IReadOnlyList<string>? stunServers = null)
    {
        ArgumentNullException.ThrowIfNull(mapProfiles);

        WatchDirectory = watchDirectory;
        MapProfiles = new ReadOnlyCollection<MapProfile>(mapProfiles.ToArray());
        LastSelectedProfileName = lastSelectedProfileName;
        BundledMapCatalogVersion = bundledMapCatalogVersion;
        PartyDisplayName = partyDisplayName;
        SignalingWorkerUrl = signalingWorkerUrl;
        StunServers = new ReadOnlyCollection<string>(
            (stunServers ?? [DefaultStunServer]).ToArray());
    }

    public string? WatchDirectory { get; }

    public IReadOnlyList<MapProfile> MapProfiles { get; }

    public string? LastSelectedProfileName { get; }

    public int BundledMapCatalogVersion { get; }

    public string? PartyDisplayName { get; }

    public string? SignalingWorkerUrl { get; }

    public IReadOnlyList<string> StunServers { get; }
}
