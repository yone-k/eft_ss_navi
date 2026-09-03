namespace EftSsNavi.Sharing.Session;

public sealed record SessionParticipant(
    Guid Id,
    string DisplayName,
    int ColorIndex,
    PartyPosition? LatestPosition = null,
    DateTimeOffset? PositionReceivedAt = null);
