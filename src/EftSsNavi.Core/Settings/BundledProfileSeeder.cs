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

        var relocatedProfiles = RelocateBundledProfiles(
            settings.MapProfiles,
            bundledProfiles,
            out var profilesRelocated);
        if (settings.BundledMapCatalogVersion >= catalogVersion)
        {
            return profilesRelocated
                ? CreateSettings(
                    settings,
                    relocatedProfiles,
                    settings.BundledMapCatalogVersion,
                    settings.LastSelectedProfileName)
                : settings;
        }

        var mergedProfiles = relocatedProfiles.ToList();
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

        return CreateSettings(
            settings,
            mergedProfiles
                .OrderBy(profile => profile.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            catalogVersion,
            selectedProfileName);
    }

    private static IReadOnlyList<MapProfile> RelocateBundledProfiles(
        IReadOnlyList<MapProfile> existingProfiles,
        IReadOnlyList<MapProfile> bundledProfiles,
        out bool profilesRelocated)
    {
        var result = existingProfiles.ToArray();
        profilesRelocated = false;
        for (var index = 0; index < result.Length; index++)
        {
            var existing = result[index];
            var bundled = bundledProfiles.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.DisplayName,
                    existing.DisplayName,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    Path.GetFileName(candidate.ImagePath),
                    Path.GetFileName(existing.ImagePath),
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    candidate.ImageSha256,
                    existing.ImageSha256,
                    StringComparison.OrdinalIgnoreCase));
            if (bundled is null ||
                !IsBundledMapPath(existing.ImagePath) ||
                string.Equals(existing.ImagePath, bundled.ImagePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result[index] = new MapProfile(
                existing.DisplayName,
                bundled.ImagePath,
                existing.CalibratedImageWidth,
                existing.CalibratedImageHeight,
                existing.ImageSha256,
                existing.CalibrationPoints,
                existing.Transform,
                existing.ImageRotationQuarterTurns);
            profilesRelocated = true;
        }

        return result;
    }

    private static bool IsBundledMapPath(string imagePath)
    {
        var mapDirectory = Path.GetDirectoryName(imagePath);
        var assetsDirectory = Path.GetDirectoryName(mapDirectory);
        return string.Equals(
                Path.GetFileName(mapDirectory),
                "Maps",
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                Path.GetFileName(assetsDirectory),
                "Assets",
                StringComparison.OrdinalIgnoreCase);
    }

    private static AppSettings CreateSettings(
        AppSettings settings,
        IReadOnlyList<MapProfile> profiles,
        int catalogVersion,
        string? selectedProfileName) =>
        new(
            settings.WatchDirectory,
            profiles,
            selectedProfileName,
            catalogVersion,
            settings.PartyDisplayName,
            settings.SignalingWorkerUrl,
            settings.StunServers,
            settings.IgnoredUpdateVersion);
}
