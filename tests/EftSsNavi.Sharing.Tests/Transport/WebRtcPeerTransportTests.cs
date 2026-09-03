using EftSsNavi.Sharing.Transport;
using SIPSorcery.Net;

namespace EftSsNavi.Sharing.Tests.Transport;

public sealed class WebRtcPeerTransportTests
{
    private static readonly string[] StunServers =
    [
        "stun:stun.example.com:3478",
        "stun:backup.example.com:3478",
    ];

    [Fact]
    public void ShouldUseThirtySecondConnectionTimeoutByDefault()
    {
        // Given: The production WebRTC transport policy.

        // When: Its default negotiation timeout is inspected.
        var timeout = WebRtcPeerTransport.DefaultConnectionTimeout;

        // Then: A connection attempt is limited to thirty seconds.
        Assert.Equal(TimeSpan.FromSeconds(30), timeout);
    }

    [Fact]
    public void ShouldMapOnlyExplicitStunServersIntoSipsorceryConfiguration()
    {
        // Given: The application-provided STUN server list.

        // When: A SIPSorcery configuration is built.
        var configuration = SipsorceryPeerFactory.CreateConfiguration(StunServers);

        // Then: No implicit STUN or TURN credential is introduced.
        Assert.Equal(StunServers, configuration.iceServers.Select(server => server.urls));
        Assert.All(configuration.iceServers, server =>
        {
            Assert.Null(server.username);
            Assert.Null(server.credential);
        });
    }

    [Fact]
    public void ShouldMapReliableOrderedChannelIntoSipsorceryOptions()
    {
        // Given: The party data channel reliability policy.

        // When: SIPSorcery channel options are built.
        var options = SipsorceryPeer.CreateDataChannelOptions(ordered: true, reliable: true);

        // Then: Ordering is enabled and both partial-reliability limits are absent.
        Assert.True(options.ordered);
        Assert.Null(options.maxPacketLifeTime);
        Assert.Null(options.maxRetransmits);
    }

    [Fact]
    public void ShouldCreateExactlyOnePeerUsingOnlyConfiguredStunServers()
    {
        // Given: Two explicitly configured STUN servers.
        var factory = new RecordingPeerFactory();

        // When: A WebRTC transport is constructed.
        using var transport = CreateTransport(factory);

        // Then: One peer is created with exactly those servers and no implicit entries.
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(StunServers, factory.ConfiguredStunServers);
    }

    [Fact]
    public async Task ShouldCreateOneOrderedReliableDataChannelForOfferer()
    {
        // Given: A new peer transport.
        var factory = new RecordingPeerFactory();
        using var transport = CreateTransport(factory);

        // When: The offerer starts negotiation.
        var offerTask = transport.CreateOfferAsync(CancellationToken.None);
        factory.Peer.CompleteIceGathering();
        await offerTask;

        // Then: Exactly one ordered, fully reliable party channel was requested.
        var request = Assert.Single(factory.Peer.DataChannelRequests);
        Assert.Equal("eftssnavi-party", request.Label);
        Assert.True(request.Ordered);
        Assert.True(request.Reliable);
    }

    [Fact]
    public async Task ShouldSetLocalOfferBeforeWaitingForIceAndReturnGatheredSdp()
    {
        // Given: A peer whose ICE gathering is still in progress.
        var factory = new RecordingPeerFactory();
        using var transport = CreateTransport(factory);

        // When: An offer is created but gathering has not completed.
        var offerTask = transport.CreateOfferAsync(CancellationToken.None);

        // Then: The local offer is set and no trickle/partial SDP is returned.
        Assert.Equal(
            ["CreateDataChannel", "CreateOffer", "SetLocal:Offer"],
            factory.Peer.Calls);
        Assert.False(offerTask.IsCompleted);

        // When: ICE gathering completes and the local SDP contains its candidates.
        factory.Peer.LocalDescriptionSdp = "offer-with-all-candidates";
        factory.Peer.CompleteIceGathering();

        // Then: The one complete SDP is returned for signaling.
        Assert.Equal("offer-with-all-candidates", await offerTask);
    }

    [Fact]
    public async Task ShouldApplyRemoteOfferBeforeCreatingAndGatheringAnswer()
    {
        // Given: A host-side transport and a remote offer.
        var factory = new RecordingPeerFactory();
        using var transport = CreateTransport(factory);

        // When: The host creates an answer.
        var answerTask = transport.CreateAnswerAsync("remote-offer", CancellationToken.None);
        factory.Peer.LocalDescriptionSdp = "answer-with-all-candidates";
        factory.Peer.CompleteIceGathering();

        // Then: Remote offer, answer creation, and local description happen in protocol order.
        Assert.Equal("answer-with-all-candidates", await answerTask);
        Assert.Equal(
            ["SetRemote:Offer:remote-offer", "CreateAnswer", "SetLocal:Answer"],
            factory.Peer.Calls);
    }

    [Fact]
    public async Task ShouldApplyRemoteAnswerToExistingOfferPeer()
    {
        // Given: An offer-side transport.
        var factory = new RecordingPeerFactory();
        using var transport = CreateTransport(factory);

        // When: Its remote answer arrives.
        await transport.ApplyAnswerAsync("remote-answer", CancellationToken.None);

        // Then: The answer is set as the remote description.
        Assert.Equal(["SetRemote:Answer:remote-answer"], factory.Peer.Calls);
    }

    [Fact]
    public async Task ShouldForwardTextThroughDataChannel()
    {
        // Given: A negotiated peer transport.
        var factory = new RecordingPeerFactory();
        using var transport = CreateTransport(factory);

        // When: Protocol JSON is sent.
        await transport.SendAsync("{\"type\":\"Goodbye\"}", CancellationToken.None);

        // Then: The exact UTF-8 text is handed to the peer data channel.
        Assert.Equal("{\"type\":\"Goodbye\"}", Assert.Single(factory.Peer.SentMessages));
    }

    [Fact]
    public async Task ShouldRejectSendBeforeDataChannelIsOpen()
    {
        // Given: A peer whose data channel has not opened.
        var factory = new RecordingPeerFactory();
        factory.Peer.IsDataChannelOpen = false;
        using var transport = CreateTransport(factory);

        // When: A consumer attempts to send protocol JSON.
        var action = () => transport.SendAsync("{\"type\":\"Hello\"}", CancellationToken.None);

        // Then: The unsafe early send is rejected instead of being handed to SIPSorcery.
        await Assert.ThrowsAsync<InvalidOperationException>(action);
        Assert.Empty(factory.Peer.SentMessages);
    }

    [Fact]
    public async Task ShouldWaitUntilDataChannelOpensBeforeReportingConnected()
    {
        // Given: A peer with a negotiated but unopened data channel.
        var factory = new RecordingPeerFactory();
        factory.Peer.IsDataChannelOpen = false;
        using var transport = CreateTransport(factory);

        // When: Connection readiness is awaited.
        var connectionTask = transport.WaitUntilConnectedAsync(CancellationToken.None);

        // Then: Readiness remains pending until the channel raises open.
        Assert.False(connectionTask.IsCompleted);
        factory.Peer.OpenDataChannel();
        await connectionTask;
    }

    [Fact]
    public async Task ShouldObserveDataChannelThatOpenedBeforeReadinessSubscription()
    {
        // Given: A data channel that won the race and is already open.
        var factory = new RecordingPeerFactory();
        factory.Peer.IsDataChannelOpen = true;
        using var transport = CreateTransport(factory);

        // When: Connection readiness is awaited after the open event.
        var connectionTask = transport.WaitUntilConnectedAsync(CancellationToken.None);

        // Then: The state check completes without waiting for a second event.
        await connectionTask;
    }

    [Fact]
    public async Task ShouldFailConnectionWaitWhenPeerDisconnectsBeforeChannelOpens()
    {
        // Given: A peer whose data channel is not open.
        var factory = new RecordingPeerFactory();
        factory.Peer.IsDataChannelOpen = false;
        using var transport = CreateTransport(factory);

        // When: The peer disconnects while readiness is awaited.
        var connectionTask = transport.WaitUntilConnectedAsync(CancellationToken.None);
        factory.Peer.Disconnect();

        // Then: The wait fails instead of reporting a usable connection.
        await Assert.ThrowsAsync<InvalidOperationException>(() => connectionTask);
    }

    [Fact]
    public async Task ShouldTimeOutWhenDataChannelDoesNotOpen()
    {
        // Given: A peer whose data channel remains unopened and a short test timeout.
        var factory = new RecordingPeerFactory();
        factory.Peer.IsDataChannelOpen = false;
        using var transport = CreateTransport(factory, TimeSpan.FromMilliseconds(50));

        // When: Connection readiness exceeds the configured equivalent of the 30-second policy.
        var action = () => transport.WaitUntilConnectedAsync(CancellationToken.None);

        // Then: The peer is closed with a timeout failure.
        await Assert.ThrowsAsync<TimeoutException>(action);
        Assert.Equal(1, factory.Peer.DisposeCount);
    }

    [Fact]
    public async Task ShouldHonorCallerCancellationWhileWaitingForDataChannelOpen()
    {
        // Given: A peer whose data channel remains unopened.
        var factory = new RecordingPeerFactory();
        factory.Peer.IsDataChannelOpen = false;
        using var cancellation = new CancellationTokenSource();
        using var transport = CreateTransport(factory);

        // When: The caller cancels the readiness wait.
        var connectionTask = transport.WaitUntilConnectedAsync(cancellation.Token);
        cancellation.Cancel();

        // Then: Cancellation is returned and the peer is closed.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connectionTask);
        Assert.Equal(1, factory.Peer.DisposeCount);
    }

    [Fact]
    public void ShouldForwardReceivedTextToConsumer()
    {
        // Given: A consumer subscribed to peer messages.
        var factory = new RecordingPeerFactory();
        using var transport = CreateTransport(factory);
        string? received = null;
        transport.MessageReceived += message => received = message;

        // When: Text arrives on the data channel.
        factory.Peer.Receive("{\"type\":\"Hello\"}");

        // Then: The exact message is raised once.
        Assert.Equal("{\"type\":\"Hello\"}", received);
    }

    [Fact]
    public void ShouldRaiseDisconnectedOnceWhenPeerDisconnectsRepeatedly()
    {
        // Given: A consumer observing connection loss.
        var factory = new RecordingPeerFactory();
        using var transport = CreateTransport(factory);
        var disconnectCount = 0;
        transport.Disconnected += () => disconnectCount++;

        // When: The underlying peer reports duplicate terminal transitions.
        factory.Peer.Disconnect();
        factory.Peer.Disconnect();

        // Then: Consumers receive one logical disconnection.
        Assert.Equal(1, disconnectCount);
    }

    [Fact]
    public async Task ShouldCancelNegotiationAfterThirtySecondPolicyTimeout()
    {
        // Given: A peer whose local description never completes and a short test timeout.
        var factory = new RecordingPeerFactory
        {
            Peer = new RecordingPeer { BlockSetLocalDescription = true },
        };
        using var transport = CreateTransport(factory, TimeSpan.FromMilliseconds(50));

        // When: Offer negotiation exceeds the configured equivalent of the 30-second policy.
        var action = () => transport.CreateOfferAsync(CancellationToken.None);

        // Then: The operation times out and closes the stalled peer.
        await Assert.ThrowsAsync<TimeoutException>(action);
        Assert.Equal(1, factory.Peer.DisposeCount);
    }

    [Fact]
    public async Task ShouldHonorCallerCancellationDuringNegotiation()
    {
        // Given: A peer whose local description is pending.
        var factory = new RecordingPeerFactory
        {
            Peer = new RecordingPeer { BlockSetLocalDescription = true },
        };
        using var cancellation = new CancellationTokenSource();
        using var transport = CreateTransport(factory);

        // When: The caller cancels negotiation.
        var offerTask = transport.CreateOfferAsync(cancellation.Token);
        cancellation.Cancel();

        // Then: Cancellation is observed and the peer is closed.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => offerTask);
        Assert.Equal(1, factory.Peer.DisposeCount);
    }

    [Fact]
    public async Task ShouldCancelInFlightNegotiationImmediatelyWhenDisposed()
    {
        // Given: A negotiation blocked inside the peer adapter.
        var factory = new RecordingPeerFactory
        {
            Peer = new RecordingPeer { BlockSetLocalDescription = true },
        };
        var transport = CreateTransport(factory);
        var offerTask = transport.CreateOfferAsync(CancellationToken.None);

        // When: The transport is disposed before its thirty-second timeout.
        transport.Dispose();

        // Then: The pending operation is cancelled immediately and does not linger.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => offerTask.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task ShouldCancelConnectionWaitImmediatelyWhenDisposed()
    {
        // Given: A readiness wait for a data channel that never opens.
        var factory = new RecordingPeerFactory();
        factory.Peer.IsDataChannelOpen = false;
        var transport = CreateTransport(factory);
        var connectionTask = transport.WaitUntilConnectedAsync(CancellationToken.None);

        // When: The transport is disposed.
        transport.Dispose();

        // Then: The pending readiness wait is cancelled immediately.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => connectionTask.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public async Task ShouldKeepExactlyOneOwnedResourceAndDiscardAllOthersDuringRaces()
    {
        // Given: A single-resource owner receiving concurrent channel candidates and disposal.
        var discarded = new System.Collections.Concurrent.ConcurrentBag<object>();
        var owner = new SingleResourceOwner<object>(resource => discarded.Add(resource));
        var candidates = Enumerable.Range(0, 100).Select(_ => new object()).ToArray();
        using var start = new ManualResetEventSlim(false);

        // When: Candidates race to attach while the owner is sealed for disposal.
        var attaches = candidates.Select(candidate => Task.Run(() =>
        {
            start.Wait();
            owner.TryAcquire(candidate, _ => { });
        })).ToArray();
        var dispose = Task.Run(() =>
        {
            start.Wait();
            owner.ReleaseAndSeal(_ => { });
        });
        start.Set();
        await Task.WhenAll(attaches.Append(dispose));

        // Then: No channel remains owned and every candidate was discarded exactly once.
        Assert.Null(owner.Current);
        Assert.Equal(candidates.Length, discarded.Count);
        Assert.Equal(candidates.Length, discarded.Distinct().Count());
    }

    [Fact]
    public void ShouldDisposePeerOnlyOnceWhenDisposedRepeatedly()
    {
        // Given: A WebRTC transport.
        var factory = new RecordingPeerFactory();
        var transport = CreateTransport(factory);

        // When: Cleanup is requested more than once.
        transport.Dispose();
        transport.Dispose();

        // Then: The peer and its channel resources are released idempotently.
        Assert.Equal(1, factory.Peer.DisposeCount);
    }

    private static WebRtcPeerTransport CreateTransport(
        RecordingPeerFactory factory,
        TimeSpan? timeout = null) =>
        new(StunServers, factory, timeout ?? TimeSpan.FromSeconds(30));

    private sealed class RecordingPeerFactory : IWebRtcPeerFactory
    {
        public RecordingPeer Peer { get; set; } = new();

        public int CreateCount { get; private set; }

        public IReadOnlyList<string> ConfiguredStunServers { get; private set; } = [];

        public IWebRtcPeer Create(IReadOnlyList<string> stunServers)
        {
            CreateCount++;
            ConfiguredStunServers = stunServers.ToArray();
            return Peer;
        }
    }

    private sealed class RecordingPeer : IWebRtcPeer
    {
        private readonly TaskCompletionSource _blockedLocalDescription =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public event Action? IceGatheringCompleted;

        public event Action? DataChannelOpened;

        public event Action<string>? MessageReceived;

        public event Action? Disconnected;

        public List<(string Label, bool Ordered, bool Reliable)> DataChannelRequests { get; } = [];

        public List<string> Calls { get; } = [];

        public List<string> SentMessages { get; } = [];

        public bool BlockSetLocalDescription { get; init; }

        public bool IsDataChannelOpen { get; set; } = true;

        public bool IsDisconnected { get; private set; }

        public string LocalDescriptionSdp { get; set; } = "initial-local-sdp";

        public int DisposeCount { get; private set; }

        public Task CreateDataChannelAsync(string label, bool ordered, bool reliable, CancellationToken cancellationToken)
        {
            Calls.Add("CreateDataChannel");
            DataChannelRequests.Add((label, ordered, reliable));
            return Task.CompletedTask;
        }

        public WebRtcSessionDescription CreateOffer()
        {
            Calls.Add("CreateOffer");
            return new WebRtcSessionDescription(WebRtcSessionDescriptionType.Offer, "created-offer");
        }

        public WebRtcSessionDescription CreateAnswer()
        {
            Calls.Add("CreateAnswer");
            return new WebRtcSessionDescription(WebRtcSessionDescriptionType.Answer, "created-answer");
        }

        public Task SetLocalDescriptionAsync(
            WebRtcSessionDescription description,
            CancellationToken cancellationToken)
        {
            Calls.Add($"SetLocal:{description.Type}");
            return BlockSetLocalDescription
                ? _blockedLocalDescription.Task
                : Task.CompletedTask;
        }

        public Task SetRemoteDescriptionAsync(
            WebRtcSessionDescription description,
            CancellationToken cancellationToken)
        {
            Calls.Add($"SetRemote:{description.Type}:{description.Sdp}");
            return Task.CompletedTask;
        }

        public Task SendAsync(string message, CancellationToken cancellationToken)
        {
            SentMessages.Add(message);
            return Task.CompletedTask;
        }

        public void CompleteIceGathering() => IceGatheringCompleted?.Invoke();

        public void Receive(string message) => MessageReceived?.Invoke(message);

        public void OpenDataChannel()
        {
            IsDataChannelOpen = true;
            DataChannelOpened?.Invoke();
        }

        public void Disconnect()
        {
            IsDisconnected = true;
            Disconnected?.Invoke();
        }

        public void Dispose() => DisposeCount++;
    }
}
