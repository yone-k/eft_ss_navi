using System.Globalization;

namespace EftSsNavi.App.Presentation;

public sealed class PartyUiState
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    public PartyUiState(
        PartyUiRole role,
        string? hostMapName,
        bool hasMatchingProfile)
    {
        Role = role;
        HostMapName = string.IsNullOrWhiteSpace(hostMapName) ? null : hostMapName;
        HasMatchingProfile = hasMatchingProfile;
    }

    public PartyUiRole Role { get; }

    public string? HostMapName { get; }

    public bool HasMatchingProfile { get; }

    public bool MapActionsEnabled => Role != PartyUiRole.Participant;

    public bool PartyMarkersVisible => Role switch
    {
        PartyUiRole.NotJoined => false,
        PartyUiRole.Host => true,
        PartyUiRole.Participant => HostMapName is not null && HasMatchingProfile,
        _ => false,
    };

    public string MapStatusMessage => Role == PartyUiRole.Participant
        ? HostMapName switch
        {
            null => "ホストがマップを選択するまで待機しています。",
            _ when !HasMatchingProfile => $"ホストのマップ「{HostMapName}」がこのPCにありません。",
            _ => string.Empty,
        }
        : string.Empty;

    public static string FormatParticipantPositionStatus(
        bool hasPosition,
        bool isOnSelectedMap,
        TimeSpan age)
        => FormatParticipantPositionStatus(hasPosition, isOnSelectedMap, age, null);

    public static string FormatParticipantPositionStatus(
        bool hasPosition,
        bool isOnSelectedMap,
        TimeSpan age,
        string? positionMapName)
    {
        if (!hasPosition)
        {
            return "位置未受信";
        }

        if (!isOnSelectedMap)
        {
            return string.IsNullOrWhiteSpace(positionMapName)
                ? "別マップ"
                : $"別マップ: {positionMapName}";
        }

        var wholeSeconds = Math.Max(0, Math.Floor(age.TotalSeconds));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{wholeSeconds:0}秒前");
    }
}

public enum PartyUiRole
{
    NotJoined,
    Host,
    Participant,
}
