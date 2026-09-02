using System.Security.Cryptography;
using EftSsMap.Core.Settings;

namespace EftSsMap.App.Tests.Configuration;

public sealed class MainWindowBundledCatalogTests
{
    [Fact]
    public void ShouldSeedVersionedBundledProfilesAtStartupAndPreserveVersionOnSave()
    {
        // Given: The main-window startup and persistence implementation.
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsMap.App",
            "MainWindow.xaml.cs"));

        // Then: Startup applies the deployed catalog and later saves retain its version.
        Assert.Contains("BundledMapCatalog.Load(_pickerDefaultDirectories.BundledMaps)", source);
        Assert.Contains("BundledProfileSeeder.Apply(", source);
        Assert.Contains("_bundledMapCatalogVersion", source);
        Assert.Contains(
            "new AppSettings(_watchDirectory, _profiles.ToArray(), selectedName, _bundledMapCatalogVersion)",
            source);
    }

    [Fact]
    public void ShouldProvideNineCalibratedOrdinaryMapsFromTheDeployedCatalog()
    {
        // Given: The source map directory distributed by the application.
        var mapDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "EftSsMap.App",
            "Assets",
            "Maps");

        // When: Its versioned catalog is loaded.
        var catalog = BundledMapCatalog.Load(mapDirectory);

        // Then: Every ordinary flat map has a complete generated calibration.
        Assert.Equal(
            ["Customs", "Factory", "Ground Zero", "Interchange", "Lighthouse", "Reserve", "Shoreline", "Street of Tarkov", "Woods"],
            catalog.Profiles.Select(profile => profile.DisplayName));
        Assert.Equal(3, catalog.Version);
        Assert.All(catalog.Profiles, profile =>
        {
            Assert.True(File.Exists(profile.ImagePath), profile.ImagePath);
            Assert.Equal(3, profile.CalibrationPoints.Count);
            var actualSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(profile.ImagePath)))
                .ToLowerInvariant();
            Assert.Equal(profile.ImageSha256, actualSha256);
        });
        Assert.Equal(9, catalog.MarkersByProfileName.Count);
        Assert.Equal(358, catalog.MarkersByProfileName.Values.Sum(markers => markers.Count));
        Assert.Contains(
            catalog.MarkersByProfileName.Values.SelectMany(markers => markers),
            marker => marker.Kind == MapMarkerKind.ScavExtract);
        Assert.Contains(
            catalog.MarkersByProfileName.Values.SelectMany(markers => markers),
            marker => marker.Kind == MapMarkerKind.Transit);
        Assert.Equal(
            209,
            catalog.MarkersByProfileName.Values
                .SelectMany(markers => markers)
                .Count(marker => marker.Kind == MapMarkerKind.PmcSpawn));
        Assert.DoesNotContain(
            catalog.MarkersByProfileName.Values.SelectMany(markers => markers),
            marker => marker.Kind is not (
                MapMarkerKind.PmcExtract or
                MapMarkerKind.SharedExtract or
                MapMarkerKind.ScavExtract or
                MapMarkerKind.Transit or
                MapMarkerKind.PmcSpawn));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EftSsMap.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
