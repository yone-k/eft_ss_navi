using EftSsMap.Core.Calibration;

namespace EftSsMap.Core.Tests.Calibration;

public sealed class MapProfileTests
{
    [Fact]
    public void ShouldCreatePersistableProfileBeforeCalibrationStarts()
    {
        // Given: A map name and the fingerprint of its selected image.
        var fingerprint = new EftSsMap.Core.Images.ImageFingerprint(
            @"C:\maps\interchange.png",
            4096,
            3440,
            "hash");

        // When: The map is added before any screenshots have been detected.
        var profile = MapProfile.CreateUncalibrated("Interchange", fingerprint);

        // Then: Image identity is retained while calibration remains incomplete.
        Assert.Equal("Interchange", profile.DisplayName);
        Assert.Equal(fingerprint.Path, profile.ImagePath);
        Assert.Equal(fingerprint.Width, profile.CalibratedImageWidth);
        Assert.Equal(fingerprint.Height, profile.CalibratedImageHeight);
        Assert.Equal(fingerprint.Sha256, profile.ImageSha256);
        Assert.Empty(profile.CalibrationPoints);
        Assert.Equal(default, profile.Transform);
    }

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
        Assert.Equal(0, profile.ImageRotationQuarterTurns);
    }

    [Fact]
    public void ShouldRotateProfileWithoutChangingCalibrationMetadata()
    {
        // Given: A calibrated profile with its original display orientation.
        var original = new MapProfile(
            "Woods",
            @"C:\maps\woods.png",
            100,
            60,
            "hash",
            [
                new CalibrationPoint(new WorldPoint(0, 0), new PixelPoint(0, 0)),
                new CalibrationPoint(new WorldPoint(1, 0), new PixelPoint(10, 0)),
                new CalibrationPoint(new WorldPoint(0, 1), new PixelPoint(0, 10)),
            ],
            new AffineTransform2D(10, 0, 0, 10, 0, 0));

        // When: Its image is rotated one quarter turn clockwise.
        var rotated = original.WithImageRotationQuarterTurns(1);

        // Then: Only the profile display setting changes.
        Assert.Equal(1, rotated.ImageRotationQuarterTurns);
        Assert.Equal(original.DisplayName, rotated.DisplayName);
        Assert.Equal(original.ImagePath, rotated.ImagePath);
        Assert.Equal(original.CalibratedImageWidth, rotated.CalibratedImageWidth);
        Assert.Equal(original.CalibratedImageHeight, rotated.CalibratedImageHeight);
        Assert.Equal(original.ImageSha256, rotated.ImageSha256);
        Assert.Equal(original.CalibrationPoints, rotated.CalibrationPoints);
        Assert.Equal(original.Transform, rotated.Transform);
    }

}
