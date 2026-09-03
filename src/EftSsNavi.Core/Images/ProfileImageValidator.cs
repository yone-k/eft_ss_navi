namespace EftSsNavi.Core.Images;

public static class ProfileImageValidator
{
    public static ProfileImageValidationResult Validate(
        ImageFingerprint calibrated,
        ImageFingerprint current)
    {
        ArgumentNullException.ThrowIfNull(calibrated);
        ArgumentNullException.ThrowIfNull(current);

        if (!PathsMatch(calibrated.Path, current.Path))
        {
            return ProfileImageValidationResult.PathMismatch;
        }

        if (calibrated.Width != current.Width)
        {
            return ProfileImageValidationResult.WidthMismatch;
        }

        if (calibrated.Height != current.Height)
        {
            return ProfileImageValidationResult.HeightMismatch;
        }

        return string.Equals(calibrated.Sha256, current.Sha256, StringComparison.OrdinalIgnoreCase)
            ? ProfileImageValidationResult.Match
            : ProfileImageValidationResult.HashMismatch;
    }

    private static bool PathsMatch(string? left, string? right)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            var normalizedLeft = Path.GetFullPath(left);
            var normalizedRight = Path.GetFullPath(right);
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
