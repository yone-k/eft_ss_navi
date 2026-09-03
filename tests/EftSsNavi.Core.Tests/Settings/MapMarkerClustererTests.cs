using EftSsNavi.Core.Calibration;
using EftSsNavi.Core.Settings;

namespace EftSsNavi.Core.Tests.Settings;

public sealed class MapMarkerClustererTests
{
    [Fact]
    public void ShouldMergeTransitivelyConnectedPmcSpawnsAtFifteenMeters()
    {
        // Given: Three spawn candidates connected in a chain and one isolated candidate.
        MapMarker[] markers =
        [
            new(MapMarkerKind.PmcSpawn, null, new WorldPoint(0, 0)),
            new(MapMarkerKind.PmcSpawn, null, new WorldPoint(10, 0)),
            new(MapMarkerKind.PmcSpawn, null, new WorldPoint(20, 0)),
            new(MapMarkerKind.PmcSpawn, null, new WorldPoint(100, 50)),
        ];

        // When: Spawn candidates are clustered for display.
        var result = MapMarkerClusterer.ClusterPmcSpawns(markers);

        // Then: Each connected group is represented by its centroid.
        Assert.Collection(
            result,
            marker => Assert.Equal(new WorldPoint(10, 0), marker.Position),
            marker => Assert.Equal(new WorldPoint(100, 50), marker.Position));
    }

    [Fact]
    public void ShouldKeepNonSpawnMarkersUnchangedWhenClusteringPmcSpawns()
    {
        // Given: Extract and transit markers alongside nearby PMC spawn candidates.
        MapMarker[] markers =
        [
            new(MapMarkerKind.ScavExtract, "Scav House", new WorldPoint(0, 0)),
            new(MapMarkerKind.Transit, "Transit to Woods", new WorldPoint(5, 0)),
            new(MapMarkerKind.PmcSpawn, null, new WorldPoint(10, 0)),
            new(MapMarkerKind.PmcSpawn, null, new WorldPoint(20, 0)),
        ];

        // When: PMC spawn clustering is applied.
        var result = MapMarkerClusterer.ClusterPmcSpawns(markers);

        // Then: Named map markers remain intact and only spawn candidates are merged.
        Assert.Equal(markers[0], result[0]);
        Assert.Equal(markers[1], result[1]);
        Assert.Equal(new WorldPoint(15, 0), result[2].Position);
    }
}
