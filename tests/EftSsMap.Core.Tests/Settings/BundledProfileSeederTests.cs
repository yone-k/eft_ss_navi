using EftSsMap.Core.Calibration;
using EftSsMap.Core.Settings;

namespace EftSsMap.Core.Tests.Settings;

public sealed class BundledProfileSeederTests
{
    [Fact]
    public void ShouldAddEveryBundledProfileWhenCatalogHasNotBeenApplied()
    {
        // Given: Existing settings created before bundled maps were introduced.
        var existing = new AppSettings(
            @"C:\EFT\Screenshots",
            [CreateProfile("Personal")],
            "Personal");
        MapProfile[] bundled =
        [
            CreateProfile("Customs"),
            CreateProfile("Factory"),
            CreateProfile("Interchange"),
        ];

        // When: The current bundled-map catalog is applied.
        var result = BundledProfileSeeder.Apply(existing, bundled, catalogVersion: 1);

        // Then: User settings are preserved and all non-conflicting bundled profiles are added.
        Assert.Equal(@"C:\EFT\Screenshots", result.WatchDirectory);
        Assert.Equal("Personal", result.LastSelectedProfileName);
        Assert.Equal(1, result.BundledMapCatalogVersion);
        Assert.Equal(
            ["Personal", "Customs", "Factory", "Interchange"],
            result.MapProfiles.Select(profile => profile.DisplayName));
    }

    [Fact]
    public void ShouldNotReplaceUserProfileWithSameNameAsBundledProfile()
    {
        // Given: A user-created profile whose name collides with a bundled profile.
        var personalCustoms = CreateProfile("Customs", @"C:\Personal\customs.png");
        var existing = new AppSettings(null, [personalCustoms], "Customs");
        var bundledCustoms = CreateProfile("CUSTOMS", @"C:\Bundled\customs.png");

        // When: The bundled catalog is applied.
        var result = BundledProfileSeeder.Apply(existing, [bundledCustoms], catalogVersion: 1);

        // Then: The personal calibration wins and no duplicate is created.
        var retained = Assert.Single(result.MapProfiles);
        Assert.Equal(personalCustoms.ImagePath, retained.ImagePath);
        Assert.Equal(1, result.BundledMapCatalogVersion);
    }

    [Fact]
    public void ShouldNotRestoreDeletedBundledProfilesUntilCatalogVersionChanges()
    {
        // Given: The current catalog version was already applied and its profile was deleted later.
        var existing = new AppSettings(
            null,
            [],
            null,
            bundledMapCatalogVersion: 1);

        // When: The same bundled catalog is considered again at startup.
        var result = BundledProfileSeeder.Apply(
            existing,
            [CreateProfile("Factory")],
            catalogVersion: 1);

        // Then: The deliberately deleted profile stays deleted.
        Assert.Empty(result.MapProfiles);
        Assert.Same(existing, result);
    }

    private static MapProfile CreateProfile(string name, string? imagePath = null)
    {
        CalibrationPoint[] points =
        [
            new(new WorldPoint(0, 0), new PixelPoint(0, 0)),
            new(new WorldPoint(1, 0), new PixelPoint(10, 0)),
            new(new WorldPoint(0, 1), new PixelPoint(0, 10)),
        ];

        return new MapProfile(
            name,
            imagePath ?? $@"C:\Bundled\{name}.png",
            100,
            100,
            new string('a', 64),
            points,
            new AffineTransform2D(10, 0, 0, 10, 0, 0));
    }
}
