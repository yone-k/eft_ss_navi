namespace EftSsNavi.Core.Calibration;

/// <summary>
/// Maps EFT X/Z coordinates to map-image X/Y pixels.
/// </summary>
public readonly record struct AffineTransform2D(
    double M11,
    double M12,
    double M21,
    double M22,
    double TranslationX,
    double TranslationY)
{
    public PixelPoint TransformPosition(WorldPoint position) =>
        new(
            (M11 * position.X) + (M12 * position.Z) + TranslationX,
            (M21 * position.X) + (M22 * position.Z) + TranslationY);

    public PixelPoint TransformDirection(WorldPoint direction) =>
        new(
            (M11 * direction.X) + (M12 * direction.Z),
            (M21 * direction.X) + (M22 * direction.Z));

    internal bool IsFinite =>
        double.IsFinite(M11) &&
        double.IsFinite(M12) &&
        double.IsFinite(M21) &&
        double.IsFinite(M22) &&
        double.IsFinite(TranslationX) &&
        double.IsFinite(TranslationY);
}
