using EftSsNavi.App.Controls;
using EftSsNavi.Core.Calibration;

namespace EftSsNavi.App.Tests.Controls;

public sealed class NavigationCursorGeometryTests
{
    [Fact]
    public void ShouldUseTenPercentLargerCursorAtFullHdBaseline()
    {
        // Given: A cursor pointing to the right from a known center.
        var points = NavigationCursorGeometry.Create(
            new PixelPoint(100, 100),
            new PixelPoint(1, 0));

        // Then: The cursor stays compact around the position.
        Assert.Equal(115.4, points.Tip.X, 6);
        Assert.Equal(100, points.Tip.Y, 6);
        Assert.Equal(92.3, points.RearLeft.X, 6);
        Assert.Equal(92.3, points.RearRight.X, 6);
        Assert.Equal(14.3, Math.Abs(points.RearLeft.Y - points.RearRight.Y), 6);
    }

    [Fact]
    public void ShouldUseShallowRearNotch()
    {
        // Given: A cursor pointing to the right.
        var points = NavigationCursorGeometry.Create(
            new PixelPoint(100, 100),
            new PixelPoint(1, 0));

        // Then: The notch cuts only slightly forward from the rear edge.
        Assert.Equal(98.075, points.Notch.X, 6);
        Assert.Equal(5.775, points.Notch.X - points.RearLeft.X, 6);
    }

    [Fact]
    public void ShouldUseThinOutline()
    {
        Assert.Equal(1.32f, NavigationCursorGeometry.GetOutlineStrokeWidth(1), 3);
    }

    [Fact]
    public void ShouldScaleCursorFromEnlargedBaseline()
    {
        var points = NavigationCursorGeometry.Create(
            new PixelPoint(100, 100),
            new PixelPoint(1, 0),
            displayScale: 0.5);

        Assert.Equal(107.7, points.Tip.X, 6);
        Assert.Equal(96.15, points.RearLeft.X, 6);
    }

    [Fact]
    public void ShouldUseUpwardDirectionWhenDirectionIsUnavailable()
    {
        var points = NavigationCursorGeometry.Create(
            new PixelPoint(50, 50),
            direction: null);

        Assert.Equal(50, points.Tip.X, 6);
        Assert.True(points.Tip.Y < 50);
    }
}
