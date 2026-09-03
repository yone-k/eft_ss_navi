using EftSsNavi.Sharing.Coordination;
using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Signaling;

namespace EftSsNavi.App.Presentation;

public static class PartyStatusMessages
{
    public const string HostSignalingFailure = "シグナリングサーバーに接続できません。";
    public const string ParticipantSignalingFailure =
        "シグナリングサーバーに接続できません。ホストに接続できません。";
    public const string JoinTimeout =
        "ホストに接続できませんでした。ルームコードとネットワークを確認してください。";
    public const string SessionEnded = "セッションが終了しました。";
    public const string RoomCodeReissueFailure =
        "ルームコードを再発行できません。もう一度お試しください。";

    public static string ForSignalingRejection(SignalingRejectReason reason) => reason switch
    {
        SignalingRejectReason.HostNotFound => "ルームコードに対応するホストが見つかりません。",
        SignalingRejectReason.HostExists => "このルームコードは使用中です。もう一度［ホストとして開始］を押してください。",
        SignalingRejectReason.Capacity => "接続処理中の参加者が多すぎます。しばらく待ってから再試行してください。",
        SignalingRejectReason.RateLimited => "接続要求が多すぎます。しばらく待ってから再試行してください。",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    public static string ForRejection(RejectReason reason) => reason switch
    {
        RejectReason.Full => "パーティが満員です。",
        RejectReason.VersionMismatch => "ホストとアプリのバージョンが異なります。",
        _ => "パーティへの参加が拒否されました。",
    };

    public static string? ForMembershipChange(
        PartyCoordinatorState previous,
        PartyCoordinatorState current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        if (previous.Role == PartyCoordinatorRole.None
            || previous.Role != current.Role
            || previous.LocalParticipantId != current.LocalParticipantId)
        {
            return null;
        }

        var previousIds = previous.Participants.Select(participant => participant.Id).ToHashSet();
        var currentIds = current.Participants.Select(participant => participant.Id).ToHashSet();
        var joinedNames = current.Participants
            .Where(participant => participant.Id != current.LocalParticipantId && !previousIds.Contains(participant.Id))
            .Select(participant => participant.DisplayName)
            .ToArray();
        var leftNames = previous.Participants
            .Where(participant => participant.Id != previous.LocalParticipantId && !currentIds.Contains(participant.Id))
            .Select(participant => participant.DisplayName)
            .ToArray();

        return (joinedNames.Length, leftNames.Length) switch
        {
            ( > 0, > 0) => $"{string.Join("、", joinedNames)}が参加し、{string.Join("、", leftNames)}が退出しました。",
            ( > 0, 0) => $"{string.Join("、", joinedNames)}が参加しました。",
            (0, > 0) => $"{string.Join("、", leftNames)}が退出しました。",
            _ => null,
        };
    }
}
