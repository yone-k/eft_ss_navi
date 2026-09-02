using System.Collections.ObjectModel;
using EftSsMap.Core.Calibration;
using EftSsMap.Core.Settings;

namespace EftSsMap.App.Controls;

public sealed class MapMarkerOverlay
{
    public IReadOnlyList<MapMarkerVisual> Markers { get; private set; } = [];

    public void Set(IReadOnlyList<MapMarker> markers, AffineTransform2D transform)
    {
        ArgumentNullException.ThrowIfNull(markers);
        Markers = new ReadOnlyCollection<MapMarkerVisual>(markers
            .Select(marker => new MapMarkerVisual(
                marker.Kind,
                marker.Name,
                transform.TransformPosition(marker.Position)))
            .ToArray());
    }
}

public sealed record MapMarkerVisual(
    MapMarkerKind Kind,
    string? Name,
    PixelPoint Position);
