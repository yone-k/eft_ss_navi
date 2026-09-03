using EftSsNavi.Core.Calibration;

namespace EftSsNavi.Core.Viewport;

/// <summary>
/// Converts immutable image coordinates to their current on-screen representation.
/// </summary>
public sealed record ViewportTransform
{
    public ViewportTransform(double scale, double offsetX, double offsetY)
    {
        EnsurePositiveFinite(scale, nameof(scale));
        EnsureFinite(offsetX, nameof(offsetX));
        EnsureFinite(offsetY, nameof(offsetY));

        Scale = scale;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public double Scale { get; }

    public double OffsetX { get; }

    public double OffsetY { get; }

    public static ViewportTransform Fit(Size2D imageSize, Size2D viewSize)
    {
        var scale = Math.Min(
            viewSize.Width / imageSize.Width,
            viewSize.Height / imageSize.Height);
        var offsetX = (viewSize.Width - (imageSize.Width * scale)) / 2;
        var offsetY = (viewSize.Height - (imageSize.Height * scale)) / 2;
        return new ViewportTransform(scale, offsetX, offsetY);
    }

    public PixelPoint ImageToView(PixelPoint imagePoint)
    {
        EnsureFinite(imagePoint);
        return new PixelPoint(
            (imagePoint.X * Scale) + OffsetX,
            (imagePoint.Y * Scale) + OffsetY);
    }

    public PixelPoint ViewToImage(PixelPoint viewPoint)
    {
        EnsureFinite(viewPoint);
        return new PixelPoint(
            (viewPoint.X - OffsetX) / Scale,
            (viewPoint.Y - OffsetY) / Scale);
    }

    public ViewportTransform Pan(double deltaX, double deltaY)
    {
        EnsureFinite(deltaX, nameof(deltaX));
        EnsureFinite(deltaY, nameof(deltaY));
        return new ViewportTransform(Scale, OffsetX + deltaX, OffsetY + deltaY);
    }

    public ViewportTransform ZoomAt(
        PixelPoint pointer,
        double factor,
        double minimumScale,
        double maximumScale)
    {
        EnsureFinite(pointer);
        EnsurePositiveFinite(factor, nameof(factor));
        EnsurePositiveFinite(minimumScale, nameof(minimumScale));
        EnsurePositiveFinite(maximumScale, nameof(maximumScale));
        if (minimumScale > maximumScale)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumScale));
        }

        var requestedScale = Scale * factor;
        var newScale = Math.Clamp(requestedScale, minimumScale, maximumScale);
        var scaleRatio = newScale / Scale;
        var newOffsetX = pointer.X - ((pointer.X - OffsetX) * scaleRatio);
        var newOffsetY = pointer.Y - ((pointer.Y - OffsetY) * scaleRatio);
        return new ViewportTransform(newScale, newOffsetX, newOffsetY);
    }

    private static void EnsureFinite(PixelPoint point)
    {
        if (!point.IsFinite)
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }
    }

    private static void EnsureFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static void EnsurePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
