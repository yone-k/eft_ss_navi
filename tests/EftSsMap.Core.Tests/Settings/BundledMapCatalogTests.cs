using EftSsMap.Core.Calibration;
using EftSsMap.Core.Settings;

namespace EftSsMap.Core.Tests.Settings;

public sealed class BundledMapCatalogTests
{
    [Fact]
    public void ShouldLoadCalibratedProfilesInManifestOrder()
    {
        // Given: A bundled catalog containing a tarkov.dev map projection.
        var mapDirectory = Path.Combine(Path.GetTempPath(), $"eft-map-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(mapDirectory);
        File.WriteAllText(
            Path.Combine(mapDirectory, "catalog.json"),
            """
            {
              "version": 3,
              "replaceableImageFileNames": ["old-customs.png"],
              "maps": [
                {
                  "displayName": "Customs",
                  "fileName": "customs-tarkov-dev.png",
                  "width": 4096,
                  "height": 2082,
                  "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "transform": [0.239, 168.65, 0.239, 136.35],
                  "coordinateRotation": 180,
                  "bounds": [[698, -307], [-372, 237]]
                }
              ]
            }
            """);

        try
        {
            // When: The catalog is loaded from the deployed map directory.
            var catalog = BundledMapCatalog.Load(mapDirectory);

            // Then: It exposes a ready-to-use calibrated profile and migration metadata.
            Assert.Equal(3, catalog.Version);
            Assert.Equal(["old-customs.png"], catalog.ReplaceableImageFileNames);
            var profile = Assert.Single(catalog.Profiles);
            Assert.Equal("Customs", profile.DisplayName);
            Assert.Equal(Path.Combine(mapDirectory, "customs-tarkov-dev.png"), profile.ImagePath);
            Assert.Equal(3, profile.CalibrationPoints.Count);
            var projectedBound = profile.Transform.TransformPosition(new WorldPoint(698, -307));
            Assert.Equal(0, projectedBound.X, precision: 8);
            Assert.Equal(0, projectedBound.Y, precision: 8);
        }
        finally
        {
            Directory.Delete(mapDirectory, recursive: true);
        }
    }

    [Fact]
    public void ShouldLoadAllExtractTransitAndClusteredPmcSpawnMarkersForMappedProfile()
    {
        // Given: A map catalog entry linked to an offline tarkov.dev marker snapshot.
        var mapDirectory = Path.Combine(Path.GetTempPath(), $"eft-marker-catalog-{Guid.NewGuid():N}");
        Directory.CreateDirectory(mapDirectory);
        File.WriteAllText(
            Path.Combine(mapDirectory, "catalog.json"),
            """
            {
              "version": 1,
              "maps": [
                {
                  "displayName": "Woods",
                  "mapKey": "woods",
                  "fileName": "woods.png",
                  "width": 1000,
                  "height": 1000,
                  "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "transform": [1, 0, 1, 0],
                  "coordinateRotation": 180,
                  "bounds": [[100, -100], [-100, 100]]
                }
              ]
            }
            """);
        File.WriteAllText(
            Path.Combine(mapDirectory, "markers.json"),
            """
            {
              "maps": {
                "woods": {
                  "extracts": [
                    { "name": "Outskirts", "faction": "pmc", "x": -88.5, "z": 72.25 },
                    { "name": "Factory Gate", "faction": "shared", "x": 45, "z": -10 },
                    { "name": "Scav House", "faction": "scav", "x": 60, "z": 15 }
                  ],
                  "transits": [
                    { "name": "Transit to Reserve", "x": -25, "z": 80 }
                  ],
                  "pmcSpawns": [
                    { "x": 10, "z": -30 },
                    { "x": 14, "z": -34 },
                    { "x": 80, "z": 90 }
                  ]
                }
              }
            }
            """);

        try
        {
            // When: The deployed catalog is loaded.
            var catalog = BundledMapCatalog.Load(mapDirectory);

            // Then: Marker kinds, names, and EFT world coordinates are retained.
            var markers = catalog.MarkersByProfileName["Woods"];
            Assert.Collection(
                markers,
                marker => AssertMarker(marker, MapMarkerKind.PmcExtract, "Outskirts", -88.5, 72.25),
                marker => AssertMarker(marker, MapMarkerKind.SharedExtract, "Factory Gate", 45, -10),
                marker => AssertMarker(marker, MapMarkerKind.ScavExtract, "Scav House", 60, 15),
                marker => AssertMarker(marker, MapMarkerKind.Transit, "Transit to Reserve", -25, 80),
                marker => AssertMarker(marker, MapMarkerKind.PmcSpawn, null, 12, -32),
                marker => AssertMarker(marker, MapMarkerKind.PmcSpawn, null, 80, 90));
        }
        finally
        {
            Directory.Delete(mapDirectory, recursive: true);
        }
    }

    private static void AssertMarker(
        MapMarker marker,
        MapMarkerKind kind,
        string? name,
        double x,
        double z)
    {
        Assert.Equal(kind, marker.Kind);
        Assert.Equal(name, marker.Name);
        Assert.Equal(new WorldPoint(x, z), marker.Position);
    }
}
