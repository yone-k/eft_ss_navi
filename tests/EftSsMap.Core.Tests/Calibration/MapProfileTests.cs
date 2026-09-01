using EftSsMap.Core.Calibration;

namespace EftSsMap.Core.Tests.Calibration;

public sealed class MapProfileTests
{
    [Fact]
    public void ShouldRetainAllCalibrationMetadataWhenProfileIsCreated()
    {
        // Given: Complete metadata from a successful three-point calibration.
        const string displayName = "Woods";
        const string absoluteImagePath = @"C:\maps\woods.webp";
        const int calibratedWidth = 7000;
        const int calibratedHeight = 6800;
        var imageSha256 = new string('a', 64);
        CalibrationPoint[] calibrationPoints =
        [
            new(new WorldPoint(0, 0), new PixelPoint(10, 20)),
            new(new WorldPoint(1, 0), new PixelPoint(12, 20)),
            new(new WorldPoint(0, 1), new PixelPoint(10, 23)),
        ];
        var coefficients = new AffineTransform2D(2, 0, 0, 3, 10, 20);

        // When: A map profile is created.
        var profile = new MapProfile(
            displayName,
            absoluteImagePath,
            calibratedWidth,
            calibratedHeight,
            imageSha256,
            calibrationPoints,
            coefficients);

        // Then: Every value needed to validate and restore calibration is retained.
        Assert.Equal(displayName, profile.DisplayName);
        Assert.Equal(absoluteImagePath, profile.ImagePath);
        Assert.Equal(calibratedWidth, profile.CalibratedImageWidth);
        Assert.Equal(calibratedHeight, profile.CalibratedImageHeight);
        Assert.Equal(imageSha256, profile.ImageSha256);
        Assert.Equal(calibrationPoints, profile.CalibrationPoints);
        Assert.Equal(coefficients, profile.Transform);
    }

}
