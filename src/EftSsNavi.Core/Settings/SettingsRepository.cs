using System.Text.Json;

namespace EftSsNavi.Core.Settings;

/// <summary>
/// Loads and saves application settings without exposing a partially-written destination file.
/// </summary>
public sealed class SettingsRepository
{
    private readonly ISettingsFileSystem fileSystem;
    private readonly string destinationPath;
    private bool saveBlockedByLoadFailure;

    public SettingsRepository(ISettingsFileSystem fileSystem, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        this.fileSystem = fileSystem;
        this.destinationPath = destinationPath;
    }

    public SettingsLoadResult Load()
    {
        string json;
        try
        {
            json = fileSystem.ReadAllText(destinationPath);
        }
        catch (Exception exception)
        {
            saveBlockedByLoadFailure = true;
            return SettingsLoadResult.Failure(SettingsErrorKind.Read, exception);
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings is null)
            {
                saveBlockedByLoadFailure = true;
                return SettingsLoadResult.Failure(
                    SettingsErrorKind.Deserialize,
                    "The settings document did not contain an object.");
            }

            if (!TryValidate(settings, out var validationError))
            {
                saveBlockedByLoadFailure = true;
                return SettingsLoadResult.Failure(SettingsErrorKind.Deserialize, validationError);
            }

            saveBlockedByLoadFailure = false;
            return SettingsLoadResult.Success(settings);
        }
        catch (Exception exception)
        {
            saveBlockedByLoadFailure = true;
            return SettingsLoadResult.Failure(SettingsErrorKind.Deserialize, exception);
        }
    }

    public SettingsSaveResult Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (saveBlockedByLoadFailure)
        {
            return SettingsSaveResult.Failure(
                SettingsErrorKind.SaveBlocked,
                "Saving is blocked because this repository failed to load the existing settings. Reset the protection explicitly before saving.");
        }

        if (!TryValidate(settings, out var validationError))
        {
            return SettingsSaveResult.Failure(SettingsErrorKind.Validation, validationError);
        }

        string json;
        try
        {
            json = JsonSerializer.Serialize(settings);
        }
        catch (Exception exception)
        {
            return SettingsSaveResult.Failure(SettingsErrorKind.Serialize, exception);
        }

        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var fileName = Path.GetFileName(destinationPath);
        var temporaryPath = Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            try
            {
                fileSystem.WriteAllText(temporaryPath, json);
            }
            catch (Exception exception)
            {
                return SettingsSaveResult.Failure(SettingsErrorKind.WriteTemporary, exception);
            }

            if (fileSystem.FileExists(destinationPath))
            {
                try
                {
                    fileSystem.ReplaceFile(temporaryPath, destinationPath);
                    return SettingsSaveResult.Success();
                }
                catch (Exception exception)
                {
                    return SettingsSaveResult.Failure(SettingsErrorKind.Replace, exception);
                }
            }

            try
            {
                fileSystem.MoveFile(temporaryPath, destinationPath);
                return SettingsSaveResult.Success();
            }
            catch (Exception exception)
            {
                return SettingsSaveResult.Failure(SettingsErrorKind.Move, exception);
            }
        }
        finally
        {
            TryDeleteTemporaryFile(temporaryPath);
        }
    }

    public void ResetLoadFailureProtection() => saveBlockedByLoadFailure = false;

    private static bool TryValidate(AppSettings settings, out string errorMessage)
    {
        var profileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in settings.MapProfiles)
        {
            if (profile is null)
            {
                errorMessage = "The settings document contains a null map profile.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(profile.DisplayName))
            {
                errorMessage = "Every map profile must have a display name.";
                return false;
            }

            if (!profileNames.Add(profile.DisplayName))
            {
                errorMessage = $"The map profile name '{profile.DisplayName}' is duplicated.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    private void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (fileSystem.FileExists(temporaryPath))
            {
                fileSystem.DeleteFile(temporaryPath);
            }
        }
        catch
        {
            // Cleanup is best-effort and must not hide the original save result.
        }
    }
}
