namespace EftSsMap.Core.Calibration;

/// <summary>
/// A point in the original map image's pixel coordinate system.
/// </summary>
public readonly record struct PixelPoint(double X, double Y)
{
    internal bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
}
