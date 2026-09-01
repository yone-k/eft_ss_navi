namespace EftSsMap.Core.Images;

/// <summary>
/// Identifies the exact map image used by a calibration.
/// </summary>
public sealed record ImageFingerprint(
    string Path,
    int Width,
    int Height,
    string Sha256);
