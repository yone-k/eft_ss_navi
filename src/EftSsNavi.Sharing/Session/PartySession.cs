namespace EftSsNavi.Sharing.Session;

public sealed class PartySession
{
    public const int MaximumParticipantCount = 5;
    public static readonly TimeSpan StaleThreshold = TimeSpan.FromSeconds(60);

    private readonly object _sync = new();
    private readonly Guid _hostId;
    private readonly TimeProvider _timeProvider;
    private readonly List<SessionParticipant> _participants;

    public PartySession(Guid hostId, string hostDisplayName, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var canonicalHostName = NormalizeDisplayName(hostDisplayName)
            ?? throw new ArgumentException("The host display name must contain 1 to 16 characters.", nameof(hostDisplayName));

        _hostId = hostId;
        _timeProvider = timeProvider;
        _participants = [new SessionParticipant(hostId, canonicalHostName, 0)];
    }

    public IReadOnlyList<SessionParticipant> Participants
    {
        get
        {
            lock (_sync)
            {
                return _participants.ToArray();
            }
        }
    }

    public bool TryJoin(Guid participantId, string requestedDisplayName, out SessionParticipant? participant)
    {
        participant = null;
        var canonicalName = NormalizeDisplayName(requestedDisplayName);
        if (canonicalName is null)
        {
            return false;
        }

        lock (_sync)
        {
            if (_participants.Count >= MaximumParticipantCount
                || _participants.Any(item => item.Id == participantId))
            {
                return false;
            }

            var displayName = ResolveDuplicateName(canonicalName);
            var colorIndex = Enumerable.Range(1, MaximumParticipantCount - 1)
                .First(color => _participants.All(item => item.ColorIndex != color));
            participant = new SessionParticipant(participantId, displayName, colorIndex);
            _participants.Add(participant);
            return true;
        }
    }

    public bool UpdatePosition(Guid participantId, PartyPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);

        lock (_sync)
        {
            var index = _participants.FindIndex(item => item.Id == participantId);
            if (index < 0)
            {
                return false;
            }

            _participants[index] = _participants[index] with
            {
                LatestPosition = position,
                PositionReceivedAt = _timeProvider.GetUtcNow(),
            };
            return true;
        }
    }

    public bool RemoveParticipant(Guid participantId)
    {
        lock (_sync)
        {
            if (participantId == _hostId)
            {
                return false;
            }

            var index = _participants.FindIndex(item => item.Id == participantId);
            if (index < 0)
            {
                return false;
            }

            _participants.RemoveAt(index);
            return true;
        }
    }

    public bool IsPositionStale(Guid participantId)
    {
        lock (_sync)
        {
            var receivedAt = _participants
                .FirstOrDefault(item => item.Id == participantId)?
                .PositionReceivedAt;
            return receivedAt.HasValue
                && _timeProvider.GetUtcNow() - receivedAt.Value > StaleThreshold;
        }
    }

    private static string? NormalizeDisplayName(string? requestedDisplayName)
    {
        var trimmed = requestedDisplayName?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed.Length > 16 ? null : trimmed;
    }

    private string ResolveDuplicateName(string requestedDisplayName)
    {
        if (_participants.All(item => !string.Equals(item.DisplayName, requestedDisplayName, StringComparison.Ordinal)))
        {
            return requestedDisplayName;
        }

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{requestedDisplayName} ({suffix})";
            if (_participants.All(item => !string.Equals(item.DisplayName, candidate, StringComparison.Ordinal)))
            {
                return candidate;
            }
        }
    }
}
