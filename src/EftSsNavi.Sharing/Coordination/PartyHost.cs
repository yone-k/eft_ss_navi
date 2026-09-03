using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Session;
using EftSsNavi.Sharing.Transport;

namespace EftSsNavi.Sharing.Coordination;

public sealed class PartyHost : IAsyncDisposable
{
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(30);
    private readonly Guid hostId;
    private readonly string hostDisplayName;
    private readonly IPartySignaling signaling;
    private readonly IPeerTransportFactory peerFactory;
    private readonly TimeProvider timeProvider;
    private readonly PartySession session;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<Guid, PeerContext> peers = [];
    private readonly object lifecycleSync = new();
    private string? roomCode;
    private string? mapName;
    private bool started;
    private bool ended;
    private int signalingDisposed;
    private Task? endTask;

    public PartyHost(
        Guid hostId,
        string hostDisplayName,
        string roomCode,
        string? mapName,
        IPartySignaling signaling,
        IPeerTransportFactory peerFactory,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(signaling);
        ArgumentNullException.ThrowIfNull(peerFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (!RoomCode.IsValid(roomCode))
        {
            throw new ArgumentException("The room code is invalid.", nameof(roomCode));
        }

        this.hostId = hostId;
        this.signaling = signaling;
        this.peerFactory = peerFactory;
        this.timeProvider = timeProvider;
        this.roomCode = roomCode;
        this.mapName = mapName;
        session = new PartySession(hostId, hostDisplayName, timeProvider);
        this.hostDisplayName = session.Participants[0].DisplayName;
        State = BuildState();
    }

    public event Action<PartyCoordinatorState>? StateChanged;

    public PartyCoordinatorState State { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (started)
            {
                return;
            }

            await signaling.StartHostAsync(roomCode!, AcceptOfferAsync, cancellationToken)
                .ConfigureAwait(false);
            started = true;
            PublishState();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ReissueRoomCodeAsync(string newRoomCode, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ended || !started)
            {
                throw new InvalidOperationException("The host party is not active.");
            }

            roomCode = null;
            PublishState();
            await signaling.ReissueHostRoomAsync(newRoomCode, cancellationToken).ConfigureAwait(false);
            roomCode = newRoomCode;
            PublishState();
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SendPositionAsync(PartyPosition position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        ValidatePosition(position);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ended)
            {
                return;
            }

            session.UpdatePosition(hostId, position);
            PublishState();
            await BroadcastAsync(ToMessage(hostId, hostDisplayName, position), null, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ChangeMapAsync(string? newMapName, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ended)
            {
                return;
            }

            mapName = newMapName;
            PublishState();
            await BroadcastAsync(new MapChangedMessage(newMapName), null, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    public Task EndAsync(CancellationToken cancellationToken = default)
    {
        Task cleanup;
        lock (lifecycleSync)
        {
            cleanup = endTask ??= EndCoreAsync();
        }

        return cancellationToken.CanBeCanceled
            ? cleanup.WaitAsync(cancellationToken)
            : cleanup;
    }

    private async Task EndCoreAsync()
    {
        await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (ended)
            {
                return;
            }

            ended = true;
            var activePeers = peers.Values.ToArray();
            foreach (var context in activePeers.Where(item => item.Accepted))
            {
                try
                {
                    await SendAsync(context, new GoodbyeMessage(), CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // One failed Goodbye must not prevent cleanup of the remaining peers.
                }
            }

            foreach (var context in activePeers)
            {
                context.Dispose();
            }

            peers.Clear();
            try
            {
                if (started)
                {
                    await signaling.StopAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                started = false;
                State = PartyCoordinatorState.Empty;
                StateChanged?.Invoke(State);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Exception? cleanupError = null;
        try
        {
            await EndAsync().ConfigureAwait(false);
        }
        catch (Exception error)
        {
            cleanupError = error;
        }

        try
        {
            if (signaling is IAsyncDisposable asyncDisposable
                && Interlocked.Exchange(ref signalingDisposed, 1) == 0)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch when (cleanupError is not null)
        {
            // Preserve the first lifecycle failure after all owned resources were attempted.
        }

        if (cleanupError is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupError).Throw();
        }
    }

    private async Task<string?> AcceptOfferAsync(
        Guid participantId,
        string offer,
        CancellationToken cancellationToken)
    {
        IPeerTransport transport;
        PeerContext context;
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (ended)
            {
                return null;
            }

            if (peers.ContainsKey(participantId))
            {
                return null;
            }

            var maximumPeerCount = PartySession.MaximumParticipantCount - 1;
            var rejectionOnly = peers.Count == maximumPeerCount
                && peers.Values.All(peer => peer.Accepted && !peer.RejectionOnly)
                && session.Participants.Count == PartySession.MaximumParticipantCount;
            if (peers.Count >= maximumPeerCount && !rejectionOnly)
            {
                return null;
            }

            transport = peerFactory.Create(participantId);
            context = new PeerContext(participantId, transport, rejectionOnly);
            context.Attach(
                json => ExecutePeerCallback(context, json),
                () => ExecuteDisconnectCallback(context));
            peers[participantId] = context;
        }
        finally
        {
            gate.Release();
        }

        try
        {
            var answer = await transport.CreateAnswerAsync(offer, cancellationToken).ConfigureAwait(false);
            _ = ExpireSilentPeerAsync(context);
            return answer;
        }
        catch
        {
            await RemovePeerAsync(context, announce: false).ConfigureAwait(false);
            throw;
        }
    }

    private void ExecutePeerCallback(PeerContext context, string json)
    {
        try
        {
            context.ExecuteAsync(() => HandleMessageAsync(context, json)).GetAwaiter().GetResult();
        }
        catch
        {
            // Invalid or failed peer work is isolated from the transport callback thread.
        }
    }

    private void ExecuteDisconnectCallback(PeerContext context)
    {
        try
        {
            context.ExecuteAsync(() => RemovePeerAsync(context, announce: true)).GetAwaiter().GetResult();
        }
        catch
        {
            // Cleanup failures must not escape a transport event callback.
        }
    }

    private async Task HandleMessageAsync(PeerContext context, string json)
    {
        if (!ProtocolJson.TryDeserialize(json, out var message) || message is null)
        {
            return;
        }

        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (ended || !peers.TryGetValue(context.ParticipantId, out var active) || !ReferenceEquals(active, context))
            {
                return;
            }

            if (!context.Accepted)
            {
                if (message is HelloMessage hello)
                {
                    if (context.RejectionOnly)
                    {
                        try
                        {
                            await SendAsync(
                                context,
                                new RejectMessage(RejectReason.Full),
                                CancellationToken.None).ConfigureAwait(false);
                        }
                        finally
                        {
                            RemovePeerLocked(context, announce: false);
                        }
                    }
                    else
                    {
                        await HandleHelloAsync(context, hello).ConfigureAwait(false);
                    }
                }

                return;
            }

            if (message is PositionMessage position && IsFinite(position))
            {
                var participant = session.Participants.Single(item => item.Id == context.ParticipantId);
                var canonical = new PartyPosition(
                    position.X,
                    position.Y,
                    position.Z,
                    position.ForwardX,
                    position.ForwardZ,
                    position.CapturedAt,
                    position.MapName);
                session.UpdatePosition(context.ParticipantId, canonical);
                PublishState();
                await BroadcastAsync(
                    ToMessage(context.ParticipantId, participant.DisplayName, canonical),
                    context.ParticipantId,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task HandleHelloAsync(PeerContext context, HelloMessage hello)
    {
        if (hello.ProtocolVersion != ProtocolJson.CurrentVersion)
        {
            await SendAsync(context, new RejectMessage(RejectReason.VersionMismatch), CancellationToken.None)
                .ConfigureAwait(false);
            RemovePeerLocked(context, announce: false);
            return;
        }

        if (!session.TryJoin(context.ParticipantId, hello.DisplayName, out var participant)
            || participant is null)
        {
            await SendAsync(context, new RejectMessage(RejectReason.Full), CancellationToken.None)
                .ConfigureAwait(false);
            RemovePeerLocked(context, announce: false);
            return;
        }

        context.Accepted = true;
        context.CancelHelloTimeout();
        try
        {
            var roster = session.Participants
                .Select(item => new PartyParticipant(item.Id, item.DisplayName, item.ColorIndex))
                .ToArray();
            await SendAsync(
                context,
                new WelcomeMessage(
                    participant.Id,
                    participant.DisplayName,
                    participant.ColorIndex,
                    mapName,
                    roster),
                CancellationToken.None).ConfigureAwait(false);
            await BroadcastAsync(
                new ParticipantJoinedMessage(new PartyParticipant(
                    participant.Id,
                    participant.DisplayName,
                    participant.ColorIndex)),
                participant.Id,
                CancellationToken.None).ConfigureAwait(false);
            PublishState();
        }
        catch
        {
            RemovePeerLocked(context, announce: false);
            PublishState();
            throw;
        }
    }

    private async Task ExpireSilentPeerAsync(PeerContext context)
    {
        try
        {
            await Task.Delay(HelloTimeout, timeProvider, context.HelloTimeoutToken).ConfigureAwait(false);
            await RemovePeerAsync(context, announce: false).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Background timeout cleanup is fully observed here.
        }
    }

    private async Task RemovePeerAsync(PeerContext context, bool announce)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!peers.TryGetValue(context.ParticipantId, out var active) || !ReferenceEquals(active, context))
            {
                return;
            }

            var wasAccepted = context.Accepted;
            RemovePeerLocked(context, announce: false);
            if (announce && wasAccepted)
            {
                await BroadcastAsync(
                    new ParticipantLeftMessage(context.ParticipantId),
                    context.ParticipantId,
                    CancellationToken.None).ConfigureAwait(false);
                PublishState();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private void RemovePeerLocked(PeerContext context, bool announce)
    {
        peers.Remove(context.ParticipantId);
        if (context.Accepted)
        {
            session.RemoveParticipant(context.ParticipantId);
        }

        context.Dispose();
        if (announce)
        {
            PublishState();
        }
    }

    private async Task BroadcastAsync(
        PartyMessage message,
        Guid? excludedParticipantId,
        CancellationToken cancellationToken)
    {
        var failed = new List<PeerContext>();
        foreach (var context in peers.Values.Where(item =>
                     item.Accepted && item.ParticipantId != excludedParticipantId).ToArray())
        {
            try
            {
                await SendAsync(context, message, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                failed.Add(context);
            }
        }

        foreach (var context in failed)
        {
            RemovePeerLocked(context, announce: false);
        }

        if (failed.Count > 0)
        {
            PublishState();
        }

        foreach (var context in failed)
        {
            foreach (var remaining in peers.Values.Where(item => item.Accepted).ToArray())
            {
                try
                {
                    await SendAsync(
                        remaining,
                        new ParticipantLeftMessage(context.ParticipantId),
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    // The original broadcast remains isolated from secondary departure notifications.
                }
            }
        }
    }

    private static Task SendAsync(PeerContext context, PartyMessage message, CancellationToken cancellationToken) =>
        context.Transport.SendAsync(ProtocolJson.Serialize(message), cancellationToken);

    private static PositionMessage ToMessage(Guid id, string name, PartyPosition position) => new(
        id,
        name,
        position.X,
        position.Y,
        position.Z,
        position.ForwardX,
        position.ForwardZ,
        position.CapturedAt,
        position.MapName);

    private static bool IsFinite(PositionMessage position) =>
        double.IsFinite(position.X)
        && double.IsFinite(position.Y)
        && double.IsFinite(position.Z)
        && (!position.ForwardX.HasValue || double.IsFinite(position.ForwardX.Value))
        && (!position.ForwardZ.HasValue || double.IsFinite(position.ForwardZ.Value));

    private static void ValidatePosition(PartyPosition position)
    {
        if (!double.IsFinite(position.X)
            || !double.IsFinite(position.Y)
            || !double.IsFinite(position.Z)
            || (position.ForwardX.HasValue && !double.IsFinite(position.ForwardX.Value))
            || (position.ForwardZ.HasValue && !double.IsFinite(position.ForwardZ.Value)))
        {
            throw new ArgumentException("Position values must be finite.", nameof(position));
        }
    }

    private PartyCoordinatorState BuildState() => new(
        PartyCoordinatorRole.Host,
        roomCode,
        mapName,
        hostId,
        hostDisplayName,
        session.Participants);

    private void PublishState()
    {
        State = BuildState();
        StateChanged?.Invoke(State);
    }

    private sealed class PeerContext : IDisposable
    {
        private readonly SemaphoreSlim serialGate = new(1, 1);
        private readonly CancellationTokenSource helloTimeout = new();
        private Action<string>? messageHandler;
        private Action? disconnectedHandler;
        private int disposed;

        public PeerContext(Guid participantId, IPeerTransport transport, bool rejectionOnly)
        {
            ParticipantId = participantId;
            Transport = transport;
            RejectionOnly = rejectionOnly;
        }

        public Guid ParticipantId { get; }

        public IPeerTransport Transport { get; }

        public bool RejectionOnly { get; }

        public bool Accepted { get; set; }

        public CancellationToken HelloTimeoutToken => helloTimeout.Token;

        public void Attach(Action<string> onMessage, Action onDisconnected)
        {
            messageHandler = onMessage;
            disconnectedHandler = onDisconnected;
            Transport.MessageReceived += onMessage;
            Transport.Disconnected += onDisconnected;
        }

        public async Task ExecuteAsync(Func<Task> action)
        {
            await serialGate.WaitAsync().ConfigureAwait(false);
            try
            {
                await action().ConfigureAwait(false);
            }
            finally
            {
                serialGate.Release();
            }
        }

        public void CancelHelloTimeout()
        {
            if (!helloTimeout.IsCancellationRequested)
            {
                helloTimeout.Cancel();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            CancelHelloTimeout();
            if (messageHandler is not null)
            {
                Transport.MessageReceived -= messageHandler;
            }

            if (disconnectedHandler is not null)
            {
                Transport.Disconnected -= disconnectedHandler;
            }

            Transport.Dispose();
            helloTimeout.Dispose();
        }
    }
}
