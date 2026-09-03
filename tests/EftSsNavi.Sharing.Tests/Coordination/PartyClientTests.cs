using EftSsNavi.Sharing.Coordination;
using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Session;

namespace EftSsNavi.Sharing.Tests.Coordination;

public sealed class PartyClientTests
{
    private static readonly Guid HostId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ClientId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task ShouldCompleteJoinAfterHelloAndWelcome()
    {
        // Given: Signaling returns an answer and the host welcomes the sent Hello.
        var fixture = CreateFixture();
        fixture.Peer.OnSendAsync = message =>
        {
            var hello = Assert.IsType<HelloMessage>(message);
            Assert.Equal("Alice", hello.DisplayName);
            Assert.Equal(ProtocolJson.CurrentVersion, hello.ProtocolVersion);
            return fixture.Peer.ReceiveAsync(CreateWelcome());
        };

        // When: The participant joins with a room code.
        await fixture.Client.JoinAsync();

        // Then: The negotiated participant state is exposed to the application.
        Assert.Equal(PartyCoordinatorRole.Participant, fixture.Client.State.Role);
        Assert.Equal(ClientId, fixture.Client.State.LocalParticipantId);
        Assert.Equal("Alice", fixture.Client.State.LocalDisplayName);
        Assert.Equal("Woods", fixture.Client.State.MapName);
        Assert.Equal(2, fixture.Client.State.Participants.Count);
    }

    [Fact]
    public async Task ShouldApplyOneThirtySecondTimeoutToWholeJoinOperation()
    {
        // Given: Signaling and WebRTC negotiation finish, but Welcome never arrives.
        var timeProvider = new ManualTimeProvider();
        var fixture = CreateFixture(timeProvider);
        var join = fixture.Client.JoinAsync();

        // When: Thirty seconds elapse from the beginning of JoinAsync.
        timeProvider.Advance(TimeSpan.FromSeconds(30));

        // Then: The whole operation times out and its peer is closed.
        await Assert.ThrowsAsync<TimeoutException>(() => join);
        Assert.Equal(1, fixture.Peer.DisposeCount);
        Assert.Equal(PartyCoordinatorRole.None, fixture.Client.State.Role);
    }

    [Fact]
    public async Task ShouldCancelInFlightSignalingWhenWholeJoinTimesOut()
    {
        // Given: Signaling remains in flight until its operation token is cancelled.
        var timeProvider = new ManualTimeProvider();
        var fixture = CreateFixture(timeProvider);
        var signalingCancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Signaling.ExchangeOffer = async (_, _, _, cancellationToken) =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("The infinite signaling delay unexpectedly completed.");
            }
            catch (OperationCanceledException)
            {
                signalingCancelled.SetResult();
                throw;
            }
        };
        var join = fixture.Client.JoinAsync();

        // When: The single thirty-second join deadline elapses.
        timeProvider.Advance(TimeSpan.FromSeconds(30));

        // Then: Join reports timeout and the same deadline cancels signaling work.
        await Assert.ThrowsAsync<TimeoutException>(() => join);
        await signalingCancelled.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ShouldPreserveCallerCancellationDuringJoin()
    {
        // Given: Signaling blocks until the caller cancels Join.
        var fixture = CreateFixture();
        fixture.Signaling.ExchangeOffer = async (_, _, _, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return "unreachable-answer";
        };
        using var cancellation = new CancellationTokenSource();
        var join = fixture.Client.JoinAsync(cancellation.Token);

        // When: The caller cancels independently of the join deadline.
        cancellation.Cancel();

        // Then: The standard cancellation exception is preserved.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => join);
    }

    [Fact]
    public async Task ShouldAddLateParticipantFromHostAnnouncement()
    {
        // Given: A joined participant with the initial roster.
        var fixture = CreateFixture();
        fixture.Peer.OnSendAsync = _ => fixture.Peer.ReceiveAsync(CreateWelcome());
        await fixture.Client.JoinAsync();
        var bob = new PartyParticipant(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Bob",
            2);

        // When: The host announces a later participant.
        await fixture.Peer.ReceiveAsync(new ParticipantJoinedMessage(bob));

        // Then: The application-facing roster includes the newcomer.
        Assert.Contains(fixture.Client.State.Participants, item => item.Id == bob.Id);
    }

    [Fact]
    public async Task ShouldRemoveParticipantFromHostAnnouncement()
    {
        // Given: A joined participant and a later Bob announcement.
        var fixture = CreateFixture();
        fixture.Peer.OnSendAsync = _ => fixture.Peer.ReceiveAsync(CreateWelcome());
        await fixture.Client.JoinAsync();
        var bobId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await fixture.Peer.ReceiveAsync(new ParticipantJoinedMessage(new PartyParticipant(bobId, "Bob", 2)));

        // When: The host announces that Bob left.
        await fixture.Peer.ReceiveAsync(new ParticipantLeftMessage(bobId));

        // Then: Bob and any associated latest position are removed.
        Assert.DoesNotContain(fixture.Client.State.Participants, item => item.Id == bobId);
    }

    [Fact]
    public async Task ShouldApplyHostMapChangeAndRaiseStateChanged()
    {
        // Given: A joined participant observing state changes.
        var fixture = CreateFixture();
        fixture.Peer.OnSendAsync = _ => fixture.Peer.ReceiveAsync(CreateWelcome());
        await fixture.Client.JoinAsync();
        PartyCoordinatorState? changed = null;
        fixture.Client.StateChanged += state => changed = state;

        // When: The host broadcasts a map change.
        await fixture.Peer.ReceiveAsync(new MapChangedMessage("Customs"));

        // Then: The latest state and event both expose the host-selected map.
        Assert.Equal("Customs", fixture.Client.State.MapName);
        Assert.Equal("Customs", Assert.IsType<PartyCoordinatorState>(changed).MapName);
    }

    [Fact]
    public async Task ShouldClearRemoteStateWhenGoodbyeIsReceived()
    {
        // Given: A participant with a populated party roster.
        var fixture = CreateFixture();
        fixture.Peer.OnSendAsync = _ => fixture.Peer.ReceiveAsync(CreateWelcome());
        await fixture.Client.JoinAsync();

        // When: The host ends the party normally.
        await fixture.Peer.ReceiveAsync(new GoodbyeMessage());

        // Then: The participant returns to a fully cleared non-party state.
        Assert.Equal(PartyCoordinatorRole.None, fixture.Client.State.Role);
        Assert.Null(fixture.Client.State.RoomCode);
        Assert.Null(fixture.Client.State.MapName);
        Assert.Empty(fixture.Client.State.Participants);
        Assert.Equal(1, fixture.Peer.DisposeCount);
    }

    [Fact]
    public async Task ShouldClearRemoteStateWhenHostDisconnects()
    {
        // Given: A joined participant.
        var fixture = CreateFixture();
        fixture.Peer.OnSendAsync = _ => fixture.Peer.ReceiveAsync(CreateWelcome());
        await fixture.Client.JoinAsync();

        // When: The host peer disconnects without Goodbye.
        await fixture.Peer.DisconnectAsync();

        // Then: Stale remote participants and positions are removed.
        Assert.Equal(PartyCoordinatorRole.None, fixture.Client.State.Role);
        Assert.Empty(fixture.Client.State.Participants);
    }

    [Fact]
    public async Task ShouldSendCurrentPositionThroughJoinedPeer()
    {
        // Given: A joined participant.
        var fixture = CreateFixture();
        fixture.Peer.OnSendAsync = message => message is HelloMessage
            ? fixture.Peer.ReceiveAsync(CreateWelcome())
            : Task.CompletedTask;
        await fixture.Client.JoinAsync();
        fixture.Peer.ClearSentMessages();
        var capturedAt = DateTimeOffset.Parse("2026-09-03T12:34:56Z");

        // When: The application reports the accepted position.
        await fixture.Client.SendPositionAsync(new PartyPosition(
            10,
            20,
            30,
            1,
            0,
            capturedAt,
            "Woods"));

        // Then: The peer sends only the protocol position fields under its assigned identity.
        var position = Assert.IsType<PositionMessage>(Assert.Single(fixture.Peer.SentMessages));
        Assert.Equal(ClientId, position.ParticipantId);
        Assert.Equal("Alice", position.DisplayName);
        Assert.Equal(capturedAt, position.CapturedAt);
        Assert.Equal("Woods", position.MapName);
    }

    [Fact]
    public async Task ShouldReplaceParticipantLatestPositionWhenNewPositionArrives()
    {
        // Given: A joined client with Bob in its roster.
        var fixture = CreateFixture();
        fixture.Peer.OnSendAsync = _ => fixture.Peer.ReceiveAsync(CreateWelcome());
        await fixture.Client.JoinAsync();
        var bobId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        await fixture.Peer.ReceiveAsync(new ParticipantJoinedMessage(new PartyParticipant(bobId, "Bob", 2)));

        // When: Two positions arrive for Bob.
        await fixture.Peer.ReceiveAsync(CreatePosition(bobId, "Bob", 10));
        await fixture.Peer.ReceiveAsync(CreatePosition(bobId, "Bob", 20));

        // Then: Only the latest position is retained.
        var bob = fixture.Client.State.Participants.Single(item => item.Id == bobId);
        Assert.Equal(20, bob.LatestPosition!.X);
    }

    [Fact]
    public async Task ShouldIgnoreInvalidProtocolJsonFromHost()
    {
        // Given: A joined participant.
        var fixture = CreateFixture();
        fixture.Peer.OnSendAsync = _ => fixture.Peer.ReceiveAsync(CreateWelcome());
        await fixture.Client.JoinAsync();
        var stateBefore = fixture.Client.State;

        // When: The host sends malformed protocol JSON.
        await fixture.Peer.ReceiveRawAsync("{not-json");

        // Then: Existing party state is unchanged.
        Assert.Same(stateBefore, fixture.Client.State);
    }

    [Fact]
    public async Task ShouldLeaveAndDisposeOnlyOnceWhenRequestedRepeatedly()
    {
        // Given: A joined participant.
        var fixture = CreateFixture();
        fixture.Peer.OnSendAsync = _ => fixture.Peer.ReceiveAsync(CreateWelcome());
        await fixture.Client.JoinAsync();

        // When: Leave and asynchronous disposal are requested repeatedly.
        await fixture.Client.LeaveAsync();
        await fixture.Client.LeaveAsync();
        await fixture.Client.DisposeAsync();
        await fixture.Client.DisposeAsync();

        // Then: The peer is closed once and state remains cleared.
        Assert.Equal(1, fixture.Peer.DisposeCount);
        Assert.Equal(PartyCoordinatorRole.None, fixture.Client.State.Role);
    }

    [Fact]
    public async Task ShouldNotRestoreParticipantStateFromWelcomeQueuedBeforeDisconnect()
    {
        // Given: One Welcome callback is blocked while another Welcome is queued behind it.
        var fixture = CreateFixture();
        using var enteredCallback = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        fixture.Client.StateChanged += state =>
        {
            if (state.Role == PartyCoordinatorRole.Participant && !enteredCallback.IsSet)
            {
                enteredCallback.Set();
                releaseCallback.Wait();
            }
        };
        var firstWelcome = Task.Run(() => fixture.Peer.ReceiveAsync(CreateWelcome()));
        Assert.True(enteredCallback.Wait(TimeSpan.FromSeconds(1)));
        var queuedWelcome = Task.Run(() => fixture.Peer.ReceiveAsync(CreateWelcome()));

        // When: The peer disconnects before queued message processing resumes.
        var disconnect = Task.Run(() => fixture.Peer.DisconnectAsync());
        releaseCallback.Set();
        await Task.WhenAll(firstWelcome, queuedWelcome, disconnect);

        // Then: The closed client cannot be resurrected by the stale Welcome.
        Assert.Equal(PartyCoordinatorRole.None, fixture.Client.State.Role);
        Assert.Empty(fixture.Client.State.Participants);
    }

    private static ClientFixture CreateFixture(TimeProvider? timeProvider = null)
    {
        var signaling = new FakePartySignaling();
        var peer = new FakePartyPeer();
        var peers = new FakePartyPeerFactory { NextPeer = peer };
        var client = new PartyClient(
            ClientId,
            "Alice",
            "ABCDEFGHJKLMNPQ2",
            signaling,
            peers,
            timeProvider ?? TimeProvider.System);
        return new ClientFixture(client, signaling, peer);
    }

    private static WelcomeMessage CreateWelcome() => new(
        ClientId,
        "Alice",
        1,
        "Woods",
        [
            new PartyParticipant(HostId, "Host", 0),
            new PartyParticipant(ClientId, "Alice", 1),
        ]);

    private static PositionMessage CreatePosition(Guid id, string name, double x) => new(
        id,
        name,
        x,
        2,
        3,
        1,
        0,
        DateTimeOffset.Parse("2026-09-03T00:00:00Z"),
        "Woods");

    private sealed record ClientFixture(
        PartyClient Client,
        FakePartySignaling Signaling,
        FakePartyPeer Peer);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset utcNow = DateTimeOffset.Parse("2026-09-03T00:00:00Z");

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state, utcNow + dueTime);
            timers.Add(timer);
            return timer;
        }

        public void Advance(TimeSpan elapsed)
        {
            utcNow += elapsed;
            foreach (var timer in timers.ToArray())
            {
                timer.FireIfDue(utcNow);
            }
        }

        private sealed class ManualTimer(
            ManualTimeProvider owner,
            TimerCallback callback,
            object? state,
            DateTimeOffset dueAt) : ITimer
        {
            private bool disposed;

            public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();

            public void Dispose()
            {
                disposed = true;
                owner.timers.Remove(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public void FireIfDue(DateTimeOffset now)
            {
                if (disposed || now < dueAt)
                {
                    return;
                }

                disposed = true;
                owner.timers.Remove(this);
                callback(state);
            }
        }
    }
}
