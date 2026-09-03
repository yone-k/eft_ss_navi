using EftSsNavi.Core.Calibration;

namespace EftSsNavi.App.Presentation;

public sealed class PositionCorrectionSession
{
    private PositionCorrectionSession(
        MapProfile originalProfile,
        WorldPoint worldPosition,
        int replacementIndex)
    {
        OriginalProfile = originalProfile;
        WorldPosition = worldPosition;
        ReplacementIndex = replacementIndex;
    }

    public MapProfile OriginalProfile { get; }

    public WorldPoint WorldPosition { get; }

    public int ReplacementIndex { get; private set; }

    public MapProfile? PendingProfile { get; private set; }

    public static bool TryCreate(
        MapProfile profile,
        WorldPoint worldPosition,
        out PositionCorrectionSession session)
    {
        ArgumentNullException.ThrowIfNull(profile);
        session = null!;
        if (!MapProfileCorrection.TryFindNearestCalibrationPointIndex(
            profile,
            worldPosition,
            out var replacementIndex))
        {
            return false;
        }

        session = new PositionCorrectionSession(profile, worldPosition, replacementIndex);
        return true;
    }

    public bool TryPreview(PixelPoint correctedPixel)
    {
        if (!MapProfileCorrection.TryApply(
            OriginalProfile,
            WorldPosition,
            correctedPixel,
            ReplacementIndex,
            out var correctedProfile))
        {
            PendingProfile = null;
            return false;
        }

        PendingProfile = correctedProfile;
        return true;
    }

    public bool TrySelectReplacement(int replacementIndex)
    {
        if (replacementIndex < 0
            || replacementIndex >= Math.Min(3, OriginalProfile.CalibrationPoints.Count))
        {
            return false;
        }

        if (ReplacementIndex != replacementIndex)
        {
            ReplacementIndex = replacementIndex;
            PendingProfile = null;
        }

        return true;
    }

    public bool TryConfirm(out MapProfile correctedProfile)
    {
        correctedProfile = OriginalProfile;
        if (PendingProfile is null)
        {
            return false;
        }

        correctedProfile = PendingProfile;
        PendingProfile = null;
        return true;
    }

    public void Cancel() => PendingProfile = null;
}
