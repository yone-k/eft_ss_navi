using EftSsNavi.App.Presentation;
using EftSsNavi.Sharing.Coordination;
using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Session;
using EftSsNavi.Sharing.Signaling;

namespace EftSsNavi.App.Tests.Presentation;

public sealed class PartyStatusMessagesTests
{
    [Fact]
    public void ShouldUseExactFullPartyMessage()
    {
        Assert.Equal("グループが満員です。", PartyStatusMessages.ForRejection(RejectReason.Full));
    }

    [Fact]
    public void ShouldUseExactVersionMismatchMessage()
    {
        Assert.Equal(
            "ホストとアプリのバージョンが異なります。",
            PartyStatusMessages.ForRejection(RejectReason.VersionMismatch));
    }

    [Fact]
    public void ShouldUseExactJoinTimeoutMessage()
    {
        Assert.Equal(
            "ホストに接続できませんでした。ルームコードとネットワークを確認してください。",
            PartyStatusMessages.JoinTimeout);
    }

    [Fact]
    public void ShouldUseRoleSpecificSignalingMessages()
    {
        Assert.Equal("シグナリングサーバーに接続できません。", PartyStatusMessages.HostSignalingFailure);
        Assert.Equal(
            "シグナリングサーバーに接続できません。ホストに接続できません。",
            PartyStatusMessages.ParticipantSignalingFailure);
    }

    [Fact]
    public void ShouldUseExactSessionEndedMessage()
    {
        Assert.Equal("セッションが終了しました。", PartyStatusMessages.SessionEnded);
    }

    [Theory]
    [InlineData(SignalingRejectReason.HostNotFound, "ルームコードに対応するホストが見つかりません。")]
    [InlineData(SignalingRejectReason.HostExists, "このルームコードは使用中です。もう一度［ホストとして開始］を押してください。")]
    [InlineData(SignalingRejectReason.Capacity, "接続処理中の参加者が多すぎます。しばらく待ってから再試行してください。")]
    [InlineData(SignalingRejectReason.RateLimited, "接続要求が多すぎます。しばらく待ってから再試行してください。")]
    public void ShouldDefineExactWorkerRejectionMessage(SignalingRejectReason reason, string expected)
    {
        // Given: A Worker rejection name exposed by the signaling layer.

        // When: The corresponding UI message is resolved without depending on transport wording.
        var actual = PartyStatusMessages.ForSignalingRejection(reason);

        // Then: The Issue-defined actionable message is returned.
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ShouldUseExactRoomCodeReissueFailureMessage()
    {
        // Given: A host failed to register a replacement room.

        // When: The dedicated status text is read.
        var message = PartyStatusMessages.RoomCodeReissueFailure;

        // Then: The user is invited to explicitly retry.
        Assert.Equal("ルームコードを再発行できません。もう一度お試しください。", message);
    }

    [Fact]
    public void ShouldDescribeRemoteParticipantJoiningAnActiveParty()
    {
        var localId = Guid.NewGuid();
        var remoteId = Guid.NewGuid();
        var previous = State(localId, [Participant(localId, "自分")]);
        var current = State(localId, [Participant(localId, "自分"), Participant(remoteId, "Alpha")]);

        var message = PartyStatusMessages.ForMembershipChange(previous, current);

        Assert.Equal("Alphaが参加しました。", message);
    }

    [Fact]
    public void ShouldTreatRemoteDisconnectionAsLeavingTheParty()
    {
        var localId = Guid.NewGuid();
        var remoteId = Guid.NewGuid();
        var previous = State(localId, [Participant(localId, "自分"), Participant(remoteId, "Alpha")]);
        var current = State(localId, [Participant(localId, "自分")]);

        var message = PartyStatusMessages.ForMembershipChange(previous, current);

        Assert.Equal("Alphaが退出しました。", message);
    }

    [Fact]
    public void ShouldNotReportInitialRosterAsNewJoins()
    {
        var localId = Guid.NewGuid();
        var current = State(localId, [Participant(localId, "自分"), Participant(Guid.NewGuid(), "Alpha")]);

        var message = PartyStatusMessages.ForMembershipChange(PartyCoordinatorState.Empty, current);

        Assert.Null(message);
    }

    private static PartyCoordinatorState State(Guid localId, IReadOnlyList<SessionParticipant> participants) => new(
        PartyCoordinatorRole.Participant,
        "ABCDEFGHJKLMNPQR",
        "Woods",
        localId,
        "自分",
        participants);

    private static SessionParticipant Participant(Guid id, string name) => new(id, name, 0);
}
