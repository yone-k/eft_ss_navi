using EftSsMap.Core.Calibration;
using EftSsMap.Core.Images;
using EftSsMap.Core.Observations;

namespace EftSsMap.Core.Presentation;

/// <summary>
/// Owns the main screen's transient observation and profile-selection state.
/// </summary>
public sealed class MainStateCoordinator
{
    private static readonly MainViewState InitialState =
        new(null, null, null, null, MainViewStatus.WaitingForObservation);

    private bool calibrationValid;
    private ProfileImageValidationResult imageValidation;
    private long epoch;

    public MainStateCoordinator()
    {
        State = InitialState;
    }

    public MapProfile? SelectedProfile { get; private set; }

    public MainViewState State { get; private set; }

    public long Epoch => Interlocked.Read(ref epoch);

    public void SelectProfile(
        MapProfile profile,
        ImageFingerprint currentImage,
        bool calibrationValid)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(currentImage);

        Interlocked.Increment(ref epoch);
        SelectedProfile = profile;
        this.calibrationValid = calibrationValid;
        imageValidation = ProfileImageValidator.Validate(
            new ImageFingerprint(
                profile.ImagePath,
                profile.CalibratedImageWidth,
                profile.CalibratedImageHeight,
                profile.ImageSha256),
            currentImage);
        ClearObservation(MainViewStatus.WaitingForObservation);
    }

    public void ProcessObservation(PositionObservation observation, string fileName)
    {
        ProcessObservation(observation, fileName, Epoch);
    }

    public void ProcessObservation(PositionObservation observation, string fileName, long observationEpoch)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(fileName);

        if (observationEpoch != Epoch)
        {
            return;
        }

        if (SelectedProfile is null)
        {
            ClearObservation(MainViewStatus.ProfileNotSelected);
            return;
        }

        if (!calibrationValid)
        {
            ClearObservation(MainViewStatus.CalibrationError);
            return;
        }

        if (imageValidation != ProfileImageValidationResult.Match)
        {
            ClearObservation(MainViewStatus.ImageError);
            return;
        }

        var transform = SelectedProfile.Transform;
        var worldPosition = new WorldPoint(observation.Position.X, observation.Position.Z);
        var markerPosition = transform.TransformPosition(worldPosition);
        PixelPoint? markerDirection = observation.HorizontalForward is { } direction
            ? transform.TransformDirection(new WorldPoint(direction.X, direction.Y))
            : null;

        State = new MainViewState(
            markerPosition,
            markerDirection,
            observation.Position,
            fileName,
            MainViewStatus.PositionAvailable);
    }

    public void ChangeWatchDirectory(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        Interlocked.Increment(ref epoch);
        ClearObservation(SelectedProfile is null
            ? MainViewStatus.ProfileNotSelected
            : MainViewStatus.WaitingForObservation);
    }

    public void DeleteProfile(string displayName)
    {
        ArgumentNullException.ThrowIfNull(displayName);
        if (SelectedProfile is null ||
            !string.Equals(SelectedProfile.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Interlocked.Increment(ref epoch);
        SelectedProfile = null;
        calibrationValid = false;
        imageValidation = default;
        ClearObservation(MainViewStatus.ProfileNotSelected);
    }

    public void ExecuteSafely(MainFailureKind failureKind, Action operation)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(operation);
            operation();
        }
        catch
        {
            ClearObservation(StatusFor(failureKind));
        }
    }

    private static MainViewStatus StatusFor(MainFailureKind failureKind) => failureKind switch
    {
        MainFailureKind.Parsing => MainViewStatus.ParseError,
        MainFailureKind.Settings => MainViewStatus.SettingsError,
        MainFailureKind.Image => MainViewStatus.ImageError,
        MainFailureKind.Calibration => MainViewStatus.CalibrationError,
        _ => MainViewStatus.SettingsError,
    };

    private void ClearObservation(MainViewStatus status)
    {
        State = new MainViewState(null, null, null, null, status);
    }
}
