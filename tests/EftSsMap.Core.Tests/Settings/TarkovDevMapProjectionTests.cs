using EftSsMap.Core.Calibration;
using EftSsMap.Core.Settings;

namespace EftSsMap.Core.Tests.Settings;

public sealed class TarkovDevMapProjectionTests
{
    private const double Precision = 1e-8;

    [Fact]
    public void ShouldMapOppositeCustomsBoundsToOppositeImageCorners()
    {
        // Given: The projection and bounds published for Customs by tarkov.dev.
        var projection = new TarkovDevMapProjection(
            scaleX: 0.239,
            marginX: 168.65,
            scaleY: 0.239,
            marginY: 136.35,
            rotationDegrees: 180,
            firstBound: new WorldPoint(698, -307),
            secondBound: new WorldPoint(-372, 237));

        // When: EFT coordinates are projected onto a raster of the SVG overlay.
        var transform = projection.CreateTransform(imageWidth: 4096, imageHeight: 2082);

        // Then: The published bounds cover the complete image.
        AssertPixel(new PixelPoint(0, 0), transform.TransformPosition(new WorldPoint(698, -307)));
        AssertPixel(new PixelPoint(4096, 2082), transform.TransformPosition(new WorldPoint(-372, 237)));
    }

    [Fact]
    public void ShouldApplyFactoryQuarterTurnBeforeMappingImageCorners()
    {
        // Given: Factory's 90-degree tarkov.dev projection.
        var projection = new TarkovDevMapProjection(
            scaleX: 1.629,
            marginX: 119.9,
            scaleY: 1.629,
            marginY: 139.3,
            rotationDegrees: 90,
            firstBound: new WorldPoint(77, -64.5),
            secondBound: new WorldPoint(-65.5, 67.4));

        // When: Its coordinate transform is created.
        var transform = projection.CreateTransform(imageWidth: 3791, imageHeight: 4096);

        // Then: Rotation changes which horizontal image edge each world bound reaches.
        AssertPixel(new PixelPoint(3791, 0), transform.TransformPosition(new WorldPoint(77, -64.5)));
        AssertPixel(new PixelPoint(0, 4096), transform.TransformPosition(new WorldPoint(-65.5, 67.4)));
    }

    [Fact]
    public void ShouldCreateThreeExactCalibrationPointsForManualCorrectionCompatibility()
    {
        // Given: A valid tarkov.dev projection.
        var projection = new TarkovDevMapProjection(
            scaleX: 1,
            marginX: 10,
            scaleY: 1,
            marginY: 20,
            rotationDegrees: 180,
            firstBound: new WorldPoint(100, -50),
            secondBound: new WorldPoint(-100, 50));

        // When: Calibration points are generated for a bundled profile.
        var points = projection.CreateCalibrationPoints(imageWidth: 1000, imageHeight: 500);
        var created = AffineCalibration.TryCreate(points, out var recalculated);

        // Then: Existing three-point correction can reproduce the generated transform.
        Assert.Equal(3, points.Count);
        Assert.True(created);
        var expected = projection.CreateTransform(imageWidth: 1000, imageHeight: 500);
        AssertPixel(
            expected.TransformPosition(new WorldPoint(25, -10)),
            recalculated.TransformPosition(new WorldPoint(25, -10)));
    }

    private static void AssertPixel(PixelPoint expected, PixelPoint actual)
    {
        Assert.Equal(expected.X, actual.X, Precision);
        Assert.Equal(expected.Y, actual.Y, Precision);
    }
}
