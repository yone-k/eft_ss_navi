using EftSsNavi.Core.Calibration;
using EftSsNavi.Core.Viewport;

namespace EftSsNavi.App.Controls;

public static class MapContentViewportFitter
{
    private const int SearchIterations = 80;
    private const double ConstraintMargin = 1e-9;

    public static MapContentFitResult Fit(
        Size2D imageSize,
        Size2D viewSize,
        IReadOnlyList<AnchoredVisualBounds> visuals,
        double maximumOverlayScale,
        double padding)
    {
        ArgumentNullException.ThrowIfNull(visuals);
        EnsurePositiveFinite(maximumOverlayScale, nameof(maximumOverlayScale));
        if (!double.IsFinite(padding) || padding < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(padding));
        }

        if (visuals.Count == 0)
        {
            return new MapContentFitResult(
                ViewportTransform.Fit(imageSize, viewSize),
                maximumOverlayScale);
        }

        var availableWidth = viewSize.Width - (padding * 2);
        var availableHeight = viewSize.Height - (padding * 2);
        if (availableWidth <= 0 || availableHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewSize));
        }

        var overlayScale = FindOverlayScale(
            imageSize,
            visuals,
            maximumOverlayScale,
            availableWidth,
            availableHeight);
        var mapScale = FindMapScale(
            imageSize,
            visuals,
            overlayScale,
            availableWidth,
            availableHeight);
        if (mapScale <= 0)
        {
            overlayScale *= 1 - ConstraintMargin;
            mapScale = FindMapScale(
                imageSize,
                visuals,
                overlayScale,
                availableWidth,
                availableHeight);
        }

        EnsurePositiveFinite(mapScale, nameof(viewSize));
        var bounds = CalculateBounds(imageSize, visuals, mapScale, overlayScale);
        var offsetX = padding + ((availableWidth - bounds.Width) / 2) - bounds.Left;
        var offsetY = padding + ((availableHeight - bounds.Height) / 2) - bounds.Top;
        return new MapContentFitResult(
            new ViewportTransform(mapScale, offsetX, offsetY),
            overlayScale);
    }

    private static double FindOverlayScale(
        Size2D imageSize,
        IReadOnlyList<AnchoredVisualBounds> visuals,
        double maximumScale,
        double availableWidth,
        double availableHeight)
    {
        if (Fits(imageSize, visuals, 0, maximumScale, availableWidth, availableHeight))
        {
            return maximumScale;
        }

        var low = 0d;
        var high = maximumScale;
        for (var iteration = 0; iteration < SearchIterations; iteration++)
        {
            var candidate = (low + high) / 2;
            if (Fits(imageSize, visuals, 0, candidate, availableWidth, availableHeight))
            {
                low = candidate;
            }
            else
            {
                high = candidate;
            }
        }

        return low * (1 - ConstraintMargin);
    }

    private static double FindMapScale(
        Size2D imageSize,
        IReadOnlyList<AnchoredVisualBounds> visuals,
        double overlayScale,
        double availableWidth,
        double availableHeight)
    {
        var low = 0d;
        var high = Math.Min(
            availableWidth / imageSize.Width,
            availableHeight / imageSize.Height);
        for (var iteration = 0; iteration < SearchIterations; iteration++)
        {
            var candidate = (low + high) / 2;
            if (Fits(imageSize, visuals, candidate, overlayScale, availableWidth, availableHeight))
            {
                low = candidate;
            }
            else
            {
                high = candidate;
            }
        }

        return low;
    }

    private static bool Fits(
        Size2D imageSize,
        IReadOnlyList<AnchoredVisualBounds> visuals,
        double mapScale,
        double overlayScale,
        double availableWidth,
        double availableHeight)
    {
        var bounds = CalculateBounds(imageSize, visuals, mapScale, overlayScale);
        return bounds.Width <= availableWidth + ConstraintMargin
            && bounds.Height <= availableHeight + ConstraintMargin;
    }

    private static ContentBounds CalculateBounds(
        Size2D imageSize,
        IReadOnlyList<AnchoredVisualBounds> visuals,
        double mapScale,
        double overlayScale)
    {
        var left = 0d;
        var top = 0d;
        var right = imageSize.Width * mapScale;
        var bottom = imageSize.Height * mapScale;
        foreach (var visual in visuals)
        {
            var anchorX = visual.Anchor.X * mapScale;
            var anchorY = visual.Anchor.Y * mapScale;
            left = Math.Min(left, anchorX + (visual.Left * overlayScale));
            top = Math.Min(top, anchorY + (visual.Top * overlayScale));
            right = Math.Max(right, anchorX + (visual.Right * overlayScale));
            bottom = Math.Max(bottom, anchorY + (visual.Bottom * overlayScale));
        }

        return new ContentBounds(left, top, right, bottom);
    }

    private static void EnsurePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private readonly record struct ContentBounds(double Left, double Top, double Right, double Bottom)
    {
        public double Width => Right - Left;

        public double Height => Bottom - Top;
    }
}

public readonly record struct AnchoredVisualBounds
{
    public AnchoredVisualBounds(
        PixelPoint anchor,
        double left,
        double top,
        double right,
        double bottom)
    {
        if (!double.IsFinite(anchor.X)
            || !double.IsFinite(anchor.Y)
            || !double.IsFinite(left)
            || !double.IsFinite(top)
            || !double.IsFinite(right)
            || !double.IsFinite(bottom)
            || left > right
            || top > bottom)
        {
            throw new ArgumentOutOfRangeException(nameof(anchor));
        }

        Anchor = anchor;
        Left = left;
        Top = top;
        Right = right;
        Bottom = bottom;
    }

    public PixelPoint Anchor { get; }

    public double Left { get; }

    public double Top { get; }

    public double Right { get; }

    public double Bottom { get; }
}

public sealed record MapContentFitResult(
    ViewportTransform Viewport,
    double OverlayScale);
