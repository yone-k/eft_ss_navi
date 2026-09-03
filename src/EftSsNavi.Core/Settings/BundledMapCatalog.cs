using System.Collections.ObjectModel;
using System.Text.Json;
using EftSsNavi.Core.Calibration;

namespace EftSsNavi.Core.Settings;

/// <summary>
/// Loads the versioned set of rasterized tarkov.dev maps distributed with the app.
/// </summary>
public sealed class BundledMapCatalog
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private BundledMapCatalog(
        int version,
        IReadOnlyList<MapProfile> profiles,
        IReadOnlyList<string> replaceableImageFileNames,
        IReadOnlyDictionary<string, IReadOnlyList<MapMarker>> markersByProfileName)
    {
        Version = version;
        Profiles = new ReadOnlyCollection<MapProfile>(profiles.ToArray());
        ReplaceableImageFileNames = new ReadOnlyCollection<string>(
            replaceableImageFileNames.ToArray());
        MarkersByProfileName = new ReadOnlyDictionary<string, IReadOnlyList<MapMarker>>(
            new Dictionary<string, IReadOnlyList<MapMarker>>(
                markersByProfileName,
                StringComparer.OrdinalIgnoreCase));
    }

    public int Version { get; }

    public IReadOnlyList<MapProfile> Profiles { get; }

    public IReadOnlyList<string> ReplaceableImageFileNames { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<MapMarker>> MarkersByProfileName { get; }

    public static BundledMapCatalog Load(string mapDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapDirectory);
        var fullMapDirectory = Path.GetFullPath(mapDirectory);
        var catalogPath = Path.Combine(fullMapDirectory, "catalog.json");
        var document = JsonSerializer.Deserialize<CatalogDocument>(
            File.ReadAllText(catalogPath),
            SerializerOptions) ?? throw new InvalidDataException("Bundled map catalog is empty.");

        ArgumentOutOfRangeException.ThrowIfLessThan(document.Version, 1);
        if (document.Maps is null || document.Maps.Count == 0)
        {
            throw new InvalidDataException("Bundled map catalog contains no maps.");
        }

        var orderedMaps = document.Maps
            .OrderBy(map => map.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var profiles = orderedMaps.Select(map => CreateProfile(fullMapDirectory, map)).ToArray();
        var markersByProfileName = LoadMarkers(fullMapDirectory, orderedMaps);
        return new BundledMapCatalog(
            document.Version,
            profiles,
            document.ReplaceableImageFileNames ?? [],
            markersByProfileName);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<MapMarker>> LoadMarkers(
        string mapDirectory,
        IReadOnlyList<CatalogMap> maps)
    {
        var mappedProfiles = maps
            .Where(map => !string.IsNullOrWhiteSpace(map.MapKey))
            .ToArray();
        if (mappedProfiles.Length == 0)
        {
            return new Dictionary<string, IReadOnlyList<MapMarker>>(StringComparer.OrdinalIgnoreCase);
        }

        var markerPath = Path.Combine(mapDirectory, "markers.json");
        var markerDocument = JsonSerializer.Deserialize<MarkerCatalogDocument>(
            File.ReadAllText(markerPath),
            SerializerOptions) ?? throw new InvalidDataException("Bundled map marker catalog is empty.");
        if (markerDocument.Maps is null)
        {
            throw new InvalidDataException("Bundled map marker catalog contains no maps.");
        }

        var result = new Dictionary<string, IReadOnlyList<MapMarker>>(StringComparer.OrdinalIgnoreCase);
        foreach (var map in mappedProfiles)
        {
            if (!markerDocument.Maps.TryGetValue(map.MapKey!, out var markerSet))
            {
                throw new InvalidDataException($"Bundled marker data is missing for '{map.MapKey}'.");
            }

            var markers = new List<MapMarker>();
            foreach (var extract in markerSet.Extracts ?? [])
            {
                var kind = extract.Faction switch
                {
                    "pmc" => MapMarkerKind.PmcExtract,
                    "shared" => MapMarkerKind.SharedExtract,
                    "scav" => MapMarkerKind.ScavExtract,
                    _ => throw new InvalidDataException(
                        $"Bundled extract '{extract.Name}' has unsupported faction '{extract.Faction}'."),
                };
                markers.Add(new MapMarker(
                    kind,
                    extract.Name,
                    new WorldPoint(extract.X, extract.Z)));
            }

            markers.AddRange((markerSet.Transits ?? []).Select(transit => new MapMarker(
                MapMarkerKind.Transit,
                transit.Name,
                new WorldPoint(transit.X, transit.Z))));

            markers.AddRange((markerSet.PmcSpawns ?? []).Select(spawn => new MapMarker(
                MapMarkerKind.PmcSpawn,
                null,
                new WorldPoint(spawn.X, spawn.Z))));
            result.Add(map.DisplayName!, MapMarkerClusterer.ClusterPmcSpawns(markers));
        }

        return result;
    }

    private static MapProfile CreateProfile(string mapDirectory, CatalogMap map)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(map.DisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(map.FileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(map.Sha256);
        ArgumentOutOfRangeException.ThrowIfLessThan(map.Width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(map.Height, 1);
        if (map.Transform is not { Count: 4 } ||
            map.Bounds is not { Count: 2 } ||
            map.Bounds.Any(bound => bound is not { Count: 2 }))
        {
            throw new InvalidDataException($"Bundled map '{map.DisplayName}' has invalid projection data.");
        }

        var projection = new TarkovDevMapProjection(
            map.Transform[0],
            map.Transform[1],
            map.Transform[2],
            map.Transform[3],
            map.CoordinateRotation,
            new WorldPoint(map.Bounds[0][0], map.Bounds[0][1]),
            new WorldPoint(map.Bounds[1][0], map.Bounds[1][1]));
        var points = projection.CreateCalibrationPoints(map.Width, map.Height);

        return new MapProfile(
            map.DisplayName,
            Path.Combine(mapDirectory, map.FileName),
            map.Width,
            map.Height,
            map.Sha256,
            points,
            projection.CreateTransform(map.Width, map.Height));
    }

    private sealed class CatalogDocument
    {
        public int Version { get; init; }

        public IReadOnlyList<string>? ReplaceableImageFileNames { get; init; }

        public IReadOnlyList<CatalogMap>? Maps { get; init; }
    }

    private sealed class CatalogMap
    {
        public string? DisplayName { get; init; }

        public string? MapKey { get; init; }

        public string? FileName { get; init; }

        public int Width { get; init; }

        public int Height { get; init; }

        public string? Sha256 { get; init; }

        public IReadOnlyList<double>? Transform { get; init; }

        public int CoordinateRotation { get; init; }

        public IReadOnlyList<IReadOnlyList<double>>? Bounds { get; init; }
    }

    private sealed class MarkerCatalogDocument
    {
        public IReadOnlyDictionary<string, MarkerSet>? Maps { get; init; }
    }

    private sealed class MarkerSet
    {
        public IReadOnlyList<ExtractMarker>? Extracts { get; init; }

        public IReadOnlyList<NamedPositionMarker>? Transits { get; init; }

        public IReadOnlyList<PositionMarker>? PmcSpawns { get; init; }
    }

    private sealed class ExtractMarker
    {
        public string? Name { get; init; }

        public string? Faction { get; init; }

        public double X { get; init; }

        public double Z { get; init; }
    }

    private sealed class PositionMarker
    {
        public double X { get; init; }

        public double Z { get; init; }
    }

    private sealed class NamedPositionMarker
    {
        public string? Name { get; init; }

        public double X { get; init; }

        public double Z { get; init; }
    }
}
