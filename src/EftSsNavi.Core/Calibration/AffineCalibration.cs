namespace EftSsNavi.Core.Calibration;

/// <summary>
/// Creates an affine transform from exactly three point correspondences.
/// </summary>
public static class AffineCalibration
{
    public const double MinimumAbsoluteLinearDeterminant = 1e-9;

    public static bool TryCreate(
        IReadOnlyList<CalibrationPoint>? points,
        out AffineTransform2D transform)
    {
        transform = default;

        if (points is null || points.Count != 3 || !AllCoordinatesAreFinite(points))
        {
            return false;
        }

        var first = points[0];
        var second = points[1];
        var third = points[2];

        if (ContainsDuplicateWorldPoint(first, second, third) ||
            ContainsDuplicatePixelPoint(first, second, third))
        {
            return false;
        }

        var worldX1 = second.World.X - first.World.X;
        var worldZ1 = second.World.Z - first.World.Z;
        var worldX2 = third.World.X - first.World.X;
        var worldZ2 = third.World.Z - first.World.Z;
        var worldDeterminant = (worldX1 * worldZ2) - (worldX2 * worldZ1);

        var pixelX1 = second.Pixel.X - first.Pixel.X;
        var pixelY1 = second.Pixel.Y - first.Pixel.Y;
        var pixelX2 = third.Pixel.X - first.Pixel.X;
        var pixelY2 = third.Pixel.Y - first.Pixel.Y;
        var pixelDeterminant = (pixelX1 * pixelY2) - (pixelX2 * pixelY1);

        if (!double.IsFinite(worldDeterminant) || worldDeterminant == 0 ||
            !double.IsFinite(pixelDeterminant) || pixelDeterminant == 0)
        {
            return false;
        }

        var m11 = ((pixelX1 * worldZ2) - (pixelX2 * worldZ1)) / worldDeterminant;
        var m12 = ((pixelX2 * worldX1) - (pixelX1 * worldX2)) / worldDeterminant;
        var m21 = ((pixelY1 * worldZ2) - (pixelY2 * worldZ1)) / worldDeterminant;
        var m22 = ((pixelY2 * worldX1) - (pixelY1 * worldX2)) / worldDeterminant;
        var translationX = first.Pixel.X - (m11 * first.World.X) - (m12 * first.World.Z);
        var translationY = first.Pixel.Y - (m21 * first.World.X) - (m22 * first.World.Z);

        var candidate = new AffineTransform2D(
            m11,
            m12,
            m21,
            m22,
            translationX,
            translationY);
        var linearDeterminant = (m11 * m22) - (m12 * m21);

        if (!candidate.IsFinite ||
            !double.IsFinite(linearDeterminant) ||
            Math.Abs(linearDeterminant) <= MinimumAbsoluteLinearDeterminant)
        {
            return false;
        }

        transform = candidate;
        return true;
    }

    private static bool AllCoordinatesAreFinite(IReadOnlyList<CalibrationPoint> points) =>
        points[0].World.IsFinite && points[0].Pixel.IsFinite &&
        points[1].World.IsFinite && points[1].Pixel.IsFinite &&
        points[2].World.IsFinite && points[2].Pixel.IsFinite;

    private static bool ContainsDuplicateWorldPoint(
        CalibrationPoint first,
        CalibrationPoint second,
        CalibrationPoint third) =>
        first.World == second.World || first.World == third.World || second.World == third.World;

    private static bool ContainsDuplicatePixelPoint(
        CalibrationPoint first,
        CalibrationPoint second,
        CalibrationPoint third) =>
        first.Pixel == second.Pixel || first.Pixel == third.Pixel || second.Pixel == third.Pixel;
}
