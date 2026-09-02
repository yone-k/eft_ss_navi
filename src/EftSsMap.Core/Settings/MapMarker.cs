using EftSsMap.Core.Calibration;

namespace EftSsMap.Core.Settings;

public enum MapMarkerKind
{
    PmcExtract,
    SharedExtract,
    ScavExtract,
    Transit,
    PmcSpawn,
}

public sealed record MapMarker(
    MapMarkerKind Kind,
    string? Name,
    WorldPoint Position);
