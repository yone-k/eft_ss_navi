using EftSsMap.Core.Images;

namespace EftSsMap.Core.Tests.Images;

public sealed class ProfileImageValidatorTests
{
    private const string Hash = "0123456789abcdef";

    [Fact]
    public void ShouldReturnMatchWhenAllFingerprintValuesMatch()
    {
        // Given: Matching calibrated and current image fingerprints.
        var calibrated = new ImageFingerprint(@"C:\Maps\Woods.png", 7000, 6000, Hash);
        var current = new ImageFingerprint(@"C:\Maps\Woods.png", 7000, 6000, Hash);

        // When: The current image is validated.
        var result = ProfileImageValidator.Validate(calibrated, current);

        // Then: The image is accepted.
        Assert.Equal(ProfileImageValidationResult.Match, result);
    }

    [Fact]
    public void ShouldTreatWindowsImagePathsAsCaseInsensitive()
    {
        // Given: Fingerprints whose Windows paths differ only by casing.
        var calibrated = new ImageFingerprint(@"C:\Maps\Woods.png", 7000, 6000, Hash);
        var current = new ImageFingerprint(@"c:\maps\WOODS.PNG", 7000, 6000, Hash);

        // When: The current image is validated.
        var result = ProfileImageValidator.Validate(calibrated, current);

        // Then: Path casing does not invalidate the calibration.
        Assert.Equal(ProfileImageValidationResult.Match, result);
    }

    [Fact]
    public void ShouldReturnPathMismatchWhenImagePathChanges()
    {
        // Given: Fingerprints for different image paths.
        var calibrated = new ImageFingerprint(@"C:\Maps\Woods.png", 7000, 6000, Hash);
        var current = new ImageFingerprint(@"C:\Maps\Customs.png", 7000, 6000, Hash);

        // When: The current image is validated.
        var result = ProfileImageValidator.Validate(calibrated, current);

        // Then: The path mismatch is identified.
        Assert.Equal(ProfileImageValidationResult.PathMismatch, result);
    }

    [Fact]
    public void ShouldReturnWidthMismatchWhenImageWidthChanges()
    {
        // Given: Fingerprints with different widths.
        var calibrated = new ImageFingerprint(@"C:\Maps\Woods.png", 7000, 6000, Hash);
        var current = new ImageFingerprint(@"C:\Maps\Woods.png", 6999, 6000, Hash);

        // When: The current image is validated.
        var result = ProfileImageValidator.Validate(calibrated, current);

        // Then: The width mismatch is identified.
        Assert.Equal(ProfileImageValidationResult.WidthMismatch, result);
    }

    [Fact]
    public void ShouldReturnHeightMismatchWhenImageHeightChanges()
    {
        // Given: Fingerprints with different heights.
        var calibrated = new ImageFingerprint(@"C:\Maps\Woods.png", 7000, 6000, Hash);
        var current = new ImageFingerprint(@"C:\Maps\Woods.png", 7000, 5999, Hash);

        // When: The current image is validated.
        var result = ProfileImageValidator.Validate(calibrated, current);

        // Then: The height mismatch is identified.
        Assert.Equal(ProfileImageValidationResult.HeightMismatch, result);
    }

    [Fact]
    public void ShouldReturnHashMismatchWhenImageContentChanges()
    {
        // Given: Fingerprints with different content hashes.
        var calibrated = new ImageFingerprint(@"C:\Maps\Woods.png", 7000, 6000, Hash);
        var current = new ImageFingerprint(@"C:\Maps\Woods.png", 7000, 6000, "fedcba9876543210");

        // When: The current image is validated.
        var result = ProfileImageValidator.Validate(calibrated, current);

        // Then: The hash mismatch is identified.
        Assert.Equal(ProfileImageValidationResult.HashMismatch, result);
    }
}
