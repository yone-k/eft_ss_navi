namespace EftSsNavi.Core.Viewport;

public sealed record Size2D
{
    public Size2D(double width, double height)
    {
        if (!double.IsFinite(width) || width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (!double.IsFinite(height) || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }
}
