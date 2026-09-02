using EftSsMap.Core.Calibration;

namespace EftSsMap.Core.Viewport;

/// <summary>
/// Rotates original image coordinates into a quarter-turned display space.
/// </summary>
public readonly record struct MapImageRotation
{
    public MapImageRotation(int quarterTurns)
    {
        QuarterTurns = ((quarterTurns % 4) + 4) % 4;
    }

    public int QuarterTurns { get; }

    public Size2D GetDisplaySize(Size2D imageSize)
    {
        ArgumentNullException.ThrowIfNull(imageSize);
        return QuarterTurns % 2 == 0
            ? imageSize
            : new Size2D(imageSize.Height, imageSize.Width);
    }

    public PixelPoint ImageToDisplay(PixelPoint imagePoint, Size2D imageSize)
    {
        EnsureFinite(imagePoint);
        ArgumentNullException.ThrowIfNull(imageSize);
        return QuarterTurns switch
        {
            0 => imagePoint,
            1 => new PixelPoint(imageSize.Height - imagePoint.Y, imagePoint.X),
            2 => new PixelPoint(
                imageSize.Width - imagePoint.X,
                imageSize.Height - imagePoint.Y),
            3 => new PixelPoint(imagePoint.Y, imageSize.Width - imagePoint.X),
            _ => throw new InvalidOperationException(),
        };
    }

    public PixelPoint DisplayToImage(PixelPoint displayPoint, Size2D imageSize)
    {
        EnsureFinite(displayPoint);
        ArgumentNullException.ThrowIfNull(imageSize);
        return QuarterTurns switch
        {
            0 => displayPoint,
            1 => new PixelPoint(displayPoint.Y, imageSize.Height - displayPoint.X),
            2 => new PixelPoint(
                imageSize.Width - displayPoint.X,
                imageSize.Height - displayPoint.Y),
            3 => new PixelPoint(imageSize.Width - displayPoint.Y, displayPoint.X),
            _ => throw new InvalidOperationException(),
        };
    }

    public PixelPoint DirectionToDisplay(PixelPoint direction)
    {
        EnsureFinite(direction);
        return QuarterTurns switch
        {
            0 => direction,
            1 => new PixelPoint(-direction.Y, direction.X),
            2 => new PixelPoint(-direction.X, -direction.Y),
            3 => new PixelPoint(direction.Y, -direction.X),
            _ => throw new InvalidOperationException(),
        };
    }

    private static void EnsureFinite(PixelPoint point)
    {
        if (!point.IsFinite)
        {
            throw new ArgumentOutOfRangeException(nameof(point));
        }
    }
}
