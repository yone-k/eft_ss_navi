namespace EftSsNavi.Core.Calibration;

/// <summary>
/// A correspondence between an EFT world point and a map-image pixel.
/// </summary>
public readonly record struct CalibrationPoint(WorldPoint World, PixelPoint Pixel);
