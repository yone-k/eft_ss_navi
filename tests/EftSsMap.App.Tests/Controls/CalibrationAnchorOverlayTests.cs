using EftSsMap.App.Controls;
using EftSsMap.Core.Calibration;

namespace EftSsMap.App.Tests.Controls;

public sealed class CalibrationAnchorOverlayTests
{
    [Fact]
    public void ShouldNumberThreeAnchorsAndHighlightReplacementTarget()
    {
        // Given: Three calibration anchors and the second replacement target.
        var overlay = new CalibrationAnchorOverlay();
        CalibrationPoint[] points =
        [
            new(new WorldPoint(0, 0), new PixelPoint(100, 200)),
            new(new WorldPoint(10, 0), new PixelPoint(300, 400)),
            new(new WorldPoint(0, 10), new PixelPoint(500, 600)),
        ];

        // When: The correction overlay is shown.
        overlay.Show(points, replacementIndex: 1);

        // Then: All anchors are numbered and only the replacement target is highlighted.
        Assert.Collection(
            overlay.Anchors,
            anchor => AssertAnchor(anchor, 1, new PixelPoint(100, 200), false),
            anchor => AssertAnchor(anchor, 2, new PixelPoint(300, 400), true),
            anchor => AssertAnchor(anchor, 3, new PixelPoint(500, 600), false));
    }

    [Fact]
    public void ShouldHideEveryAnchorWhenCorrectionModeEnds()
    {
        // Given: A visible correction overlay.
        var overlay = new CalibrationAnchorOverlay();
        overlay.Show(
            [
                new CalibrationPoint(new WorldPoint(0, 0), new PixelPoint(100, 200)),
                new CalibrationPoint(new WorldPoint(10, 0), new PixelPoint(300, 400)),
                new CalibrationPoint(new WorldPoint(0, 10), new PixelPoint(500, 600)),
            ],
            replacementIndex: 0);

        // When: Correction mode ends.
        overlay.Hide();

        // Then: No calibration anchor remains visible.
        Assert.Empty(overlay.Anchors);
    }

    [Fact]
    public void ShouldHitTestDisplayedAnchorInViewCoordinates()
    {
        // Given: Three displayed anchors and a view transform.
        var overlay = new CalibrationAnchorOverlay();
        overlay.Show(
            [
                new CalibrationPoint(new WorldPoint(0, 0), new PixelPoint(100, 200)),
                new CalibrationPoint(new WorldPoint(10, 0), new PixelPoint(300, 400)),
                new CalibrationPoint(new WorldPoint(0, 10), new PixelPoint(500, 600)),
            ],
            replacementIndex: 0);

        // When: The user clicks within the second displayed anchor.
        var found = overlay.TryHitTest(
            new PixelPoint(604, 805),
            imagePoint => new PixelPoint(imagePoint.X * 2, imagePoint.Y * 2),
            out var anchorIndex);

        // Then: The corresponding zero-based calibration-point index is returned.
        Assert.True(found);
        Assert.Equal(1, anchorIndex);
    }

    private static void AssertAnchor(
        CalibrationAnchor anchor,
        int number,
        PixelPoint position,
        bool willBeReplaced)
    {
        Assert.Equal(number, anchor.Number);
        Assert.Equal(position, anchor.Position);
        Assert.Equal(willBeReplaced, anchor.WillBeReplaced);
    }
}
