using EftSsNavi.App.Controls;
using EftSsNavi.Core.Calibration;
using EftSsNavi.Core.Settings;

namespace EftSsNavi.App.Tests.Controls;

public sealed class MapMarkerOverlayTests
{
    [Fact]
    public void ShouldProjectWorldMarkersThroughSelectedProfileCalibration()
    {
        // Given: World markers and a calibrated image transform.
        var overlay = new MapMarkerOverlay();
        MapMarker[] markers =
        [
            new(MapMarkerKind.PmcExtract, "Outskirts", new WorldPoint(10, 20)),
            new(MapMarkerKind.PmcSpawn, null, new WorldPoint(-5, 4)),
        ];
        var transform = new AffineTransform2D(2, 0, 0, 3, 100, 200);

        // When: The overlay is prepared for the selected profile.
        overlay.Set(markers, transform);

        // Then: Both icon positions use the same calibration as the player marker.
        Assert.Collection(
            overlay.Markers,
            marker => Assert.Equal(new PixelPoint(120, 260), marker.Position),
            marker => Assert.Equal(new PixelPoint(90, 212), marker.Position));
    }

    [Fact]
    public void ShouldClearMarkersWhenSelectedProfileHasNoTarkovDevData()
    {
        // Given: A marker overlay showing one bundled marker.
        var overlay = new MapMarkerOverlay();
        overlay.Set(
            [new MapMarker(MapMarkerKind.PmcSpawn, null, new WorldPoint(0, 0))],
            new AffineTransform2D(1, 0, 0, 1, 0, 0));

        // When: A profile without marker data is selected.
        overlay.Set([], default);

        // Then: Stale markers from the previous map are removed.
        Assert.Empty(overlay.Markers);
    }
}
