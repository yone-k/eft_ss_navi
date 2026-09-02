namespace EftSsMap.Core.Calibration;

/// <summary>
/// A point in EFT's horizontal world plane, where the axes are X and Z.
/// </summary>
public readonly record struct WorldPoint(double X, double Z)
{
    internal bool IsFinite => double.IsFinite(X) && double.IsFinite(Z);
}
