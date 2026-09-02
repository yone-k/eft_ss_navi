using EftSsMap.Core.Calibration;

namespace EftSsMap.Core.Settings;

/// <summary>
/// Adds a newly released bundled-map catalog once without overwriting personal calibrations.
/// </summary>
public static class BundledProfileSeeder
{
    public static AppSettings Apply(
        AppSettings settings,
        IReadOnlyList<MapProfile> bundledProfiles,
        int catalogVersion)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(bundledProfiles);
        ArgumentOutOfRangeException.ThrowIfLessThan(catalogVersion, 1);

        if (settings.BundledMapCatalogVersion >= catalogVersion)
        {
            return settings;
        }

        var mergedProfiles = settings.MapProfiles.ToList();
        var names = new HashSet<string>(
            mergedProfiles.Select(profile => profile.DisplayName),
            StringComparer.OrdinalIgnoreCase);
        foreach (var bundledProfile in bundledProfiles)
        {
            ArgumentNullException.ThrowIfNull(bundledProfile);
            if (names.Add(bundledProfile.DisplayName))
            {
                mergedProfiles.Add(bundledProfile);
            }
        }

        return new AppSettings(
            settings.WatchDirectory,
            mergedProfiles,
            settings.LastSelectedProfileName,
            catalogVersion);
    }
}
