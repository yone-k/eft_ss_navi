using EftSsNavi.Core.Calibration;

namespace EftSsNavi.Core.Settings;

/// <summary>
/// Adds a newly released bundled-map catalog once without overwriting personal calibrations.
/// </summary>
public static class BundledProfileSeeder
{
    public static AppSettings Apply(
        AppSettings settings,
        IReadOnlyList<MapProfile> bundledProfiles,
        int catalogVersion,
        IReadOnlyCollection<string>? replaceableImageFileNames = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(bundledProfiles);
        ArgumentOutOfRangeException.ThrowIfLessThan(catalogVersion, 1);

        if (settings.BundledMapCatalogVersion >= catalogVersion)
        {
            return settings;
        }

        var mergedProfiles = settings.MapProfiles.ToList();
        var replaceableFiles = new HashSet<string>(
            replaceableImageFileNames ?? [],
            StringComparer.OrdinalIgnoreCase);
        var selectedProfileName = settings.LastSelectedProfileName;
        foreach (var bundledProfile in bundledProfiles)
        {
            ArgumentNullException.ThrowIfNull(bundledProfile);
            var existingIndex = mergedProfiles.FindIndex(profile => string.Equals(
                profile.DisplayName,
                bundledProfile.DisplayName,
                StringComparison.OrdinalIgnoreCase));
            if (existingIndex < 0)
            {
                var renamedIndex = mergedProfiles.FindIndex(profile =>
                    replaceableFiles.Contains(Path.GetFileName(profile.ImagePath)) &&
                    string.Equals(
                        Path.GetFileName(profile.ImagePath),
                        Path.GetFileName(bundledProfile.ImagePath),
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        profile.ImageSha256,
                        bundledProfile.ImageSha256,
                        StringComparison.OrdinalIgnoreCase));
                if (renamedIndex >= 0)
                {
                    var previousName = mergedProfiles[renamedIndex].DisplayName;
                    mergedProfiles[renamedIndex] = bundledProfile;
                    if (string.Equals(
                        selectedProfileName,
                        previousName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        selectedProfileName = bundledProfile.DisplayName;
                    }
                }
                else
                {
                    mergedProfiles.Add(bundledProfile);
                }

                continue;
            }

            var existingProfile = mergedProfiles[existingIndex];
            var existingFileName = Path.GetFileName(existingProfile.ImagePath);
            if (replaceableFiles.Contains(existingFileName))
            {
                mergedProfiles[existingIndex] = bundledProfile;
                if (string.Equals(
                    selectedProfileName,
                    existingProfile.DisplayName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    selectedProfileName = bundledProfile.DisplayName;
                }
            }
        }

        return new AppSettings(
            settings.WatchDirectory,
            mergedProfiles
                .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            selectedProfileName,
            catalogVersion);
    }
}
