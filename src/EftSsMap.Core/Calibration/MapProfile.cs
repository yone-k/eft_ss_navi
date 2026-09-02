using System.Collections.ObjectModel;
using EftSsMap.Core.Images;

namespace EftSsMap.Core.Calibration;

/// <summary>
/// Metadata and coefficients needed to validate and restore a calibrated map.
/// </summary>
public sealed class MapProfile
{
    public static MapProfile CreateUncalibrated(string displayName, ImageFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        return new MapProfile(
            displayName,
            fingerprint.Path,
            fingerprint.Width,
            fingerprint.Height,
            fingerprint.Sha256,
            [],
            default);
    }

    public MapProfile(
        string displayName,
        string imagePath,
        int calibratedImageWidth,
        int calibratedImageHeight,
        string imageSha256,
        IReadOnlyList<CalibrationPoint> calibrationPoints,
        AffineTransform2D transform)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        ArgumentNullException.ThrowIfNull(imagePath);
        ArgumentNullException.ThrowIfNull(imageSha256);
        ArgumentNullException.ThrowIfNull(calibrationPoints);

        DisplayName = displayName;
        ImagePath = imagePath;
        CalibratedImageWidth = calibratedImageWidth;
        CalibratedImageHeight = calibratedImageHeight;
        ImageSha256 = imageSha256;
        CalibrationPoints = new ReadOnlyCollection<CalibrationPoint>(calibrationPoints.ToArray());
        Transform = transform;
    }

    public string DisplayName { get; }

    public string ImagePath { get; }

    public int CalibratedImageWidth { get; }

    public int CalibratedImageHeight { get; }

    public string ImageSha256 { get; }

    public IReadOnlyList<CalibrationPoint> CalibrationPoints { get; }

    public AffineTransform2D Transform { get; }
}
