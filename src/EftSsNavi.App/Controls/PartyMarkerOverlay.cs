using System.Collections.ObjectModel;
using EftSsNavi.Core.Calibration;

namespace EftSsNavi.App.Controls;

public sealed class PartyMarkerOverlay
{
    private static readonly string[] Colors =
    [
        "#2F80ED",
        "#F2C94C",
        "#9B51E0",
        "#FF6FB5",
        "#F5F5F5",
    ];

    public IReadOnlyList<PartyMarkerVisual> Markers { get; private set; } = [];

    public void Set(IReadOnlyList<PartyMarkerVisual> markers)
    {
        ArgumentNullException.ThrowIfNull(markers);
        Markers = new ReadOnlyCollection<PartyMarkerVisual>(markers.ToArray());
    }

    internal static string ColorForIndex(int colorIndex)
    {
        if ((uint)colorIndex >= Colors.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(colorIndex));
        }

        return Colors[colorIndex];
    }
}

public sealed record PartyMarkerVisual
{
    public PartyMarkerVisual(
        string displayName,
        PixelPoint position,
        PixelPoint? direction,
        int colorIndex,
        bool isStale)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName;
        Position = position;
        Direction = direction;
        ColorHex = PartyMarkerOverlay.ColorForIndex(colorIndex);
        Shape = direction is null ? PartyMarkerShape.Circle : PartyMarkerShape.Arrow;
        Opacity = isStale ? 0.5 : 1.0;
    }

    public string DisplayName { get; }

    public PixelPoint Position { get; }

    public PixelPoint? Direction { get; }

    public string ColorHex { get; }

    public PartyMarkerShape Shape { get; }

    public double Opacity { get; }
}

public enum PartyMarkerShape
{
    Arrow,
    Circle,
}
