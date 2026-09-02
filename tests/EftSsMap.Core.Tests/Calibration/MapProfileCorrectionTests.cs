using EftSsMap.Core.Calibration;

namespace EftSsMap.Core.Tests.Calibration;

public sealed class MapProfileCorrectionTests
{
    private const double Precision = 1e-10;

    [Fact]
    public void ShouldIdentifyAnchorThatCorrectionWillReplace()
    {
        // Given: Three anchors and a current position near the second anchor.
        var profile = CreateProfile();

        // When: The replacement target is requested.
        var found = MapProfileCorrection.TryFindNearestCalibrationPointIndex(
            profile,
            new WorldPoint(9, 1),
            out var index);

        // Then: The same nearest anchor used by correction is exposed to the UI.
        Assert.True(found);
        Assert.Equal(1, index);
    }

    [Fact]
    public void ShouldReplaceExplicitlySelectedAnchorInsteadOfNearestAnchor()
    {
        // Given: A correction near the first anchor while the third is selected explicitly.
        var profile = CreateProfile();
        var correctedWorld = new WorldPoint(1, 1);
        var correctedPixel = new PixelPoint(120, 80);

        // When: The correction is applied to the selected third anchor.
        var applied = MapProfileCorrection.TryApply(
            profile,
            correctedWorld,
            correctedPixel,
            replacementIndex: 2,
            out var correctedProfile);

        // Then: The third anchor is replaced and the nearer first anchor remains.
        Assert.True(applied);
        Assert.Contains(profile.CalibrationPoints[0], correctedProfile.CalibrationPoints);
        Assert.DoesNotContain(profile.CalibrationPoints[2], correctedProfile.CalibrationPoints);
        Assert.Contains(
            new CalibrationPoint(correctedWorld, correctedPixel),
            correctedProfile.CalibrationPoints);
    }

    [Fact]
    public void ShouldReplaceNearestCalibrationPointAndRecalculateTransform()
    {
        // Given: A profile with three calibration points spread across the map.
        var profile = CreateProfile();
        var correctedWorld = new WorldPoint(1, 1);
        var correctedPixel = new PixelPoint(120, 80);

        // When: A displayed world position is corrected to its actual image position.
        var applied = MapProfileCorrection.TryApply(
            profile,
            correctedWorld,
            correctedPixel,
            out var correctedProfile);

        // Then: The nearest old point is replaced and the correction is reproduced exactly.
        Assert.True(applied);
        Assert.Equal(3, correctedProfile.CalibrationPoints.Count);
        Assert.DoesNotContain(
            new CalibrationPoint(new WorldPoint(0, 0), new PixelPoint(0, 0)),
            correctedProfile.CalibrationPoints);
        Assert.Contains(
            new CalibrationPoint(correctedWorld, correctedPixel),
            correctedProfile.CalibrationPoints);
        AssertPoint(correctedPixel, correctedProfile.Transform.TransformPosition(correctedWorld));
        Assert.Equal(profile.DisplayName, correctedProfile.DisplayName);
        Assert.Equal(profile.ImagePath, correctedProfile.ImagePath);
        Assert.Equal(profile.ImageSha256, correctedProfile.ImageSha256);
    }

    [Fact]
    public void ShouldKeepReplacingSameAnchorForCorrectionsInOneNeighborhood()
    {
        // Given: One corner anchor was replaced by a nearby correction.
        Assert.True(MapProfileCorrection.TryApply(
            CreateProfile(),
            new WorldPoint(1, 1),
            new PixelPoint(120, 80),
            out var onceCorrected));

        // When: Another correction is made in the same neighborhood.
        var latestWorld = new WorldPoint(1.5, 1.25);
        var latestPixel = new PixelPoint(125, 75);
        var applied = MapProfileCorrection.TryApply(
            onceCorrected,
            latestWorld,
            latestPixel,
            out var twiceCorrected);

        // Then: The same anchor is updated while the two distant anchors remain.
        Assert.True(applied);
        Assert.Equal(3, twiceCorrected.CalibrationPoints.Count);
        Assert.DoesNotContain(
            twiceCorrected.CalibrationPoints,
            point => point.World == new WorldPoint(1, 1));
        Assert.Contains(new CalibrationPoint(latestWorld, latestPixel), twiceCorrected.CalibrationPoints);
        Assert.Contains(
            twiceCorrected.CalibrationPoints,
            point => point.World == new WorldPoint(10, 0));
        Assert.Contains(
            twiceCorrected.CalibrationPoints,
            point => point.World == new WorldPoint(0, 10));
    }

    [Fact]
    public void ShouldRejectReplacementWhenThreeAnchorsWouldBecomeCollinear()
    {
        // Given: Three valid anchors whose first point is nearest to the map center.
        var profile = CreateProfile();

        // When: Replacing it would put all three anchors on one line.
        var applied = MapProfileCorrection.TryApply(
            profile,
            new WorldPoint(5, 5),
            new PixelPoint(500, 500),
            out var correctedProfile);

        // Then: The original calibration is preserved.
        Assert.False(applied);
        Assert.Same(profile, correctedProfile);
    }

    [Fact]
    public void ShouldPreserveImageRotationWhenCalibrationPointIsCorrected()
    {
        // Given: A rotated map profile.
        var profile = CreateProfile(imageRotationQuarterTurns: 3);

        // When: One of its calibration points is corrected.
        var applied = MapProfileCorrection.TryApply(
            profile,
            new WorldPoint(1, 1),
            new PixelPoint(120, 80),
            out var correctedProfile);

        // Then: The per-profile display rotation remains unchanged.
        Assert.True(applied);
        Assert.Equal(3, correctedProfile.ImageRotationQuarterTurns);
    }

    private static MapProfile CreateProfile(int imageRotationQuarterTurns = 0) => new(
        "Woods",
        @"C:\Maps\woods.png",
        7000,
        6000,
        "image-hash",
        [
            new CalibrationPoint(new WorldPoint(0, 0), new PixelPoint(0, 0)),
            new CalibrationPoint(new WorldPoint(10, 0), new PixelPoint(1000, 0)),
            new CalibrationPoint(new WorldPoint(0, 10), new PixelPoint(0, 1000)),
        ],
        new AffineTransform2D(100, 0, 0, 100, 0, 0),
        imageRotationQuarterTurns);

    private static void AssertPoint(PixelPoint expected, PixelPoint actual)
    {
        Assert.Equal(expected.X, actual.X, Precision);
        Assert.Equal(expected.Y, actual.Y, Precision);
    }
}
