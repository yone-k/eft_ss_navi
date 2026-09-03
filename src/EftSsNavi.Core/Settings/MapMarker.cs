using EftSsNavi.Core.Calibration;

namespace EftSsNavi.Core.Settings;

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
