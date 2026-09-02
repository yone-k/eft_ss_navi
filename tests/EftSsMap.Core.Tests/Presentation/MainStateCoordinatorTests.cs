using System.Numerics;
using EftSsMap.Core.Calibration;
using EftSsMap.Core.Images;
using EftSsMap.Core.Observations;
using EftSsMap.Core.Presentation;

namespace EftSsMap.Core.Tests.Presentation;

public sealed class MainStateCoordinatorTests
{
    private const double Precision = 1e-10;
    private const string ImageHash = "0123456789abcdef";

    [Fact]
    public void ShouldStartWithoutAStoredPosition()
    {
        // Given: A newly started main-state coordinator.

        // When: Its initial state is read.
        var state = new MainStateCoordinator().State;

        // Then: No past observation is restored.
        Assert.Null(state.MarkerPosition);
        Assert.Null(state.MarkerDirection);
        Assert.Null(state.WorldPosition);
        Assert.Null(state.FileName);
        Assert.Equal(MainViewStatus.WaitingForObservation, state.Status);
    }

    [Fact]
    public void ShouldPopulateMarkerFileNameAndWorldPositionForValidObservation()
    {
        // Given: A selected profile whose image and calibration are valid.
        var coordinator = CreateCoordinatorWithSelectedProfile(
            CreateProfile("Woods", new AffineTransform2D(2, 0, 0, 3, 10, 20)));
        var observation = CreateObservation(new Vector3(4, 5, 6), new Vector2(1, 0));

        // When: A valid screenshot observation is processed.
        coordinator.ProcessObservation(observation, "eft-observation.png");

        // Then: The marker and user-facing observation fields are populated.
        AssertPoint(new PixelPoint(18, 38), coordinator.State.MarkerPosition);
        Assert.Equal(new Vector3(4, 5, 6), coordinator.State.WorldPosition);
        Assert.Equal("eft-observation.png", coordinator.State.FileName);
        Assert.Equal(MainViewStatus.PositionAvailable, coordinator.State.Status);
    }

    [Fact]
    public void ShouldKeepPointAndOmitDirectionWhenObservationDirectionIsUnavailable()
    {
        // Given: A valid selected profile and an observation without horizontal direction.
        var coordinator = CreateCoordinatorWithSelectedProfile(CreateProfile("Woods"));
        var observation = CreateObservation(new Vector3(4, 5, 6), null);

        // When: The observation is processed.
        coordinator.ProcessObservation(observation, "no-direction.png");

        // Then: Position remains available but no direction marker is produced.
        Assert.NotNull(coordinator.State.MarkerPosition);
        Assert.Null(coordinator.State.MarkerDirection);
        Assert.Equal(MainViewStatus.PositionAvailable, coordinator.State.Status);
    }

    [Fact]
    public void ShouldNotProjectObservationWhenNoProfileIsSelected()
    {
        // Given: A coordinator without a selected profile.
        var coordinator = new MainStateCoordinator();

        // When: An otherwise valid observation is processed.
        coordinator.ProcessObservation(CreateObservation(new Vector3(4, 5, 6), null), "unselected.png");

        // Then: Projection is prohibited.
        Assert.Null(coordinator.State.MarkerPosition);
        Assert.Null(coordinator.State.WorldPosition);
        Assert.Null(coordinator.State.FileName);
        Assert.Equal(MainViewStatus.ProfileNotSelected, coordinator.State.Status);
    }

    [Fact]
    public void ShouldNotProjectObservationWhenCalibrationIsInvalid()
    {
        // Given: A selected profile with invalid calibration state.
        var profile = CreateProfile("Woods");
        var coordinator = new MainStateCoordinator();
        coordinator.SelectProfile(profile, FingerprintFor(profile), calibrationValid: false);

        // When: An otherwise valid observation is processed.
        coordinator.ProcessObservation(CreateObservation(new Vector3(4, 5, 6), null), "invalid-calibration.png");

        // Then: Projection is prohibited with a calibration-specific status.
        Assert.Null(coordinator.State.MarkerPosition);
        Assert.Null(coordinator.State.WorldPosition);
        Assert.Equal(MainViewStatus.CalibrationError, coordinator.State.Status);
    }

    [Fact]
    public void ShouldNotProjectObservationWhenSelectedImageFingerprintIsInvalid()
    {
        // Given: A profile selected against image content different from its calibrated image.
        var profile = CreateProfile("Woods");
        var changedImage = FingerprintFor(profile) with { Sha256 = "changed-content" };
        var coordinator = new MainStateCoordinator();
        coordinator.SelectProfile(profile, changedImage, calibrationValid: true);

        // When: An otherwise valid observation is processed.
        coordinator.ProcessObservation(CreateObservation(new Vector3(4, 5, 6), null), "changed-image.png");

        // Then: Projection is prohibited with an image-specific status.
        Assert.Null(coordinator.State.MarkerPosition);
        Assert.Null(coordinator.State.WorldPosition);
        Assert.Equal(MainViewStatus.ImageError, coordinator.State.Status);
    }

    [Fact]
    public void ShouldClearPositionImmediatelyWhenProfileChanges()
    {
        // Given: A displayed position for one selected profile.
        var coordinator = CreateCoordinatorWithSelectedProfile(CreateProfile("Woods"));
        coordinator.ProcessObservation(CreateObservation(new Vector3(4, 5, 6), null), "woods.png");

        // When: A different valid profile is selected.
        var customs = CreateProfile("Customs");
        coordinator.SelectProfile(customs, FingerprintFor(customs), calibrationValid: true);

        // Then: The old position and file name are cleared immediately.
        Assert.Null(coordinator.State.MarkerPosition);
        Assert.Null(coordinator.State.WorldPosition);
        Assert.Null(coordinator.State.FileName);
        Assert.Equal(MainViewStatus.WaitingForObservation, coordinator.State.Status);
    }

    [Fact]
    public void ShouldClearPositionImmediatelyWhenWatchDirectoryChanges()
    {
        // Given: A displayed position for the current watch directory.
        var coordinator = CreateCoordinatorWithSelectedProfile(CreateProfile("Woods"));
        coordinator.ProcessObservation(CreateObservation(new Vector3(4, 5, 6), null), "old-directory.png");

        // When: The watched directory changes.
        coordinator.ChangeWatchDirectory(@"C:\Screenshots\New");

        // Then: The old position and file name are cleared immediately.
        Assert.Null(coordinator.State.MarkerPosition);
        Assert.Null(coordinator.State.WorldPosition);
        Assert.Null(coordinator.State.FileName);
        Assert.Equal(MainViewStatus.WaitingForObservation, coordinator.State.Status);
    }

    [Fact]
    public void ShouldClearSelectionAndPositionWhenSelectedProfileIsDeleted()
    {
        // Given: A displayed position for a selected profile.
        var coordinator = CreateCoordinatorWithSelectedProfile(CreateProfile("Woods"));
        coordinator.ProcessObservation(CreateObservation(new Vector3(4, 5, 6), null), "woods.png");

        // When: The selected profile is deleted using different name casing.
        coordinator.DeleteProfile("WOODS");

        // Then: The profile selection and its position are cleared.
        Assert.Null(coordinator.SelectedProfile);
        Assert.Null(coordinator.State.MarkerPosition);
        Assert.Null(coordinator.State.FileName);
        Assert.Equal(MainViewStatus.ProfileNotSelected, coordinator.State.Status);
    }

    [Fact]
    public void ShouldPreserveDisplayedPositionWhenUnselectedProfileIsDeleted()
    {
        // Given: A displayed position for the selected profile.
        var coordinator = CreateCoordinatorWithSelectedProfile(CreateProfile("Woods"));
        coordinator.ProcessObservation(CreateObservation(new Vector3(4, 5, 6), null), "woods.png");
        var before = coordinator.State;

        // When: A different profile is deleted.
        coordinator.DeleteProfile("Customs");

        // Then: The selected profile's displayed state is preserved.
        Assert.Equal(before, coordinator.State);
        Assert.Equal("Woods", coordinator.SelectedProfile?.DisplayName);
    }

    [Fact]
    public void ShouldRemainClearUntilNextValidObservationAfterWatchDirectoryChanges()
    {
        // Given: A coordinator cleared by a watch-directory change.
        var coordinator = CreateCoordinatorWithSelectedProfile(CreateProfile("Woods"));
        coordinator.ProcessObservation(CreateObservation(new Vector3(1, 2, 3), null), "old.png");
        coordinator.ChangeWatchDirectory(@"C:\Screenshots\New");

        // When: No new observation has arrived.
        var clearedState = coordinator.State;

        // Then: No old observation is restored, while the next valid observation can populate state.
        Assert.Null(clearedState.MarkerPosition);
        Assert.Null(clearedState.FileName);
        coordinator.ProcessObservation(CreateObservation(new Vector3(7, 8, 9), null), "new.png");
        Assert.NotNull(coordinator.State.MarkerPosition);
        Assert.Equal("new.png", coordinator.State.FileName);
    }

    [Fact]
    public void ShouldIgnoreObservationCapturedBeforeWatchDirectoryChanges()
    {
        // Given: An observation callback captured the current epoch before the watch directory changed.
        var coordinator = CreateCoordinatorWithSelectedProfile(CreateProfile("Woods"));
        var capturedEpoch = coordinator.Epoch;
        coordinator.ChangeWatchDirectory(@"C:\Screenshots\New");

        // When: The delayed callback runs after the transition.
        coordinator.ProcessObservation(
            CreateObservation(new Vector3(7, 8, 9), null),
            "old-directory.png",
            capturedEpoch);

        // Then: The old observation cannot restore the cleared state.
        Assert.Null(coordinator.State.MarkerPosition);
        Assert.Null(coordinator.State.FileName);
        Assert.Equal(MainViewStatus.WaitingForObservation, coordinator.State.Status);
    }

    [Fact]
    public void ShouldIgnoreObservationCapturedBeforeProfileChanges()
    {
        // Given: An observation callback captured the current epoch for the old profile.
        var coordinator = CreateCoordinatorWithSelectedProfile(CreateProfile("Woods"));
        var capturedEpoch = coordinator.Epoch;
        var customs = CreateProfile("Customs");
        coordinator.SelectProfile(customs, FingerprintFor(customs), calibrationValid: true);

        // When: The delayed callback runs after the profile transition.
        coordinator.ProcessObservation(
            CreateObservation(new Vector3(7, 8, 9), null),
            "woods.png",
            capturedEpoch);

        // Then: The old observation is not projected on the newly selected profile.
        Assert.Null(coordinator.State.MarkerPosition);
        Assert.Null(coordinator.State.FileName);
        Assert.Equal(MainViewStatus.WaitingForObservation, coordinator.State.Status);
    }

    [Fact]
    public void ShouldApplyOnlyAffineLinearComponentToDirection()
    {
        // Given: A profile transform with scale, rotation, and large translation.
        var transform = new AffineTransform2D(0, -2, 3, 0, 1000, -2000);
        var coordinator = CreateCoordinatorWithSelectedProfile(CreateProfile("Woods", transform));

        // When: An observation with horizontal direction is processed.
        coordinator.ProcessObservation(
            CreateObservation(new Vector3(4, 5, 6), new Vector2(2, 5)),
            "direction.png");

        // Then: The direction excludes translation.
        AssertPoint(new PixelPoint(-10, 6), coordinator.State.MarkerDirection);
    }

    [Fact]
    public void ShouldReprojectDisplayedPositionWhenSelectedProfileIsCorrected()
    {
        // Given: A displayed position for the selected profile.
        var coordinator = CreateCoordinatorWithSelectedProfile(CreateProfile("Woods"));
        coordinator.ProcessObservation(
            CreateObservation(new Vector3(4, 5, 6), null),
            "correction.png");
        var correctedProfile = CreateProfile(
            "Woods",
            new AffineTransform2D(2, 0, 0, 3, 10, 20));

        // When: The selected profile is replaced with its corrected calibration.
        var updated = coordinator.TryUpdateSelectedProfile(correctedProfile);

        // Then: The same world position and file remain visible at the corrected pixel.
        Assert.True(updated);
        Assert.Same(correctedProfile, coordinator.SelectedProfile);
        AssertPoint(new PixelPoint(18, 38), coordinator.State.MarkerPosition);
        Assert.Equal(new Vector3(4, 5, 6), coordinator.State.WorldPosition);
        Assert.Equal("correction.png", coordinator.State.FileName);
        Assert.Equal(MainViewStatus.PositionAvailable, coordinator.State.Status);
    }

    public static TheoryData<MainFailureKind, MainViewStatus> FailureStatuses => new()
    {
        { MainFailureKind.Parsing, MainViewStatus.ParseError },
        { MainFailureKind.Settings, MainViewStatus.SettingsError },
        { MainFailureKind.Image, MainViewStatus.ImageError },
        { MainFailureKind.Calibration, MainViewStatus.CalibrationError },
    };

    [Theory]
    [MemberData(nameof(FailureStatuses))]
    public void ShouldContainFailureAndExposeCauseSpecificStatus(
        MainFailureKind failureKind,
        MainViewStatus expectedStatus)
    {
        // Given: A coordinator operation that throws for a known failure source.
        var coordinator = CreateCoordinatorWithSelectedProfile(CreateProfile("Woods"));

        // When: The throwing operation is executed through the coordinator boundary.
        var exception = Record.Exception(
            () => coordinator.ExecuteSafely(failureKind, () => throw new InvalidOperationException("failure")));

        // Then: No exception escapes and the status identifies the failure source.
        Assert.Null(exception);
        Assert.Equal(expectedStatus, coordinator.State.Status);
    }

    private static MainStateCoordinator CreateCoordinatorWithSelectedProfile(MapProfile profile)
    {
        var coordinator = new MainStateCoordinator();
        coordinator.SelectProfile(profile, FingerprintFor(profile), calibrationValid: true);
        return coordinator;
    }

    private static MapProfile CreateProfile(
        string displayName,
        AffineTransform2D? transform = null) =>
        new(
            displayName,
            @"C:\Maps\map.png",
            7000,
            6000,
            ImageHash,
            [
                new CalibrationPoint(new WorldPoint(0, 0), new PixelPoint(0, 0)),
                new CalibrationPoint(new WorldPoint(1, 0), new PixelPoint(1, 0)),
                new CalibrationPoint(new WorldPoint(0, 1), new PixelPoint(0, 1)),
            ],
            transform ?? new AffineTransform2D(1, 0, 0, 1, 0, 0));

    private static ImageFingerprint FingerprintFor(MapProfile profile) =>
        new(
            profile.ImagePath,
            profile.CalibratedImageWidth,
            profile.CalibratedImageHeight,
            profile.ImageSha256);

    private static PositionObservation CreateObservation(Vector3 position, Vector2? direction) =>
        new(
            new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Local),
            position,
            Quaternion.Identity,
            "12-00-00",
            null,
            direction);

    private static void AssertPoint(PixelPoint expected, PixelPoint? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.X, actual.Value.X, Precision);
        Assert.Equal(expected.Y, actual.Value.Y, Precision);
    }
}
