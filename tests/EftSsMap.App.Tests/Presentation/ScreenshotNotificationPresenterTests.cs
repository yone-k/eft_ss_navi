using System.Numerics;
using EftSsMap.App.Presentation;
using EftSsMap.Core.Calibration;
using EftSsMap.Core.Images;
using EftSsMap.Core.Observations;
using EftSsMap.Core.Presentation;

namespace EftSsMap.App.Tests.Presentation;

public sealed class ScreenshotNotificationPresenterTests
{
    [Fact]
    public void ShouldPreserveLastValidObservationWhenFileNameIsRejected()
    {
        var coordinator = CreateSelectedCoordinator();
        var presenter = new ScreenshotNotificationPresenter(coordinator);
        presenter.Accept(CreateObservation(), "valid.png", coordinator.Epoch);
        var stateBeforeRejection = coordinator.State;

        var message = presenter.RejectFileName("invalid.png");

        Assert.Same(stateBeforeRejection, coordinator.State);
        Assert.Contains("invalid.png", message, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldPreserveLastValidObservationWhenMonitoringFails()
    {
        var coordinator = CreateSelectedCoordinator();
        var presenter = new ScreenshotNotificationPresenter(coordinator);
        presenter.Accept(CreateObservation(), "valid.png", coordinator.Epoch);
        var stateBeforeFailure = coordinator.State;

        var message = presenter.MonitoringFailed(new IOException("watch failed"));

        Assert.Same(stateBeforeFailure, coordinator.State);
        Assert.Contains("watch failed", message, StringComparison.Ordinal);
    }

    private static MainStateCoordinator CreateSelectedCoordinator()
    {
        var profile = new MapProfile(
            "Woods",
            @"C:\Maps\woods.png",
            100,
            100,
            "hash",
            [
                new CalibrationPoint(new WorldPoint(0, 0), new PixelPoint(0, 0)),
                new CalibrationPoint(new WorldPoint(1, 0), new PixelPoint(1, 0)),
                new CalibrationPoint(new WorldPoint(0, 1), new PixelPoint(0, 1)),
            ],
            new AffineTransform2D(1, 0, 0, 1, 0, 0));
        var coordinator = new MainStateCoordinator();
        coordinator.SelectProfile(
            profile,
            new ImageFingerprint(profile.ImagePath, 100, 100, "hash"),
            calibrationValid: true);
        return coordinator;
    }

    private static PositionObservation CreateObservation() => new(
        new DateTime(2026, 9, 1, 12, 0, 0),
        new Vector3(1, 2, 3),
        Quaternion.Identity,
        "12.00",
        null,
        Vector2.UnitY);
}
