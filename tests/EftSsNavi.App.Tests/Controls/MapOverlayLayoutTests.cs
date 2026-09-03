using EftSsNavi.App.Controls;
using EftSsNavi.Core.Viewport;

namespace EftSsNavi.App.Tests.Controls;

public sealed class MapOverlayLayoutTests
{
    [Fact]
    public void ShouldUseFullHdMapAreaAsScaleBaseline()
    {
        var scale = MapOverlayLayout.CalculateNormalScale(new Size2D(1600, 900));

        Assert.Equal(1, scale, 6);
    }

    [Theory]
    [InlineData(1200, 900, 0.75)]
    [InlineData(1600, 675, 0.75)]
    public void ShouldUseSmallerMapAreaRatio(double width, double height, double expected)
    {
        var scale = MapOverlayLayout.CalculateNormalScale(new Size2D(width, height));

        Assert.Equal(expected, scale, 6);
    }

    [Theory]
    [InlineData(160, 90, 0.75)]
    [InlineData(3200, 1800, 1.75)]
    public void ShouldClampNormalScaleToReadableBounds(double width, double height, double expected)
    {
        var scale = MapOverlayLayout.CalculateNormalScale(new Size2D(width, height));

        Assert.Equal(expected, scale, 6);
    }

    [Fact]
    public void ShouldScaleMarkerDimensionsTogether()
    {
        var metrics = MapOverlayLayout.CreateMarkerMetrics(0.75);

        Assert.Equal(9, metrics.LabelFontSize, 6);
        Assert.Equal(5.625, metrics.ExtractRadius, 6);
        Assert.Equal(2.25, metrics.LabelOutlineWidth, 6);
        Assert.Equal(8.25, metrics.LabelOffsetX, 6);
    }
}
