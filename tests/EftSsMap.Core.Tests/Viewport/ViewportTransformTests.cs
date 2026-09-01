using EftSsMap.Core.Calibration;
using EftSsMap.Core.Viewport;

namespace EftSsMap.Core.Tests.Viewport;

public sealed class ViewportTransformTests
{
    private const double Precision = 1e-10;

    [Fact]
    public void ShouldFitLandscapeImageCenteredInsideView()
    {
        // Given: A landscape image and a square view.
        var imageSize = new Size2D(1000, 500);
        var viewSize = new Size2D(800, 800);

        // When: A fit transform is created.
        var transform = ViewportTransform.Fit(imageSize, viewSize);

        // Then: The complete image is centered with vertical letterboxing.
        AssertPoint(new PixelPoint(0, 200), transform.ImageToView(new PixelPoint(0, 0)));
        AssertPoint(new PixelPoint(800, 600), transform.ImageToView(new PixelPoint(1000, 500)));
    }

    [Fact]
    public void ShouldFitPortraitImageCenteredInsideView()
    {
        // Given: A portrait image and a square view.
        var imageSize = new Size2D(500, 1000);
        var viewSize = new Size2D(800, 800);

        // When: A fit transform is created.
        var transform = ViewportTransform.Fit(imageSize, viewSize);

        // Then: The complete image is centered with horizontal letterboxing.
        AssertPoint(new PixelPoint(200, 0), transform.ImageToView(new PixelPoint(0, 0)));
        AssertPoint(new PixelPoint(600, 800), transform.ImageToView(new PixelPoint(500, 1000)));
    }

    [Fact]
    public void ShouldRoundTripImagePointThroughViewCoordinates()
    {
        // Given: A fitted viewport transform and an image point.
        var transform = ViewportTransform.Fit(new Size2D(1200, 600), new Size2D(900, 700));
        var imagePoint = new PixelPoint(321.25, 456.75);

        // When: The point is transformed to view space and back.
        var roundTripped = transform.ViewToImage(transform.ImageToView(imagePoint));

        // Then: The original image coordinate is recovered.
        AssertPoint(imagePoint, roundTripped);
    }

    [Fact]
    public void ShouldKeepPointerImageCoordinateFixedWhenZooming()
    {
        // Given: A transform and a pointer in view coordinates.
        var transform = ViewportTransform.Fit(new Size2D(1000, 500), new Size2D(800, 800));
        var pointer = new PixelPoint(275, 350);
        var imagePointUnderPointer = transform.ViewToImage(pointer);

        // When: The view is zoomed around the pointer.
        var zoomed = transform.ZoomAt(pointer, 2, 0.1, 10);

        // Then: The same image coordinate remains under the pointer.
        AssertPoint(pointer, zoomed.ImageToView(imagePointUnderPointer));
    }

    [Fact]
    public void ShouldTranslateViewCoordinatesWhenPanning()
    {
        // Given: A fitted transform and an image coordinate.
        var transform = ViewportTransform.Fit(new Size2D(100, 100), new Size2D(200, 200));
        var imagePoint = new PixelPoint(20, 30);
        var before = transform.ImageToView(imagePoint);

        // When: The viewport is panned.
        var panned = transform.Pan(15, -8);

        // Then: The view coordinate moves by the pan delta.
        AssertPoint(new PixelPoint(before.X + 15, before.Y - 8), panned.ImageToView(imagePoint));
    }

    [Theory]
    [InlineData(100, 2)]
    [InlineData(0.001, 0.5)]
    public void ShouldClampZoomScaleToConfiguredBounds(double factor, double expectedScale)
    {
        // Given: A unit-scale viewport with configured zoom limits.
        var transform = new ViewportTransform(1, 0, 0);

        // When: Zoom would exceed one of the limits.
        var zoomed = transform.ZoomAt(new PixelPoint(0, 0), factor, 0.5, 2);

        // Then: The scale is clamped at that limit.
        Assert.Equal(expectedScale, zoomed.Scale, Precision);
    }

    [Fact]
    public void ShouldLeaveOriginalTransformAndImagePointUnchangedAfterDisplayOperations()
    {
        // Given: An immutable display transform and a calibration point in image coordinates.
        var transform = new ViewportTransform(1, 10, 20);
        var calibrationPoint = new PixelPoint(300, 400);

        // When: New zoomed and panned transforms are produced.
        _ = transform.ZoomAt(new PixelPoint(50, 60), 2, 0.5, 4).Pan(12, 13);

        // Then: Neither the source transform nor calibration coordinate is mutated.
        Assert.Equal(new ViewportTransform(1, 10, 20), transform);
        Assert.Equal(new PixelPoint(300, 400), calibrationPoint);
    }

    public static TheoryData<double, double> InvalidSizes => new()
    {
        { 0, 10 },
        { -1, 10 },
        { double.NaN, 10 },
        { 10, double.PositiveInfinity },
    };

    [Theory]
    [MemberData(nameof(InvalidSizes))]
    public void ShouldRejectNonPositiveOrNonFiniteSize(double width, double height)
    {
        // Given: A non-positive or non-finite dimension.

        // When: A size is created.
        var create = () => new Size2D(width, height);

        // Then: The invalid size is rejected.
        Assert.Throws<ArgumentOutOfRangeException>(create);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ShouldRejectNonPositiveOrNonFiniteTransformScale(double scale)
    {
        // Given: A non-positive or non-finite viewport scale.

        // When: A transform is created.
        var create = () => new ViewportTransform(scale, 0, 0);

        // Then: The invalid transform is rejected.
        Assert.Throws<ArgumentOutOfRangeException>(create);
    }

    [Fact]
    public void ShouldRejectNonFinitePanDelta()
    {
        // Given: A valid viewport transform and a non-finite pan delta.
        var transform = new ViewportTransform(1, 0, 0);

        // When: A non-finite pan is requested.
        var pan = () => transform.Pan(double.NaN, 1);

        // Then: The invalid delta is rejected.
        Assert.Throws<ArgumentOutOfRangeException>(pan);
    }

    [Fact]
    public void ShouldRejectNonPositiveZoomFactor()
    {
        // Given: A valid viewport transform.
        var transform = new ViewportTransform(1, 0, 0);

        // When: A non-positive zoom factor is requested.
        var zoom = () => transform.ZoomAt(new PixelPoint(0, 0), 0, 0.5, 2);

        // Then: The invalid factor is rejected.
        Assert.Throws<ArgumentOutOfRangeException>(zoom);
    }

    [Fact]
    public void ShouldRejectNonFiniteCoordinateConversionInput()
    {
        // Given: A valid viewport transform and a non-finite image coordinate.
        var transform = new ViewportTransform(1, 0, 0);

        // When: The point is converted to view coordinates.
        Action convert = () =>
        {
            _ = transform.ImageToView(new PixelPoint(double.NaN, 0));
        };

        // Then: The invalid coordinate is rejected.
        Assert.Throws<ArgumentOutOfRangeException>(convert);
    }

    private static void AssertPoint(PixelPoint expected, PixelPoint actual)
    {
        Assert.Equal(expected.X, actual.X, Precision);
        Assert.Equal(expected.Y, actual.Y, Precision);
    }
}
