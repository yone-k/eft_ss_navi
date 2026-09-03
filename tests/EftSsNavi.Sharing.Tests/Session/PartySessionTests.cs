using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Session;

namespace EftSsNavi.Sharing.Tests.Session;

public sealed class PartySessionTests
{
    private static readonly Guid HostId = Guid.Parse("4418cdb5-ad06-419a-b83a-f099af078f90");
    private static readonly DateTimeOffset InitialTime = DateTimeOffset.Parse("2026-09-03T00:00:00Z");

    [Fact]
    public void ShouldIncludeHostWithColorZeroWhenSessionIsCreated()
    {
        // Given: A newly created host session.
        var session = CreateSession();

        // When: Its participant snapshot is read.
        var participant = Assert.Single(session.Participants);

        // Then: The host occupies the first color.
        Assert.Equal(HostId, participant.Id);
        Assert.Equal("Host", participant.DisplayName);
        Assert.Equal(0, participant.ColorIndex);
    }

    [Fact]
    public void ShouldAssignParticipantColorsInJoinOrder()
    {
        // Given: A session containing only its host.
        var session = CreateSession();

        // When: Four participants join in sequence.
        var joined = Enumerable.Range(1, 4)
            .Select(index => session.TryJoin(Guid.NewGuid(), $"Player {index}", out var participant) ? participant : null)
            .ToArray();

        // Then: They receive colors one through four in join order.
        Assert.Equal([1, 2, 3, 4], joined.Select(participant => participant!.ColorIndex));
    }

    [Fact]
    public void ShouldRejectSixthActiveMemberWhenHostAndFourParticipantsArePresent()
    {
        // Given: A full session of the host and four participants.
        var session = CreateSession();
        Enumerable.Range(1, 4).ToList().ForEach(index =>
            Assert.True(session.TryJoin(Guid.NewGuid(), $"Player {index}", out _)));

        // When: Another participant tries to join.
        var wasAdded = session.TryJoin(Guid.NewGuid(), "Overflow", out var participant);

        // Then: Admission is rejected and active membership stays at five.
        Assert.False(wasAdded);
        Assert.Null(participant);
        Assert.Equal(5, session.Participants.Count);
    }

    [Theory]
    [InlineData(" Alice ", "Alice")]
    [InlineData("\tBob\r\n", "Bob")]
    public void ShouldTrimDisplayNameWhenParticipantJoins(string requestedName, string expectedName)
    {
        // Given: A session and a display name with surrounding whitespace.
        var session = CreateSession();

        // When: The participant joins.
        var wasAdded = session.TryJoin(Guid.NewGuid(), requestedName, out var participant);

        // Then: The canonical display name has only its surrounding whitespace removed.
        Assert.True(wasAdded);
        Assert.Equal(expectedName, participant!.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345678901234567")]
    public void ShouldRejectInvalidDisplayNameWhenParticipantJoins(string requestedName)
    {
        // Given: A session and a blank or overlength display name.
        var session = CreateSession();

        // When: The participant tries to join.
        var wasAdded = session.TryJoin(Guid.NewGuid(), requestedName, out var participant);

        // Then: Admission is rejected without adding participant state.
        Assert.False(wasAdded);
        Assert.Null(participant);
        Assert.Single(session.Participants);
    }

    [Fact]
    public void ShouldAppendSmallestUnusedSuffixWhenDisplayNameMatchesExactly()
    {
        // Given: Existing participants using a base name and its second suffix.
        var session = CreateSession();
        Assert.True(session.TryJoin(Guid.NewGuid(), "Alice", out _));
        Assert.True(session.TryJoin(Guid.NewGuid(), "Alice", out var second));

        // When: A third exact duplicate joins.
        Assert.True(session.TryJoin(Guid.NewGuid(), "Alice", out var third));

        // Then: The host assigns consecutive minimal unused suffixes.
        Assert.Equal("Alice (2)", second!.DisplayName);
        Assert.Equal("Alice (3)", third!.DisplayName);
    }

    [Fact]
    public void ShouldTreatDisplayNameComparisonAsCaseSensitive()
    {
        // Given: A participant whose display name differs only by casing.
        var session = CreateSession();
        Assert.True(session.TryJoin(Guid.NewGuid(), "Alice", out _));

        // When: Another participant joins as lowercase alice.
        Assert.True(session.TryJoin(Guid.NewGuid(), "alice", out var participant));

        // Then: The distinct casing is preserved without a suffix.
        Assert.Equal("alice", participant!.DisplayName);
    }

    [Fact]
    public void ShouldReplaceLatestPositionInsteadOfKeepingHistory()
    {
        // Given: A joined participant with an initial position.
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateSession(timeProvider);
        var participantId = Guid.NewGuid();
        Assert.True(session.TryJoin(participantId, "Alice", out _));
        session.UpdatePosition(participantId, CreatePosition(x: 10));

        // When: A newer position is received.
        timeProvider.Advance(TimeSpan.FromSeconds(5));
        session.UpdatePosition(participantId, CreatePosition(x: 20));

        // Then: Only the latest point and its local receive time remain.
        var participant = Assert.Single(session.Participants, item => item.Id == participantId);
        Assert.Equal(20, participant.LatestPosition!.X);
        Assert.Equal(InitialTime.AddSeconds(5), participant.PositionReceivedAt);
    }

    [Fact]
    public void ShouldRemoveParticipantAndLatestPositionWhenParticipantLeaves()
    {
        // Given: A joined participant with a current position.
        var session = CreateSession();
        var participantId = Guid.NewGuid();
        Assert.True(session.TryJoin(participantId, "Alice", out _));
        session.UpdatePosition(participantId, CreatePosition(x: 10));

        // When: The participant leaves.
        var wasRemoved = session.RemoveParticipant(participantId);

        // Then: Both membership and marker-producing position state are gone.
        Assert.True(wasRemoved);
        Assert.DoesNotContain(session.Participants, participant => participant.Id == participantId);
    }

    [Fact]
    public void ShouldAllowParticipantToRejoinAfterLeaving()
    {
        // Given: A participant who left a session after publishing a position.
        var session = CreateSession();
        var participantId = Guid.NewGuid();
        Assert.True(session.TryJoin(participantId, "Alice", out _));
        session.UpdatePosition(participantId, CreatePosition(x: 10));
        Assert.True(session.RemoveParticipant(participantId));

        // When: The same participant ID explicitly rejoins.
        var wasAdded = session.TryJoin(participantId, "Alice", out var participant);

        // Then: A fresh participant state is admitted without the old marker.
        Assert.True(wasAdded);
        Assert.NotNull(participant);
        Assert.Null(participant.LatestPosition);
        Assert.Null(participant.PositionReceivedAt);
    }

    [Fact]
    public void ShouldKeepPositionFreshAtExactlySixtySeconds()
    {
        // Given: A position received exactly sixty seconds ago.
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateSession(timeProvider);
        var participantId = Guid.NewGuid();
        Assert.True(session.TryJoin(participantId, "Alice", out _));
        session.UpdatePosition(participantId, CreatePosition(x: 10));
        timeProvider.Advance(TimeSpan.FromSeconds(60));

        // When: Freshness is evaluated at the boundary.
        var isStale = session.IsPositionStale(participantId);

        // Then: The position is not stale until it exceeds sixty seconds.
        Assert.False(isStale);
    }

    [Fact]
    public void ShouldMarkPositionStaleWhenOlderThanSixtySeconds()
    {
        // Given: A position received more than sixty seconds ago.
        var timeProvider = new ManualTimeProvider(InitialTime);
        var session = CreateSession(timeProvider);
        var participantId = Guid.NewGuid();
        Assert.True(session.TryJoin(participantId, "Alice", out _));
        session.UpdatePosition(participantId, CreatePosition(x: 10));
        timeProvider.Advance(TimeSpan.FromSeconds(60) + TimeSpan.FromTicks(1));

        // When: Freshness is evaluated.
        var isStale = session.IsPositionStale(participantId);

        // Then: The position is stale according to local receive time.
        Assert.True(isStale);
    }

    [Fact]
    public async Task ShouldNeverExceedFiveActiveMembersWhenJoinsAreConcurrent()
    {
        // Given: A host session receiving many Hello admissions at once.
        var session = CreateSession();
        using var start = new ManualResetEventSlim(false);

        // When: Twenty participants concurrently attempt to join.
        var attempts = Enumerable.Range(1, 20).Select(index => Task.Run(() =>
        {
            start.Wait();
            return session.TryJoin(Guid.NewGuid(), $"Player {index}", out _);
        })).ToArray();
        start.Set();
        var results = await Task.WhenAll(attempts);

        // Then: Exactly four join the host and the active limit remains five.
        Assert.Equal(4, results.Count(result => result));
        Assert.Equal(5, session.Participants.Count);
    }

    private static PartySession CreateSession(TimeProvider? timeProvider = null) =>
        new(HostId, "Host", timeProvider ?? new ManualTimeProvider(InitialTime));

    private static PartyPosition CreatePosition(double x) =>
        new(
            x,
            2,
            3,
            0.6,
            -0.8,
            DateTimeOffset.Parse("2026-09-03T00:00:00Z"),
            "Customs");

    private sealed class ManualTimeProvider(DateTimeOffset initialTime) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialTime;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan elapsed) => _utcNow += elapsed;
    }
}
