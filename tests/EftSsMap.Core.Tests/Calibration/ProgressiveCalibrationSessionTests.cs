using EftSsMap.Core.Calibration;
using EftSsMap.Core.Images;

namespace EftSsMap.Core.Tests.Calibration;

public sealed class ProgressiveCalibrationSessionTests
{
    [Fact]
    public void ShouldAddAnchorWhenDetectedPositionIsPlacedOnMap()
    {
        // Given: An uncalibrated profile with one detected position waiting for placement.
        var session = CreateSession();
        var world = new WorldPoint(10, 20);
        Assert.True(session.TryStage(world));

        // When: The user clicks the matching map position.
        var result = session.Place(new PixelPoint(100, 200));

        // Then: The point is saved and the session waits for another screenshot.
        Assert.Equal(ProgressiveCalibrationPlacement.AnchorAdded, result);
        Assert.Equal(
            new CalibrationPoint(world, new PixelPoint(100, 200)),
            Assert.Single(session.Profile.CalibrationPoints));
        Assert.Null(session.PendingWorldPoint);
    }

    [Fact]
    public void ShouldKeepFirstDetectedPositionUntilItIsPlaced()
    {
        // Given: A detected position is already waiting for placement.
        var session = CreateSession();
        var first = new WorldPoint(10, 20);
        Assert.True(session.TryStage(first));

        // When: Another screenshot is detected before the user clicks the map.
        var staged = session.TryStage(new WorldPoint(30, 40));

        // Then: The position shown to the user does not silently change.
        Assert.False(staged);
        Assert.Equal(first, session.PendingWorldPoint);
    }

    [Fact]
    public void ShouldCompleteCalibrationAfterThreeValidPlacements()
    {
        // Given: Two separated calibration anchors already exist.
        var session = CreateSession();
        AddAnchor(session, new WorldPoint(0, 0), new PixelPoint(10, 20));
        AddAnchor(session, new WorldPoint(100, 0), new PixelPoint(210, 20));
        Assert.True(session.TryStage(new WorldPoint(0, 100)));

        // When: The third non-collinear anchor is placed.
        var result = session.Place(new PixelPoint(10, 320));

        // Then: A usable affine calibration is stored in the same profile.
        Assert.Equal(ProgressiveCalibrationPlacement.Completed, result);
        Assert.Equal(3, session.Profile.CalibrationPoints.Count);
        Assert.Equal(
            new PixelPoint(110, 170),
            session.Profile.Transform.TransformPosition(new WorldPoint(50, 50)));
    }

    [Fact]
    public void ShouldRejectInvalidThirdPlacementWithoutDiscardingExistingAnchors()
    {
        // Given: Two valid anchors and a third world position on the same line.
        var session = CreateSession();
        AddAnchor(session, new WorldPoint(0, 0), new PixelPoint(10, 20));
        AddAnchor(session, new WorldPoint(100, 0), new PixelPoint(210, 20));
        Assert.True(session.TryStage(new WorldPoint(200, 0)));

        // When: The invalid third point is placed.
        var result = session.Place(new PixelPoint(410, 20));

        // Then: Only the invalid candidate is rejected so another screenshot can be used.
        Assert.Equal(ProgressiveCalibrationPlacement.InvalidAnchor, result);
        Assert.Equal(2, session.Profile.CalibrationPoints.Count);
        Assert.Null(session.PendingWorldPoint);
    }

    [Fact]
    public void ShouldRejectDuplicatePlacementBeforeCalibrationIsComplete()
    {
        // Given: One anchor already exists and another screenshot reports the same world position.
        var session = CreateSession();
        AddAnchor(session, new WorldPoint(10, 20), new PixelPoint(100, 200));
        Assert.True(session.TryStage(new WorldPoint(10, 20)));

        // When: The duplicate is placed elsewhere on the map.
        var result = session.Place(new PixelPoint(300, 400));

        // Then: It is rejected without increasing calibration progress.
        Assert.Equal(ProgressiveCalibrationPlacement.InvalidAnchor, result);
        Assert.Single(session.Profile.CalibrationPoints);
    }

    [Fact]
    public void ShouldPreserveImageRotationWhileCalibrationPointsAreAdded()
    {
        // Given: An uncalibrated profile displayed at 90 degrees clockwise.
        var session = CreateSession(imageRotationQuarterTurns: 1);

        // When: A detected location is placed on the map.
        AddAnchor(session, new WorldPoint(10, 20), new PixelPoint(100, 200));

        // Then: Saving partial calibration does not reset its display orientation.
        Assert.Equal(1, session.Profile.ImageRotationQuarterTurns);
    }

    [Fact]
    public void ShouldPreservePendingDetectedPositionWhenMapIsRotated()
    {
        // Given: A detected position is waiting for the user to click the map.
        var session = CreateSession();
        var pending = new WorldPoint(10, 20);
        Assert.True(session.TryStage(pending));

        // When: The profile display is rotated before the click.
        session.SetImageRotationQuarterTurns(3);

        // Then: Rotation is stored without discarding the pending calibration position.
        Assert.Equal(3, session.Profile.ImageRotationQuarterTurns);
        Assert.Equal(pending, session.PendingWorldPoint);
    }

    private static ProgressiveCalibrationSession CreateSession(int imageRotationQuarterTurns = 0)
    {
        var profile = MapProfile.CreateUncalibrated(
            "Interchange",
            new ImageFingerprint(@"C:\Maps\interchange.png", 4096, 3440, "hash"))
            .WithImageRotationQuarterTurns(imageRotationQuarterTurns);
        return new ProgressiveCalibrationSession(profile);
    }

    private static void AddAnchor(
        ProgressiveCalibrationSession session,
        WorldPoint world,
        PixelPoint pixel)
    {
        Assert.True(session.TryStage(world));
        Assert.Equal(ProgressiveCalibrationPlacement.AnchorAdded, session.Place(pixel));
    }
}
