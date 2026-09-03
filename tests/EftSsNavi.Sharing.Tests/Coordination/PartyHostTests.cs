using EftSsNavi.Sharing.Coordination;
using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Session;

namespace EftSsNavi.Sharing.Tests.Coordination;

public sealed class PartyHostTests
{
    private static readonly Guid HostId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid AliceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BobId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static TheoryData<string> PositionNumericFields => new()
    {
        "x",
        "y",
        "z",
        "forwardX",
        "forwardZ",
    };

    [Fact]
    public async Task ShouldWelcomeParticipantWhenValidHelloIsReceived()
    {
        // Given: A started host and one negotiated participant peer.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.ConnectAsync(AliceId);

        // When: The participant introduces itself using the current protocol.
        await alice.ReceiveAsync(new HelloMessage("Alice", ProtocolJson.CurrentVersion));

        // Then: The host confirms the assigned identity, color, map, and roster.
        var welcome = Assert.IsType<WelcomeMessage>(Assert.Single(alice.SentMessages));
        Assert.Equal(AliceId, welcome.ParticipantId);
        Assert.Equal("Alice", welcome.DisplayName);
        Assert.Equal(1, welcome.ColorIndex);
        Assert.Equal("Woods", welcome.MapName);
        Assert.Equal([HostId, AliceId], welcome.Participants.Select(item => item.Id));
    }

    [Fact]
    public async Task ShouldRejectParticipantWhenPartyIsFull()
    {
        // Given: A host with all four participant slots occupied.
        var fixture = await HostFixture.StartAsync();
        foreach (var index in Enumerable.Range(1, 4))
        {
            var participantId = Guid.Parse($"00000000-0000-0000-0000-{index:D12}");
            var peer = await fixture.ConnectAsync(participantId);
            await peer.ReceiveAsync(new HelloMessage($"P{index}", ProtocolJson.CurrentVersion));
        }

        // When: A fifth participant negotiates and sends Hello.
        var rejected = await fixture.ConnectAsync(BobId);
        await rejected.ReceiveAsync(new HelloMessage("TooMany", ProtocolJson.CurrentVersion));

        // Then: Full is returned and the temporary rejection peer is closed.
        var reject = Assert.IsType<RejectMessage>(Assert.Single(rejected.SentMessages));
        Assert.Equal(RejectReason.Full, reject.Reason);
        Assert.Equal(1, rejected.DisposeCount);
        Assert.Equal(5, fixture.Host.State.Participants.Count);
    }

    [Fact]
    public async Task ShouldRejectParticipantWhenProtocolVersionDiffers()
    {
        // Given: A host and a negotiated participant peer.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.ConnectAsync(AliceId);

        // When: Hello declares an unsupported protocol version.
        await alice.ReceiveAsync(new HelloMessage("Alice", ProtocolJson.CurrentVersion + 1));

        // Then: VersionMismatch is returned and the peer is closed.
        var reject = Assert.IsType<RejectMessage>(Assert.Single(alice.SentMessages));
        Assert.Equal(RejectReason.VersionMismatch, reject.Reason);
        Assert.Equal(1, alice.DisposeCount);
    }

    [Fact]
    public async Task ShouldAnnounceLateParticipantToExistingPeers()
    {
        // Given: Alice is already in the host party.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");
        alice.ClearSentMessages();

        // When: Bob joins later.
        await fixture.JoinAsync(BobId, "Bob");

        // Then: Alice receives Bob's canonical participant identity.
        var joined = Assert.IsType<ParticipantJoinedMessage>(Assert.Single(alice.SentMessages));
        Assert.Equal(BobId, joined.Participant.Id);
        Assert.Equal("Bob", joined.Participant.DisplayName);
        Assert.Equal(2, joined.Participant.ColorIndex);
    }

    [Fact]
    public async Task ShouldAllowParticipantToRejoinAfterDisconnect()
    {
        // Given: Alice joined and then disconnected.
        var fixture = await HostFixture.StartAsync();
        var original = await fixture.JoinAsync(AliceId, "Alice");
        await original.DisconnectAsync();

        // When: A new peer reconnects with the same participant id and name.
        var replacement = await fixture.ConnectAsync(AliceId);
        await replacement.ReceiveAsync(new HelloMessage("Alice", ProtocolJson.CurrentVersion));

        // Then: The participant is admitted again with the released first color.
        var welcome = Assert.IsType<WelcomeMessage>(Assert.Single(replacement.SentMessages));
        Assert.Equal(AliceId, welcome.ParticipantId);
        Assert.Equal(1, welcome.ColorIndex);
    }

    [Fact]
    public async Task ShouldOverwriteSpoofedPositionIdentityBeforeRelay()
    {
        // Given: Alice and Bob are joined through distinct authenticated peers.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");
        var bob = await fixture.JoinAsync(BobId, "Bob");
        alice.ClearSentMessages();
        bob.ClearSentMessages();

        // When: Alice submits a position claiming Bob's identity and display name.
        await alice.ReceiveAsync(new PositionMessage(
            BobId,
            "Impostor",
            10,
            20,
            30,
            1,
            0,
            DateTimeOffset.Parse("2026-09-03T00:00:00Z"),
            "Woods"));

        // Then: Bob receives the coordinates with Alice's host-managed identity.
        var relayed = Assert.IsType<PositionMessage>(Assert.Single(bob.SentMessages));
        Assert.Equal(AliceId, relayed.ParticipantId);
        Assert.Equal("Alice", relayed.DisplayName);
        Assert.Equal((10, 20, 30), (relayed.X, relayed.Y, relayed.Z));
        Assert.Empty(alice.SentMessages);
    }

    [Theory]
    [MemberData(nameof(PositionNumericFields))]
    public async Task ShouldIgnorePositionWhenAnyNumericFieldIsNotFinite(string fieldName)
    {
        // Given: Alice and Bob are joined and Bob is observing relays.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");
        var bob = await fixture.JoinAsync(BobId, "Bob");
        bob.ClearSentMessages();
        var values = new Dictionary<string, string>
        {
            ["x"] = "10",
            ["y"] = "20",
            ["z"] = "30",
            ["forwardX"] = "1",
            ["forwardZ"] = "0",
        };
        values[fieldName] = "1e999";
        var nonFinitePosition =
            $$"""
            {"type":"Position","participantId":"22222222-2222-2222-2222-222222222222","displayName":"Impostor","x":{{values["x"]}},"y":{{values["y"]}},"z":{{values["z"]}},"forwardX":{{values["forwardX"]}},"forwardZ":{{values["forwardZ"]}},"capturedAt":"2026-09-03T00:00:00Z","mapName":"Woods"}
            """;

        // When: Alice submits JSON whose X coordinate is not finite.
        await alice.ReceiveRawAsync(nonFinitePosition);

        // Then: The invalid position is neither stored nor relayed.
        Assert.Empty(bob.SentMessages);
        Assert.Null(fixture.Host.State.Participants.Single(item => item.Id == AliceId).LatestPosition);
    }

    [Fact]
    public async Task ShouldBroadcastMapChangedToEveryParticipant()
    {
        // Given: Two participants are joined to the host.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");
        var bob = await fixture.JoinAsync(BobId, "Bob");
        alice.ClearSentMessages();
        bob.ClearSentMessages();

        // When: The host selects another map.
        await fixture.Host.ChangeMapAsync("Customs");

        // Then: Every peer receives the same MapChanged message.
        Assert.Equal("Customs", Assert.IsType<MapChangedMessage>(Assert.Single(alice.SentMessages)).MapName);
        Assert.Equal("Customs", Assert.IsType<MapChangedMessage>(Assert.Single(bob.SentMessages)).MapName);
    }

    [Fact]
    public async Task ShouldBroadcastParticipantLeftWhenPeerDisconnects()
    {
        // Given: Alice and Bob are joined.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");
        var bob = await fixture.JoinAsync(BobId, "Bob");
        bob.ClearSentMessages();

        // When: Alice's peer disconnects.
        await alice.DisconnectAsync();

        // Then: Bob is notified and Alice is removed from host state.
        Assert.Equal(AliceId, Assert.IsType<ParticipantLeftMessage>(Assert.Single(bob.SentMessages)).ParticipantId);
        Assert.DoesNotContain(fixture.Host.State.Participants, item => item.Id == AliceId);
    }

    [Fact]
    public async Task ShouldBroadcastGoodbyeBeforeHostEndsParty()
    {
        // Given: A host with one participant.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");
        alice.ClearSentMessages();

        // When: The host ends the party normally.
        await fixture.Host.EndAsync();

        // Then: Goodbye is sent before the peer and signaling are closed.
        Assert.IsType<GoodbyeMessage>(Assert.Single(alice.SentMessages));
        Assert.Equal(1, alice.DisposeCount);
        Assert.Equal(1, fixture.Signaling.StopCount);
    }

    [Fact]
    public async Task ShouldReissueRoomCodeWithoutClosingEstablishedPeers()
    {
        // Given: A host with an established participant peer.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");

        // When: The host reissues the room code.
        await fixture.Host.ReissueRoomCodeAsync("ZXCVBNM23456789A");

        // Then: Signaling moves to the new code while the WebRTC peer remains active.
        Assert.Equal(["ZXCVBNM23456789A"], fixture.Signaling.ReissuedRoomCodes);
        Assert.Equal("ZXCVBNM23456789A", fixture.Host.State.RoomCode);
        Assert.Equal(0, alice.DisposeCount);
    }

    [Fact]
    public async Task ShouldHideRoomCodeWhileReissueIsInProgress()
    {
        // Given: A host whose signaling switch is paused.
        var fixture = await HostFixture.StartAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Signaling.ReissueHostRoom = async (_, _) =>
        {
            entered.SetResult();
            await release.Task;
        };

        // When: Room-code reissue begins but has not completed.
        var reissue = fixture.Host.ReissueRoomCodeAsync("ZXCVBNM23456789A");
        await entered.Task;

        // Then: Hosting continues but no joinable code is advertised during the switch.
        Assert.Equal(PartyCoordinatorRole.Host, fixture.Host.State.Role);
        Assert.Null(fixture.Host.State.RoomCode);
        release.SetResult();
        await reissue;
    }

    [Fact]
    public async Task ShouldKeepRoomCodeHiddenWhenReissueFails()
    {
        // Given: An active host whose new Worker room cannot be registered.
        var fixture = await HostFixture.StartAsync();
        fixture.Signaling.ReissueHostRoom = (_, _) => Task.FromException(new PartySignalingException("failed"));

        // When: Room-code reissue fails.
        await Assert.ThrowsAsync<PartySignalingException>(() =>
            fixture.Host.ReissueRoomCodeAsync("ZXCVBNM23456789A"));

        // Then: The role and established party remain while new joins stay disabled.
        Assert.Equal(PartyCoordinatorRole.Host, fixture.Host.State.Role);
        Assert.Null(fixture.Host.State.RoomCode);
        Assert.Single(fixture.Host.State.Participants);
    }

    [Fact]
    public async Task ShouldBroadcastHostPositionToEveryParticipant()
    {
        // Given: Two participants are joined to the host.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");
        var bob = await fixture.JoinAsync(BobId, "Bob");
        alice.ClearSentMessages();
        bob.ClearSentMessages();
        var position = new PartyPosition(10, 20, 30, 1, 0, DateTimeOffset.Parse("2026-09-03T00:00:00Z"), "Woods");

        // When: The host reports its current position.
        await fixture.Host.SendPositionAsync(position);

        // Then: Every peer receives the host-managed identity and coordinates.
        foreach (var peer in new[] { alice, bob })
        {
            var sent = Assert.IsType<PositionMessage>(Assert.Single(peer.SentMessages));
            Assert.Equal(HostId, sent.ParticipantId);
            Assert.Equal("Host", sent.DisplayName);
            Assert.Equal(10, sent.X);
        }
    }

    [Fact]
    public async Task ShouldIgnoreInvalidProtocolJsonFromPeer()
    {
        // Given: Alice and Bob are joined and Bob observes relays.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");
        var bob = await fixture.JoinAsync(BobId, "Bob");
        bob.ClearSentMessages();

        // When: Alice sends malformed protocol JSON.
        await alice.ReceiveRawAsync("{not-json");

        // Then: The host remains active and sends nothing to Bob.
        Assert.Empty(bob.SentMessages);
        Assert.Equal(PartyCoordinatorRole.Host, fixture.Host.State.Role);
    }

    [Fact]
    public async Task ShouldEndAndDisposeOnlyOnceWhenRequestedRepeatedly()
    {
        // Given: A host with an established participant.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");

        // When: End and asynchronous disposal are requested repeatedly.
        await fixture.Host.EndAsync();
        await fixture.Host.EndAsync();
        await fixture.Host.DisposeAsync();
        await fixture.Host.DisposeAsync();

        // Then: Signaling and peer resources are closed once.
        Assert.Equal(1, fixture.Signaling.StopCount);
        Assert.Equal(1, alice.DisposeCount);
    }

    [Fact]
    public async Task ShouldCloseNegotiatedPeerWhenHelloDoesNotArriveWithinThirtySeconds()
    {
        // Given: A negotiated peer that never sends Hello.
        var timeProvider = new ManualTimeProvider();
        var fixture = await HostFixture.StartAsync(timeProvider);
        var silentPeer = await fixture.ConnectAsync(AliceId);

        // When: Thirty seconds elapse after negotiation.
        timeProvider.Advance(TimeSpan.FromSeconds(30));

        // Then: The unauthenticated peer is cleaned up without entering the roster.
        Assert.True(SpinWait.SpinUntil(
            () => silentPeer.DisposeCount == 1,
            TimeSpan.FromSeconds(1)));
        Assert.DoesNotContain(fixture.Host.State.Participants, item => item.Id == AliceId);
    }

    [Fact]
    public async Task ShouldLimitNegotiatedAndPendingParticipantPeersToFour()
    {
        // Given: Four participant offers are negotiated but have not sent Hello.
        var fixture = await HostFixture.StartAsync();
        foreach (var index in Enumerable.Range(1, 4))
        {
            var participantId = Guid.Parse($"00000000-0000-0000-0000-{index:D12}");
            Assert.Equal("local-answer", await fixture.Signaling.DeliverOfferAsync(participantId, "offer"));
        }

        // When: A fifth distinct participant submits an offer.
        var answer = await fixture.Signaling.DeliverOfferAsync(BobId, "offer");

        // Then: No fifth WebRTC peer is allocated.
        Assert.Null(answer);
        Assert.Equal(4, fixture.Peers.CreatedPeers.Count);
    }

    [Fact]
    public async Task ShouldAllowOnlyOneTemporaryRejectionPeerWhenManyOffersHitFullParty()
    {
        // Given: All four established participant slots are occupied.
        var fixture = await HostFixture.StartAsync();
        foreach (var index in Enumerable.Range(1, 4))
        {
            var participantId = Guid.Parse($"00000000-0000-0000-0000-{index:D12}");
            await fixture.JoinAsync(participantId, $"P{index}");
        }

        // When: Many distinct offers arrive concurrently while the party is full.
        var offers = Enumerable.Range(100, 20)
            .Select(index => fixture.Signaling.DeliverOfferAsync(
                Guid.Parse($"00000000-0000-0000-0000-{index:D12}"),
                "offer"));
        var answers = await Task.WhenAll(offers);

        // Then: Only one bounded peer is allocated to communicate Full.
        Assert.Single(answers, answer => answer == "local-answer");
        Assert.Equal(5, fixture.Peers.CreatedPeers.Count);
    }

    [Fact]
    public async Task ShouldNotReplaceAcceptedPeerWhenDuplicateParticipantOfferArrives()
    {
        // Given: Alice already occupies an authenticated participant peer.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");

        // When: Another offer claims Alice's participant id.
        var answer = await fixture.Signaling.DeliverOfferAsync(AliceId, "replacement-offer");

        // Then: The established peer and roster entry remain authoritative.
        Assert.Null(answer);
        Assert.Equal(0, alice.DisposeCount);
        Assert.Contains(fixture.Host.State.Participants, participant => participant.Id == AliceId);
        Assert.Single(fixture.Peers.CreatedPeers);
    }

    [Fact]
    public async Task ShouldRollbackParticipantAcceptanceWhenWelcomeCannotBeSent()
    {
        // Given: A negotiated peer whose Welcome send fails.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.ConnectAsync(AliceId);
        alice.OnSendAsync = message => message is WelcomeMessage
            ? Task.FromException(new InvalidOperationException("send failed"))
            : Task.CompletedTask;

        // When: Alice sends a valid Hello.
        await alice.ReceiveAsync(new HelloMessage("Alice", ProtocolJson.CurrentVersion));

        // Then: The failed admission releases its peer and participant slot.
        Assert.Equal(1, alice.DisposeCount);
        Assert.DoesNotContain(fixture.Host.State.Participants, participant => participant.Id == AliceId);
    }

    [Fact]
    public async Task ShouldContinueBroadcastAndRemovePeerWhenOneSendFails()
    {
        // Given: Alice's channel fails while Bob remains healthy.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");
        var bob = await fixture.JoinAsync(BobId, "Bob");
        alice.ClearSentMessages();
        bob.ClearSentMessages();
        alice.OnSendAsync = message => message is MapChangedMessage
            ? Task.FromException(new InvalidOperationException("channel failed"))
            : Task.CompletedTask;

        // When: The host broadcasts a map change.
        await fixture.Host.ChangeMapAsync("Customs");

        // Then: Bob receives it and the failed peer is removed from canonical state.
        Assert.Contains(bob.SentMessages, message => message is MapChangedMessage { MapName: "Customs" });
        Assert.Equal(1, alice.DisposeCount);
        Assert.DoesNotContain(fixture.Host.State.Participants, participant => participant.Id == AliceId);
        Assert.Equal("Customs", fixture.Host.State.MapName);
    }

    [Fact]
    public async Task ShouldRunHostCleanupOnlyOnceWhenSignalingStopFails()
    {
        // Given: Host signaling fails while stopping an established party.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");
        fixture.Signaling.Stop = _ => Task.FromException(new InvalidOperationException("stop failed"));

        // When: End is requested twice after the cleanup failure.
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.EndAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.EndAsync());

        // Then: Peer and state cleanup completed once even though signaling reported failure.
        Assert.Equal(1, fixture.Signaling.StopCount);
        Assert.Equal(1, alice.DisposeCount);
        Assert.Equal(PartyCoordinatorRole.None, fixture.Host.State.Role);
    }

    [Fact]
    public async Task ShouldContinueHostCleanupAfterEndCallerIsCancelled()
    {
        // Given: Signaling stop is paused after host cleanup begins.
        var fixture = await HostFixture.StartAsync();
        var alice = await fixture.JoinAsync(AliceId, "Alice");
        var enteredStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStop = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Signaling.Stop = async _ =>
        {
            enteredStop.SetResult();
            await releaseStop.Task;
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // When: A cancelled caller initiates End while cleanup is still running.
        var end = fixture.Host.EndAsync(cancellation.Token);
        await enteredStop.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => end);
        releaseStop.SetResult();
        await fixture.Host.DisposeAsync();

        // Then: The shared cleanup still closes every owned resource once.
        Assert.Equal(1, fixture.Signaling.StopCount);
        Assert.Equal(1, fixture.Signaling.DisposeCount);
        Assert.Equal(1, alice.DisposeCount);
        Assert.Equal(PartyCoordinatorRole.None, fixture.Host.State.Role);
    }

    private sealed class HostFixture
    {
        private HostFixture(PartyHost host, FakePartySignaling signaling, FakePartyPeerFactory peers)
        {
            Host = host;
            Signaling = signaling;
            Peers = peers;
        }

        public PartyHost Host { get; }

        public FakePartySignaling Signaling { get; }

        public FakePartyPeerFactory Peers { get; }

        public static async Task<HostFixture> StartAsync(TimeProvider? timeProvider = null)
        {
            var signaling = new FakePartySignaling();
            var peers = new FakePartyPeerFactory();
            var host = new PartyHost(
                HostId,
                "Host",
                "ABCDEFGHJKLMNPQ2",
                "Woods",
                signaling,
                peers,
                timeProvider ?? TimeProvider.System);
            await host.StartAsync();
            return new HostFixture(host, signaling, peers);
        }

        public async Task<FakePartyPeer> ConnectAsync(Guid participantId)
        {
            var answer = await Signaling.DeliverOfferAsync(participantId, "remote-offer");
            Assert.Equal("local-answer", answer);
            return Peers.ByParticipantId[participantId];
        }

        public async Task<FakePartyPeer> JoinAsync(Guid participantId, string displayName)
        {
            var peer = await ConnectAsync(participantId);
            await peer.ReceiveAsync(new HelloMessage(displayName, ProtocolJson.CurrentVersion));
            return peer;
        }
    }


    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset utcNow = DateTimeOffset.Parse("2026-09-03T00:00:00Z");

        public override DateTimeOffset GetUtcNow() => utcNow;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
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

        private sealed class ManualTimer(ManualTimeProvider owner, TimerCallback callback, object? state, DateTimeOffset dueAt) : ITimer
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
