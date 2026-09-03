using EftSsNavi.App.Controls;
using EftSsNavi.Core.Calibration;

namespace EftSsNavi.App.Tests.Controls;

public sealed class PartyMarkerOverlayTests
{
    public static TheoryData<int, string> PartyColors => new()
    {
        { 0, "#2F80ED" },
        { 1, "#F2C94C" },
        { 2, "#9B51E0" },
        { 3, "#FF6FB5" },
        { 4, "#F5F5F5" },
    };

    [Theory]
    [MemberData(nameof(PartyColors))]
    public void ShouldUseFixedPartyColorWhenColorIndexIsAssigned(int colorIndex, string expectedColor)
    {
        // Given: A current party marker with an assigned participant color.
        var overlay = new PartyMarkerOverlay();
        overlay.Set([CreateVisual(colorIndex: colorIndex)]);

        // When: The marker snapshot is read for drawing.
        var marker = Assert.Single(overlay.Markers);

        // Then: The documented fixed color is exposed to the renderer.
        Assert.Equal(expectedColor, marker.ColorHex);
    }

    [Fact]
    public void ShouldUseDirectionalArrowWhenDirectionIsAvailable()
    {
        // Given: A party marker with a projected direction.
        var overlay = new PartyMarkerOverlay();
        overlay.Set([CreateVisual(direction: new PixelPoint(20, 10))]);

        // When: The marker snapshot is read for drawing.
        var marker = Assert.Single(overlay.Markers);

        // Then: The marker uses the same directional arrow shape as navigation.
        Assert.Equal(PartyMarkerShape.Arrow, marker.Shape);
    }

    [Fact]
    public void ShouldUseCircleWhenDirectionIsUnavailable()
    {
        // Given: A party marker without a projected direction.
        var overlay = new PartyMarkerOverlay();
        overlay.Set([CreateVisual(direction: null)]);

        // When: The marker snapshot is read for drawing.
        var marker = Assert.Single(overlay.Markers);

        // Then: Directionless participants remain visible as a circle.
        Assert.Equal(PartyMarkerShape.Circle, marker.Shape);
    }

    [Fact]
    public void ShouldUseHalfOpacityWhenPositionIsStale()
    {
        // Given: A party marker whose latest position is stale.
        var overlay = new PartyMarkerOverlay();
        overlay.Set([CreateVisual(isStale: true)]);

        // When: The marker snapshot is read for drawing.
        var marker = Assert.Single(overlay.Markers);

        // Then: The marker and its label share 50 percent opacity.
        Assert.Equal(0.5, marker.Opacity);
    }

    [Fact]
    public void ShouldSnapshotPartyMarkersWhenSet()
    {
        // Given: A mutable marker collection supplied by MainWindow.
        var source = new List<PartyMarkerVisual> { CreateVisual(displayName: "Alpha") };
        var overlay = new PartyMarkerOverlay();

        // When: The overlay accepts the collection and the caller later mutates it.
        overlay.Set(source);
        source.Clear();

        // Then: The active draw snapshot is unaffected by the caller mutation.
        var marker = Assert.Single(overlay.Markers);
        Assert.Equal("Alpha", marker.DisplayName);
    }

    private static PartyMarkerVisual CreateVisual(
        string displayName = "Teammate",
        PixelPoint? direction = default,
        int colorIndex = 0,
        bool isStale = false) => new(
            displayName,
            new PixelPoint(10, 10),
            direction,
            colorIndex,
            isStale);
}
