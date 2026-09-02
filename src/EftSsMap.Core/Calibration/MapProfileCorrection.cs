namespace EftSsMap.Core.Calibration;

/// <summary>
/// Adds a user-confirmed correspondence to an existing map profile and refits it.
/// </summary>
public static class MapProfileCorrection
{
    public static bool TryFindNearestCalibrationPointIndex(
        MapProfile profile,
        WorldPoint worldPosition,
        out int index)
    {
        ArgumentNullException.ThrowIfNull(profile);
        index = -1;
        if (!worldPosition.IsFinite || profile.CalibrationPoints.Count < 3)
        {
            return false;
        }

        var nearestDistanceSquared = double.PositiveInfinity;
        for (var candidateIndex = 0; candidateIndex < 3; candidateIndex++)
        {
            var distanceSquared = DistanceSquared(
                profile.CalibrationPoints[candidateIndex].World,
                worldPosition);
            if (double.IsFinite(distanceSquared) && distanceSquared < nearestDistanceSquared)
            {
                index = candidateIndex;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return index >= 0;
    }

    public static bool TryApply(
        MapProfile profile,
        WorldPoint worldPosition,
        PixelPoint correctedPixel,
        out MapProfile correctedProfile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        correctedProfile = profile;

        if (!TryFindNearestCalibrationPointIndex(profile, worldPosition, out var nearestIndex))
        {
            return false;
        }

        return TryApply(
            profile,
            worldPosition,
            correctedPixel,
            nearestIndex,
            out correctedProfile);
    }

    public static bool TryApply(
        MapProfile profile,
        WorldPoint worldPosition,
        PixelPoint correctedPixel,
        int replacementIndex,
        out MapProfile correctedProfile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        correctedProfile = profile;
        if (!worldPosition.IsFinite
            || !correctedPixel.IsFinite
            || profile.CalibrationPoints.Count < 3
            || replacementIndex < 0
            || replacementIndex >= 3)
        {
            return false;
        }

        var points = profile.CalibrationPoints.Take(3).ToArray();
        points[replacementIndex] = new CalibrationPoint(worldPosition, correctedPixel);
        if (!AffineCalibration.TryCreate(points, out var transform))
        {
            return false;
        }

        correctedProfile = new MapProfile(
            profile.DisplayName,
            profile.ImagePath,
            profile.CalibratedImageWidth,
            profile.CalibratedImageHeight,
            profile.ImageSha256,
            points,
            transform,
            profile.ImageRotationQuarterTurns);
        return true;
    }

    private static double DistanceSquared(WorldPoint left, WorldPoint right)
    {
        var deltaX = left.X - right.X;
        var deltaZ = left.Z - right.Z;
        return (deltaX * deltaX) + (deltaZ * deltaZ);
    }
}
