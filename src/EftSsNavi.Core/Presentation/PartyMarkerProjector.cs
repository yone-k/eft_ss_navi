using EftSsNavi.Core.Calibration;

namespace EftSsNavi.Core.Presentation;

/// <summary>
/// Converts a remote party position into the selected map's pixel coordinate system.
/// </summary>
public static class PartyMarkerProjector
{
    public static PartyMarkerProjection? Project(
        MapProfile? selectedProfile,
        bool calibrationValid,
        string? positionMapName,
        WorldPoint worldPosition,
        WorldPoint? worldDirection)
    {
        if (selectedProfile is null ||
            !calibrationValid ||
            !string.Equals(
                selectedProfile.DisplayName,
                positionMapName,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var transform = selectedProfile.Transform;
        return new PartyMarkerProjection(
            transform.TransformPosition(worldPosition),
            worldDirection is { } direction
                ? transform.TransformDirection(direction)
                : null);
    }
}

/// <summary>
/// Pixel-space values used to draw one remote party marker.
/// </summary>
public readonly record struct PartyMarkerProjection(
    PixelPoint Position,
    PixelPoint? Direction);
