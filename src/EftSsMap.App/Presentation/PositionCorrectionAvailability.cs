using EftSsMap.Core.Calibration;

namespace EftSsMap.App.Presentation;

public static class PositionCorrectionAvailability
{
    public static bool IsAvailable(
        MapProfile? selectedProfile,
        IReadOnlyList<MapProfile> bundledProfiles)
    {
        ArgumentNullException.ThrowIfNull(bundledProfiles);
        if (selectedProfile is null)
        {
            return false;
        }

        return !bundledProfiles.Any(bundledProfile =>
            string.Equals(
                bundledProfile.DisplayName,
                selectedProfile.DisplayName,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                bundledProfile.ImagePath,
                selectedProfile.ImagePath,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                bundledProfile.ImageSha256,
                selectedProfile.ImageSha256,
                StringComparison.OrdinalIgnoreCase));
    }
}
