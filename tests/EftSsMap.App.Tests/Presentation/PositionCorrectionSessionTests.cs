using EftSsMap.App.Presentation;
using EftSsMap.Core.Calibration;

namespace EftSsMap.App.Tests.Presentation;

public sealed class PositionCorrectionSessionTests
{
    [Fact]
    public void ShouldKeepDroppedCorrectionPendingUntilConfirmed()
    {
        // Given: A correction session for the currently displayed position.
        var original = CreateProfile();
        Assert.True(PositionCorrectionSession.TryCreate(
            original,
            new WorldPoint(1, 1),
            out var session));

        // When: The marker is dropped at its corrected image position.
        var previewed = session.TryPreview(new PixelPoint(120, 80));

        // Then: A corrected profile is pending but the original remains the active source.
        Assert.True(previewed);
        Assert.Same(original, session.OriginalProfile);
        Assert.NotNull(session.PendingProfile);
        Assert.NotSame(original, session.PendingProfile);
        Assert.Equal(3, original.CalibrationPoints.Count);
    }

    [Fact]
    public void ShouldReturnPendingProfileOnlyWhenCorrectionIsConfirmed()
    {
        // Given: A valid dropped correction preview.
        Assert.True(PositionCorrectionSession.TryCreate(
            CreateProfile(),
            new WorldPoint(1, 1),
            out var session));
        Assert.True(session.TryPreview(new PixelPoint(120, 80)));
        var pending = session.PendingProfile;

        // When: The user confirms the correction.
        var confirmed = session.TryConfirm(out var correctedProfile);

        // Then: The pending profile is emitted once and cleared from the session.
        Assert.True(confirmed);
        Assert.Same(pending, correctedProfile);
        Assert.Null(session.PendingProfile);
        Assert.False(session.TryConfirm(out _));
    }

    [Fact]
    public void ShouldDiscardPendingProfileWhenCorrectionIsCanceled()
    {
        // Given: A valid dropped correction preview.
        Assert.True(PositionCorrectionSession.TryCreate(
            CreateProfile(),
            new WorldPoint(1, 1),
            out var session));
        Assert.True(session.TryPreview(new PixelPoint(120, 80)));

        // When: The user cancels the correction.
        session.Cancel();

        // Then: Nothing remains available to save.
        Assert.Null(session.PendingProfile);
        Assert.False(session.TryConfirm(out _));
    }

    [Fact]
    public void ShouldDiscardPreviewAndUseExplicitlySelectedReplacementAnchor()
    {
        // Given: A preview that currently replaces the default nearest anchor.
        var original = CreateProfile();
        Assert.True(PositionCorrectionSession.TryCreate(
            original,
            new WorldPoint(1, 1),
            out var session));
        Assert.Equal(0, session.ReplacementIndex);
        Assert.True(session.TryPreview(new PixelPoint(120, 80)));

        // When: The user selects the third calibration anchor instead.
        var selected = session.TrySelectReplacement(2);

        // Then: The stale preview is discarded and the next drop replaces that anchor.
        Assert.True(selected);
        Assert.Equal(2, session.ReplacementIndex);
        Assert.Null(session.PendingProfile);
        Assert.True(session.TryPreview(new PixelPoint(120, 80)));
        Assert.Contains(original.CalibrationPoints[0], session.PendingProfile!.CalibrationPoints);
        Assert.DoesNotContain(original.CalibrationPoints[2], session.PendingProfile.CalibrationPoints);
    }

    private static MapProfile CreateProfile() => new(
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
        new AffineTransform2D(100, 0, 0, 100, 0, 0));
}
