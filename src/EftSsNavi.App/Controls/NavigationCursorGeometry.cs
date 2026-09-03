using EftSsNavi.Core.Calibration;

namespace EftSsNavi.App.Controls;

public static class NavigationCursorGeometry
{
    public const float OutlineStrokeWidth = 1.2f;

    private const double TipDistance = 14;
    private const double RearDistance = 7;
    private const double HalfWidth = 6.5;
    private const double NotchDistance = -1.75;

    public static NavigationCursorPoints Create(PixelPoint center, PixelPoint? direction)
    {
        var directionX = direction?.X ?? 0;
        var directionY = direction?.Y ?? -1;
        var length = Math.Sqrt((directionX * directionX) + (directionY * directionY));
        if (!double.IsFinite(length) || length <= double.Epsilon)
        {
            directionX = 0;
            directionY = -1;
            length = 1;
        }

        directionX /= length;
        directionY /= length;
        var perpendicularX = -directionY;
        var perpendicularY = directionX;

        return new NavigationCursorPoints(
            PointAlong(center, directionX, directionY, TipDistance),
            OffsetRear(center, directionX, directionY, perpendicularX, perpendicularY, HalfWidth),
            PointAlong(center, directionX, directionY, NotchDistance),
            OffsetRear(center, directionX, directionY, perpendicularX, perpendicularY, -HalfWidth));
    }

    private static PixelPoint PointAlong(
        PixelPoint center,
        double directionX,
        double directionY,
        double distance) => new(
            center.X + (directionX * distance),
            center.Y + (directionY * distance));

    private static PixelPoint OffsetRear(
        PixelPoint center,
        double directionX,
        double directionY,
        double perpendicularX,
        double perpendicularY,
        double perpendicularDistance) => new(
            center.X - (directionX * RearDistance) + (perpendicularX * perpendicularDistance),
            center.Y - (directionY * RearDistance) + (perpendicularY * perpendicularDistance));
}

public readonly record struct NavigationCursorPoints(
    PixelPoint Tip,
    PixelPoint RearLeft,
    PixelPoint Notch,
    PixelPoint RearRight);
