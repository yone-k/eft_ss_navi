using EftSsNavi.Core.Calibration;
using EftSsNavi.Core.Viewport;

namespace EftSsNavi.Core.Tests.Viewport;

public sealed class MapImageRotationTests
{
    private static readonly Size2D ImageSize = new(100, 60);

    [Theory]
    [InlineData(0, 100, 60)]
    [InlineData(1, 60, 100)]
    [InlineData(2, 100, 60)]
    [InlineData(3, 60, 100)]
    public void ShouldSwapDisplayDimensionsForOddQuarterTurns(
        int quarterTurns,
        double expectedWidth,
        double expectedHeight)
    {
        // Given: A non-square image and a quarter-turn display rotation.
        var rotation = new MapImageRotation(quarterTurns);

        // When: Its displayed bounds are requested.
        var displaySize = rotation.GetDisplaySize(ImageSize);

        // Then: Width and height swap only at 90 and 270 degrees.
        Assert.Equal(new Size2D(expectedWidth, expectedHeight), displaySize);
    }

    [Theory]
    [InlineData(0, 20, 10)]
    [InlineData(1, 50, 20)]
    [InlineData(2, 80, 50)]
    [InlineData(3, 10, 80)]
    public void ShouldRotateImagePointClockwiseAroundImageBounds(
        int quarterTurns,
        double expectedX,
        double expectedY)
    {
        // Given: A point in original image coordinates.
        var rotation = new MapImageRotation(quarterTurns);

        // When: It is converted to displayed coordinates.
        var displayed = rotation.ImageToDisplay(new PixelPoint(20, 10), ImageSize);

        // Then: The point follows the selected clockwise quarter turns.
        Assert.Equal(new PixelPoint(expectedX, expectedY), displayed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ShouldRestoreOriginalPointAfterDisplayRoundTrip(int quarterTurns)
    {
        // Given: A point transformed into the rotated display space.
        var rotation = new MapImageRotation(quarterTurns);
        var original = new PixelPoint(20, 10);
        var displayed = rotation.ImageToDisplay(original, ImageSize);

        // When: The displayed position is converted back for calibration.
        var restored = rotation.DisplayToImage(displayed, ImageSize);

        // Then: Calibration continues to use original image coordinates.
        Assert.Equal(original, restored);
    }

    [Theory]
    [InlineData(0, 3, 4)]
    [InlineData(1, -4, 3)]
    [InlineData(2, -3, -4)]
    [InlineData(3, 4, -3)]
    public void ShouldRotateMarkerDirectionWithoutTranslation(
        int quarterTurns,
        double expectedX,
        double expectedY)
    {
        // Given: A marker direction in original image coordinates.
        var rotation = new MapImageRotation(quarterTurns);

        // When: The direction is rotated for display.
        var displayed = rotation.DirectionToDisplay(new PixelPoint(3, 4));

        // Then: Only its orientation changes.
        Assert.Equal(new PixelPoint(expectedX, expectedY), displayed);
    }

    [Theory]
    [InlineData(-1, 3)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    public void ShouldNormalizeQuarterTurns(int requested, int expected)
    {
        // When: A rotation outside the canonical range is created.
        var rotation = new MapImageRotation(requested);

        // Then: It wraps into the persisted 0-3 range.
        Assert.Equal(expected, rotation.QuarterTurns);
    }
}
