using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Session;
using EftSsNavi.Sharing.Signaling;
using EftSsNavi.Sharing.Transport;

namespace EftSsNavi.Sharing.Coordination;

public enum PartyCoordinatorRole
{
    None,
    Host,
    Participant,
}

public sealed record PartyCoordinatorState(
    PartyCoordinatorRole Role,
    string? RoomCode,
    string? MapName,
    Guid? LocalParticipantId,
    string? LocalDisplayName,
    IReadOnlyList<SessionParticipant> Participants)
{
    public static PartyCoordinatorState Empty { get; } = new(
        PartyCoordinatorRole.None,
        null,
        null,
        null,
        null,
        []);
}

public interface IPartySignaling
{
    Task StartHostAsync(
        string roomCode,
        Func<Guid, string, CancellationToken, Task<string?>> offerHandler,
        CancellationToken cancellationToken = default);

    Task<string> ExchangeOfferAsync(
        string roomCode,
        Guid participantId,
        string offer,
        CancellationToken cancellationToken = default);

    Task ReissueHostRoomAsync(string roomCode, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface IPeerTransportFactory
{
    IPeerTransport Create(Guid participantId);
}

public interface IPartyCoordinator : IAsyncDisposable
{
    event Action<PartyCoordinatorState>? StateChanged;

    PartyCoordinatorState State { get; }

    Task StartHostAsync(string displayName, string? mapName, CancellationToken cancellationToken = default);

    Task JoinAsync(string displayName, string roomCode, CancellationToken cancellationToken = default);

    Task<string> ReissueRoomCodeAsync(CancellationToken cancellationToken = default);

    Task SendPositionAsync(PartyPosition position, CancellationToken cancellationToken = default);

    Task ChangeMapAsync(string? mapName, CancellationToken cancellationToken = default);

    Task LeaveAsync(CancellationToken cancellationToken = default);

    Task EndAsync(CancellationToken cancellationToken = default);
}

public sealed class PartySignalingException(
    string message,
    SignalingFailureKind? failureKind = null,
    SignalingRejectReason? rejectReason = null) : Exception(message)
{
    public SignalingFailureKind? FailureKind { get; } = failureKind;

    public SignalingRejectReason? RejectReason { get; } = rejectReason;
}

public sealed class PartyRejectedException(RejectReason reason) : Exception($"Party join was rejected: {reason}.")
{
    public RejectReason Reason { get; } = reason;
}
