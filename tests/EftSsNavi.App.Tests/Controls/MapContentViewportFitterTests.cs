using EftSsNavi.App.Controls;
using EftSsNavi.Core.Calibration;
using EftSsNavi.Core.Viewport;

namespace EftSsNavi.App.Tests.Controls;

public sealed class MapContentViewportFitterTests
{
    private const double Precision = 1e-6;

    [Fact]
    public void ShouldKeepImageAndOutsideLabelInsidePaddedView()
    {
        var imageSize = new Size2D(1000, 500);
        var viewSize = new Size2D(800, 500);
        AnchoredVisualBounds[] visuals =
        [
            new(new PixelPoint(1100, 250), -8, -8, 120, 8),
        ];

        var result = MapContentViewportFitter.Fit(imageSize, viewSize, visuals, 1, 8);

        AssertContentInside(viewSize, imageSize, visuals, result, 8);
    }

    [Fact]
    public void ShouldKeepNormalOverlayScaleWhenItFits()
    {
        AnchoredVisualBounds[] visuals =
        [
            new(new PixelPoint(50, 50), -5, -5, 5, 5),
        ];

        var result = MapContentViewportFitter.Fit(
            new Size2D(100, 100),
            new Size2D(800, 600),
            visuals,
            maximumOverlayScale: 1.25,
            padding: 8);

        Assert.Equal(1.25, result.OverlayScale, Precision);
    }

    [Fact]
    public void ShouldUseLargestFittingScaleAboveSeventyFivePercent()
    {
        AnchoredVisualBounds[] visuals =
        [
            new(new PixelPoint(50, 50), 0, -5, 90, 5),
        ];

        var result = MapContentViewportFitter.Fit(
            new Size2D(100, 100),
            new Size2D(100, 100),
            visuals,
            maximumOverlayScale: 1,
            padding: 8);

        Assert.InRange(result.OverlayScale, 0.75, 0.999999);
        AssertContentInside(new Size2D(100, 100), new Size2D(100, 100), visuals, result, 8);
    }

    [Fact]
    public void ShouldAllowScaleBelowSeventyFivePercentToPreserveCompleteDisplay()
    {
        AnchoredVisualBounds[] visuals =
        [
            new(new PixelPoint(50, 50), 0, -5, 240, 5),
        ];

        var result = MapContentViewportFitter.Fit(
            new Size2D(100, 100),
            new Size2D(100, 100),
            visuals,
            maximumOverlayScale: 1,
            padding: 8);

        Assert.InRange(result.OverlayScale, double.Epsilon, 0.749999);
        AssertContentInside(new Size2D(100, 100), new Size2D(100, 100), visuals, result, 8);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ShouldFitOutsideMarkerAtEveryQuarterTurn(int quarterTurns)
    {
        var rotation = new MapImageRotation(quarterTurns);
        var imageSize = new Size2D(1000, 500);
        var displaySize = rotation.GetDisplaySize(imageSize);
        var displayMarker = rotation.ImageToDisplay(new PixelPoint(1100, 250), imageSize);
        AnchoredVisualBounds[] visuals =
        [
            new(displayMarker, -8, -8, 80, 8),
        ];

        var result = MapContentViewportFitter.Fit(
            displaySize,
            new Size2D(800, 600),
            visuals,
            maximumOverlayScale: 1,
            padding: 8);

        AssertContentInside(new Size2D(800, 600), displaySize, visuals, result, 8);
    }

    [Fact]
    public void ShouldRetainImageOnlyFitWhenNoMapInformationExists()
    {
        var imageSize = new Size2D(1000, 500);
        var viewSize = new Size2D(800, 600);

        var result = MapContentViewportFitter.Fit(imageSize, viewSize, [], 1, 8);

        Assert.Equal(ViewportTransform.Fit(imageSize, viewSize), result.Viewport);
        Assert.Equal(1, result.OverlayScale, Precision);
    }

    private static void AssertContentInside(
        Size2D viewSize,
        Size2D imageSize,
        IReadOnlyList<AnchoredVisualBounds> visuals,
        MapContentFitResult result,
        double padding)
    {
        var imageTopLeft = result.Viewport.ImageToView(default);
        var imageBottomRight = result.Viewport.ImageToView(
            new PixelPoint(imageSize.Width, imageSize.Height));
        Assert.InRange(imageTopLeft.X, padding - Precision, viewSize.Width);
        Assert.InRange(imageTopLeft.Y, padding - Precision, viewSize.Height);
        Assert.InRange(imageBottomRight.X, 0, viewSize.Width - padding + Precision);
        Assert.InRange(imageBottomRight.Y, 0, viewSize.Height - padding + Precision);

        foreach (var visual in visuals)
        {
            var anchor = result.Viewport.ImageToView(visual.Anchor);
            Assert.InRange(anchor.X + (visual.Left * result.OverlayScale), padding - Precision, viewSize.Width);
            Assert.InRange(anchor.Y + (visual.Top * result.OverlayScale), padding - Precision, viewSize.Height);
            Assert.InRange(anchor.X + (visual.Right * result.OverlayScale), 0, viewSize.Width - padding + Precision);
            Assert.InRange(anchor.Y + (visual.Bottom * result.OverlayScale), 0, viewSize.Height - padding + Precision);
        }
    }
}
