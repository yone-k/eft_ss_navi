namespace EftSsNavi.Sharing.Protocol;

public abstract record PartyMessage;

public sealed record HelloMessage(string DisplayName, int ProtocolVersion) : PartyMessage;

public sealed record WelcomeMessage(
    Guid ParticipantId,
    string DisplayName,
    int ColorIndex,
    string? MapName,
    IReadOnlyList<PartyParticipant> Participants) : PartyMessage;

public sealed record RejectMessage(RejectReason Reason) : PartyMessage;

public sealed record PositionMessage(
    Guid ParticipantId,
    string DisplayName,
    double X,
    double Y,
    double Z,
    double? ForwardX,
    double? ForwardZ,
    DateTimeOffset CapturedAt,
    string? MapName) : PartyMessage;

public sealed record MapChangedMessage(string? MapName) : PartyMessage;

public sealed record ParticipantJoinedMessage(PartyParticipant Participant) : PartyMessage;

public sealed record ParticipantLeftMessage(Guid ParticipantId) : PartyMessage;

public sealed record GoodbyeMessage : PartyMessage;

public sealed record PartyParticipant(Guid Id, string DisplayName, int ColorIndex);

public enum RejectReason
{
    Full,
    VersionMismatch,
}
