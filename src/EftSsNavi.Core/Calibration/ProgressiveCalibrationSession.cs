namespace EftSsNavi.Core.Calibration;

/// <summary>
/// Collects map anchors from positions detected by the screenshot monitor.
/// </summary>
public sealed class ProgressiveCalibrationSession
{
    public ProgressiveCalibrationSession(MapProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.CalibrationPoints.Count >= 3)
        {
            throw new ArgumentException("The profile is already calibrated.", nameof(profile));
        }

        Profile = profile;
    }

    public MapProfile Profile { get; private set; }

    public WorldPoint? PendingWorldPoint { get; private set; }

    public void SetImageRotationQuarterTurns(int imageRotationQuarterTurns) =>
        Profile = Profile.WithImageRotationQuarterTurns(imageRotationQuarterTurns);

    public bool TryStage(WorldPoint worldPoint)
    {
        if (!worldPoint.IsFinite || PendingWorldPoint is not null || Profile.CalibrationPoints.Count >= 3)
        {
            return false;
        }

        PendingWorldPoint = worldPoint;
        return true;
    }

    public ProgressiveCalibrationPlacement Place(PixelPoint pixelPoint)
    {
        if (PendingWorldPoint is not { } worldPoint)
        {
            return ProgressiveCalibrationPlacement.NoPendingPosition;
        }

        PendingWorldPoint = null;
        if (!pixelPoint.IsFinite || Profile.CalibrationPoints.Any(point =>
                point.World == worldPoint || point.Pixel == pixelPoint))
        {
            return ProgressiveCalibrationPlacement.InvalidAnchor;
        }

        var points = Profile.CalibrationPoints
            .Append(new CalibrationPoint(worldPoint, pixelPoint))
            .ToArray();
        if (points.Length < 3)
        {
            Profile = CopyWith(points, default);
            return ProgressiveCalibrationPlacement.AnchorAdded;
        }

        if (!AffineCalibration.TryCreate(points, out var transform))
        {
            return ProgressiveCalibrationPlacement.InvalidAnchor;
        }

        Profile = CopyWith(points, transform);
        return ProgressiveCalibrationPlacement.Completed;
    }

    private MapProfile CopyWith(
        IReadOnlyList<CalibrationPoint> calibrationPoints,
        AffineTransform2D transform) =>
        new(
            Profile.DisplayName,
            Profile.ImagePath,
            Profile.CalibratedImageWidth,
            Profile.CalibratedImageHeight,
            Profile.ImageSha256,
            calibrationPoints,
            transform,
            Profile.ImageRotationQuarterTurns);
}
