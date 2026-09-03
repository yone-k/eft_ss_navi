using EftSsNavi.Core.Viewport;

namespace EftSsNavi.App.Controls;

public static class MapOverlayLayout
{
    public const double ReferenceWidth = 1600;
    public const double ReferenceHeight = 900;
    public const double MinimumNormalScale = 0.75;
    public const double MaximumNormalScale = 1.75;
    public const double CursorBaselineScale = 1.1;

    public static double CalculateNormalScale(Size2D viewSize)
    {
        var scale = Math.Min(
            viewSize.Width / ReferenceWidth,
            viewSize.Height / ReferenceHeight);
        return Math.Clamp(scale, MinimumNormalScale, MaximumNormalScale);
    }

    public static MapMarkerMetrics CreateMarkerMetrics(double scale)
    {
        EnsurePositiveFinite(scale, nameof(scale));
        return new MapMarkerMetrics(
            scale,
            DarkOutlineWidth: 1.5 * scale,
            IconStrokeWidth: 1.6 * scale,
            LabelOutlineWidth: 3 * scale,
            LabelFontSize: 12 * scale,
            ExtractRadius: 7.5 * scale,
            SpawnRadius: 5 * scale,
            LabelOffsetX: 11 * scale,
            LabelBaselineOffsetY: 4 * scale);
    }

    public static double CalculateCursorScale(double displayScale)
    {
        EnsurePositiveFinite(displayScale, nameof(displayScale));
        return CursorBaselineScale * displayScale;
    }

    public static double CalculateCursorHitRadius(double displayScale) =>
        Math.Max(
            MarkerDragInteraction.HitRadius,
            MarkerDragInteraction.HitRadius * CalculateCursorScale(displayScale));

    private static void EnsurePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

public readonly record struct MapMarkerMetrics(
    double Scale,
    double DarkOutlineWidth,
    double IconStrokeWidth,
    double LabelOutlineWidth,
    double LabelFontSize,
    double ExtractRadius,
    double SpawnRadius,
    double LabelOffsetX,
    double LabelBaselineOffsetY);
