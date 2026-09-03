namespace EftSsNavi.Core.Settings;

public sealed class SettingsSaveResult
{
    private SettingsSaveResult(bool isSuccess, SettingsErrorKind errorKind, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public SettingsErrorKind ErrorKind { get; }

    public string? ErrorMessage { get; }

    internal static SettingsSaveResult Success() => new(true, SettingsErrorKind.None, null);

    internal static SettingsSaveResult Failure(SettingsErrorKind kind, Exception exception) =>
        new(false, kind, exception.Message);

    internal static SettingsSaveResult Failure(SettingsErrorKind kind, string message) =>
        new(false, kind, message);
}

public sealed class SettingsLoadResult
{
    private SettingsLoadResult(
        bool isSuccess,
        AppSettings? value,
        SettingsErrorKind errorKind,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        Value = value;
        ErrorKind = errorKind;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public AppSettings? Value { get; }

    public SettingsErrorKind ErrorKind { get; }

    public string? ErrorMessage { get; }

    internal static SettingsLoadResult Success(AppSettings value) =>
        new(true, value, SettingsErrorKind.None, null);

    internal static SettingsLoadResult Failure(SettingsErrorKind kind, Exception exception) =>
        new(false, null, kind, exception.Message);

    internal static SettingsLoadResult Failure(SettingsErrorKind kind, string message) =>
        new(false, null, kind, message);
}
