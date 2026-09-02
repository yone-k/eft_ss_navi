using EftSsMap.App.Controls;
using EftSsMap.Core.Calibration;

namespace EftSsMap.App.Tests.Controls;

public sealed class NavigationCursorGeometryTests
{
    [Fact]
    public void ShouldUseCompactCursorDimensions()
    {
        // Given: A cursor pointing to the right from a known center.
        var points = NavigationCursorGeometry.Create(
            new PixelPoint(100, 100),
            new PixelPoint(1, 0));

        // Then: The cursor stays compact around the position.
        Assert.Equal(114, points.Tip.X, 6);
        Assert.Equal(100, points.Tip.Y, 6);
        Assert.Equal(93, points.RearLeft.X, 6);
        Assert.Equal(93, points.RearRight.X, 6);
        Assert.Equal(13, Math.Abs(points.RearLeft.Y - points.RearRight.Y), 6);
    }

    [Fact]
    public void ShouldUseShallowRearNotch()
    {
        // Given: A cursor pointing to the right.
        var points = NavigationCursorGeometry.Create(
            new PixelPoint(100, 100),
            new PixelPoint(1, 0));

        // Then: The notch cuts only slightly forward from the rear edge.
        Assert.Equal(98.25, points.Notch.X, 6);
        Assert.Equal(5.25, points.Notch.X - points.RearLeft.X, 6);
    }

    [Fact]
    public void ShouldUseThinOutline()
    {
        Assert.Equal(1.2f, NavigationCursorGeometry.OutlineStrokeWidth);
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
