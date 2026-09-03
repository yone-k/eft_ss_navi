namespace EftSsNavi.Core.Settings;

/// <summary>
/// Identifies the settings operation that failed.
/// </summary>
public enum SettingsErrorKind
{
    None,
    Read,
    Deserialize,
    Validation,
    Serialize,
    WriteTemporary,
    Move,
    Replace,
    SaveBlocked,
}
