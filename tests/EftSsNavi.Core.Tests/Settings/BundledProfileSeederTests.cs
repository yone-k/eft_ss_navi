using EftSsNavi.Core.Calibration;
using EftSsNavi.Core.Settings;

namespace EftSsNavi.Core.Tests.Settings;

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
            ["Customs", "Factory", "Interchange", "Personal"],
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

    [Fact]
    public void ShouldReplaceLegacyBundledImageWithoutReplacingPersonalImage()
    {
        // Given: One profile based on a previously bundled image and one personal profile.
        var legacy = CreateProfile("Customs", @"C:\OldApp\Assets\Maps\customs-monki-glory4lyfe.png");
        var personal = CreateProfile("Woods", @"D:\My Maps\woods.png");
        var existing = new AppSettings(null, [legacy, personal], "Customs");
        MapProfile[] bundled =
        [
            CreateProfile("Customs", @"C:\NewApp\Assets\Maps\customs-tarkov-dev.png"),
            CreateProfile("Woods", @"C:\NewApp\Assets\Maps\woods-tarkov-dev.png"),
        ];

        // When: The tarkov.dev catalog replaces known legacy bundled files.
        var result = BundledProfileSeeder.Apply(
            existing,
            bundled,
            catalogVersion: 1,
            replaceableImageFileNames: ["customs-monki-glory4lyfe.png", "woods-jindouz.png"]);

        // Then: Only the known bundled image is migrated to the new calibrated profile.
        Assert.Equal(@"C:\NewApp\Assets\Maps\customs-tarkov-dev.png", result.MapProfiles[0].ImagePath);
        Assert.Equal(personal.ImagePath, result.MapProfiles[1].ImagePath);
    }

    [Fact]
    public void ShouldRenameExistingBundledProfileAndPreserveItsSelection()
    {
        // Given: Version 1 settings still contain the old SOT display name.
        var imageHash = new string('b', 64);
        var legacy = CreateProfile(
            "SOT",
            @"C:\OldApp\Assets\Maps\streets-of-tarkov-tarkov-dev.png",
            imageHash);
        var existing = new AppSettings(null, [legacy], "SOT", bundledMapCatalogVersion: 1);
        var renamed = CreateProfile(
            "Street Of Tarkov",
            @"C:\NewApp\Assets\Maps\streets-of-tarkov-tarkov-dev.png",
            imageHash);

        // When: The version 2 catalog is applied.
        var result = BundledProfileSeeder.Apply(
            existing,
            [renamed],
            catalogVersion: 2,
            replaceableImageFileNames: ["streets-of-tarkov-tarkov-dev.png"]);

        // Then: The old entry is replaced and the last selection follows its new name.
        var migrated = Assert.Single(result.MapProfiles);
        Assert.Equal("Street Of Tarkov", migrated.DisplayName);
        Assert.Equal(renamed.ImagePath, migrated.ImagePath);
        Assert.Equal("Street Of Tarkov", result.LastSelectedProfileName);
    }

    [Fact]
    public void ShouldNormalizeSelectedBundledDisplayNameWhenCapitalizationChanges()
    {
        // Given: Version 2 settings contain the previously released capitalization.
        var imageHash = new string('c', 64);
        var existingProfile = CreateProfile(
            "Street Of Tarkov",
            @"C:\OldApp\Assets\Maps\streets-of-tarkov-tarkov-dev.png",
            imageHash);
        var existing = new AppSettings(
            null,
            [existingProfile],
            "Street Of Tarkov",
            bundledMapCatalogVersion: 2);
        var normalized = CreateProfile(
            "Street of Tarkov",
            @"C:\NewApp\Assets\Maps\streets-of-tarkov-tarkov-dev.png",
            imageHash);

        // When: The version 3 catalog corrects the preposition capitalization.
        var result = BundledProfileSeeder.Apply(
            existing,
            [normalized],
            catalogVersion: 3,
            replaceableImageFileNames: ["streets-of-tarkov-tarkov-dev.png"]);

        // Then: Both the profile and its persisted selection use the canonical name.
        var migrated = Assert.Single(result.MapProfiles);
        Assert.Equal("Street of Tarkov", migrated.DisplayName);
        Assert.Equal("Street of Tarkov", result.LastSelectedProfileName);
        Assert.Equal(3, result.BundledMapCatalogVersion);
    }

    [Fact]
    public void ShouldSortAllProfilesByDisplayNameWhenCatalogVersionChanges()
    {
        // Given: Existing manual maps and bundled maps are in an arbitrary order.
        var existing = new AppSettings(
            null,
            [CreateProfile("Woods"), CreateProfile("Personal"), CreateProfile("Customs")],
            null,
            bundledMapCatalogVersion: 1);

        // When: A newer bundled catalog is applied.
        var result = BundledProfileSeeder.Apply(
            existing,
            [CreateProfile("Factory")],
            catalogVersion: 2);

        // Then: Every profile is stored in case-insensitive alphabetical order.
        Assert.Equal(
            ["Customs", "Factory", "Personal", "Woods"],
            result.MapProfiles.Select(profile => profile.DisplayName));
    }

    [Fact]
    public void ShouldPreservePartySettingsWhenCatalogVersionChanges()
    {
        // Given: Worker-based party settings and an older bundled-map catalog.
        var existing = new AppSettings(
            null,
            [],
            null,
            bundledMapCatalogVersion: 1,
            partyDisplayName: "Alpha",
            signalingWorkerUrl: "https://party.example.test",
            stunServers: ["stun:example.test:3478"]);

        // When: A newer bundled catalog is applied.
        var result = BundledProfileSeeder.Apply(
            existing,
            [CreateProfile("Woods")],
            catalogVersion: 2);

        // Then: Rebuilding the settings snapshot retains every party setting.
        Assert.Equal(existing.PartyDisplayName, result.PartyDisplayName);
        Assert.Equal(existing.SignalingWorkerUrl, result.SignalingWorkerUrl);
        Assert.Equal(existing.StunServers, result.StunServers);
    }

    [Fact]
    public void ShouldPreserveIgnoredUpdateVersionWhenCatalogVersionChanges()
    {
        // Given: A suppressed application update and an older bundled-map catalog.
        var existing = new AppSettings(
            null,
            [],
            null,
            bundledMapCatalogVersion: 1,
            ignoredUpdateVersion: "0.10.0");

        // When: A newer bundled catalog rebuilds the settings snapshot.
        var result = BundledProfileSeeder.Apply(
            existing,
            [CreateProfile("Woods")],
            catalogVersion: 2);

        // Then: The application update suppression is retained.
        Assert.Equal(existing.IgnoredUpdateVersion, result.IgnoredUpdateVersion);
    }

    private static MapProfile CreateProfile(
        string name,
        string? imagePath = null,
        string? imageHash = null)
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
            imageHash ?? new string('a', 64),
            points,
            new AffineTransform2D(10, 0, 0, 10, 0, 0));
    }
}
