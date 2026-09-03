using EftSsNavi.Core.Calibration;

namespace EftSsNavi.Core.Settings;

/// <summary>
/// Converts EFT X/Z coordinates to pixels in a rasterized tarkov.dev SVG overlay.
/// </summary>
public sealed class TarkovDevMapProjection
{
    private readonly double _scaleX;
    private readonly double _marginX;
    private readonly double _scaleY;
    private readonly double _marginY;
    private readonly double _cosine;
    private readonly double _sine;
    private readonly WorldPoint _firstBound;
    private readonly WorldPoint _secondBound;

    public TarkovDevMapProjection(
        double scaleX,
        double marginX,
        double scaleY,
        double marginY,
        int rotationDegrees,
        WorldPoint firstBound,
        WorldPoint secondBound)
    {
        if (!double.IsFinite(scaleX) || scaleX == 0 ||
            !double.IsFinite(scaleY) || scaleY == 0 ||
            !double.IsFinite(marginX) || !double.IsFinite(marginY))
        {
            throw new ArgumentOutOfRangeException(nameof(scaleX));
        }

        if (rotationDegrees is not (0 or 90 or 180 or 270))
        {
            throw new ArgumentOutOfRangeException(nameof(rotationDegrees));
        }

        if (!firstBound.IsFinite || !secondBound.IsFinite ||
            firstBound.X == secondBound.X || firstBound.Z == secondBound.Z)
        {
            throw new ArgumentException("Map bounds must form a finite non-empty rectangle.");
        }

        _scaleX = scaleX;
        _marginX = marginX;
        _scaleY = scaleY;
        _marginY = marginY;
        var angle = rotationDegrees * Math.PI / 180d;
        _cosine = Math.Cos(angle);
        _sine = Math.Sin(angle);
        _firstBound = firstBound;
        _secondBound = secondBound;
    }

    public AffineTransform2D CreateTransform(int imageWidth, int imageHeight)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(imageWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(imageHeight, 1);

        var projectedBounds = RectangleCorners().Select(ProjectRaw).ToArray();
        var minimumX = projectedBounds.Min(point => point.X);
        var maximumX = projectedBounds.Max(point => point.X);
        var minimumY = projectedBounds.Min(point => point.Y);
        var maximumY = projectedBounds.Max(point => point.Y);
        var pixelScaleX = imageWidth / (maximumX - minimumX);
        var pixelScaleY = imageHeight / (maximumY - minimumY);

        return new AffineTransform2D(
            pixelScaleX * _scaleX * _cosine,
            pixelScaleX * _scaleX * -_sine,
            pixelScaleY * -_scaleY * _sine,
            pixelScaleY * -_scaleY * _cosine,
            pixelScaleX * (_marginX - minimumX),
            pixelScaleY * (_marginY - minimumY));
    }

    public IReadOnlyList<CalibrationPoint> CreateCalibrationPoints(int imageWidth, int imageHeight)
    {
        var transform = CreateTransform(imageWidth, imageHeight);
        WorldPoint[] worldPoints =
        [
            _firstBound,
            new WorldPoint(_secondBound.X, _firstBound.Z),
            new WorldPoint(_firstBound.X, _secondBound.Z),
        ];

        return worldPoints
            .Select(point => new CalibrationPoint(point, transform.TransformPosition(point)))
            .ToArray();
    }

    private IEnumerable<WorldPoint> RectangleCorners()
    {
        yield return _firstBound;
        yield return new WorldPoint(_secondBound.X, _firstBound.Z);
        yield return _secondBound;
        yield return new WorldPoint(_firstBound.X, _secondBound.Z);
    }

    private PixelPoint ProjectRaw(WorldPoint point)
    {
        var rotatedX = (point.X * _cosine) - (point.Z * _sine);
        var rotatedZ = (point.X * _sine) + (point.Z * _cosine);
        return new PixelPoint(
            (_scaleX * rotatedX) + _marginX,
            (-_scaleY * rotatedZ) + _marginY);
    }
}
