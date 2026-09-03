using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Session;
using EftSsNavi.Sharing.Transport;

namespace EftSsNavi.Sharing.Coordination;

public sealed class PartyClient : IAsyncDisposable
{
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(30);
    private readonly Guid participantId;
    private readonly string requestedDisplayName;
    private readonly string roomCode;
    private readonly IPartySignaling signaling;
    private readonly IPeerTransport transport;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim messageGate = new(1, 1);
    private readonly object stateSync = new();
    private readonly TaskCompletionSource<WelcomeMessage> welcome =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int closed;
    private int signalingDisposed;

    public PartyClient(
        Guid participantId,
        string displayName,
        string roomCode,
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

        var normalizedDisplayName = displayName?.Trim();
        if (string.IsNullOrEmpty(normalizedDisplayName) || normalizedDisplayName.Length > 16)
        {
            throw new ArgumentException("The display name must contain 1 to 16 characters.", nameof(displayName));
        }

        this.participantId = participantId;
        requestedDisplayName = normalizedDisplayName;
        this.roomCode = roomCode;
        this.signaling = signaling;
        this.timeProvider = timeProvider;
        transport = peerFactory.Create(participantId);
        transport.MessageReceived += OnMessageReceived;
        transport.Disconnected += OnDisconnected;
        State = PartyCoordinatorState.Empty;
    }

    public event Action<PartyCoordinatorState>? StateChanged;

    public PartyCoordinatorState State { get; private set; }

    public async Task JoinAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref closed) != 0, this);
        using var timeoutCancellation = new CancellationTokenSource(JoinTimeout, timeProvider);
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);
        try
        {
            await JoinCoreAsync(operationCancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException error) when (
            timeoutCancellation.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            CloseAndClear();
            throw new TimeoutException("The party join operation timed out.", error);
        }
        catch (TimeoutException)
        {
            CloseAndClear();
            throw;
        }
        catch
        {
            CloseAndClear();
            throw;
        }
    }

    public async Task SendPositionAsync(
        PartyPosition position,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(position);
        if (State.Role != PartyCoordinatorRole.Participant
            || State.LocalParticipantId is not { } localId
            || State.LocalDisplayName is not { } localName)
        {
            return;
        }

        var message = new PositionMessage(
            localId,
            localName,
            position.X,
            position.Y,
            position.Z,
            position.ForwardX,
            position.ForwardZ,
            position.CapturedAt,
            position.MapName);
        if (!IsFinite(message))
        {
            throw new ArgumentException("Position values must be finite.", nameof(position));
        }

        await transport.SendAsync(ProtocolJson.Serialize(message), cancellationToken).ConfigureAwait(false);
    }

    public Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        CloseAndClear();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        CloseAndClear();
        if (signaling is IAsyncDisposable asyncDisposable
            && Interlocked.Exchange(ref signalingDisposed, 1) == 0)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task JoinCoreAsync(CancellationToken cancellationToken)
    {
        var offer = await transport.CreateOfferAsync(cancellationToken).ConfigureAwait(false);
        var answer = await signaling.ExchangeOfferAsync(
            roomCode,
            participantId,
            offer,
            cancellationToken).ConfigureAwait(false);
        await transport.ApplyAnswerAsync(answer, cancellationToken).ConfigureAwait(false);
        await transport.WaitUntilConnectedAsync(cancellationToken).ConfigureAwait(false);
        await transport.SendAsync(
            ProtocolJson.Serialize(new HelloMessage(requestedDisplayName, ProtocolJson.CurrentVersion)),
            cancellationToken).ConfigureAwait(false);
        await welcome.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private void OnMessageReceived(string json)
    {
        try
        {
            HandleMessageAsync(json).GetAwaiter().GetResult();
        }
        catch
        {
            // Invalid or failed peer work is isolated from the transport callback thread.
        }
    }

    private void OnDisconnected() => CloseAndClear();

    private async Task HandleMessageAsync(string json)
    {
        if (!ProtocolJson.TryDeserialize(json, out var message) || message is null)
        {
            return;
        }

        await messageGate.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (stateSync)
            {
                if (Volatile.Read(ref closed) != 0)
                {
                    return;
                }

                switch (message)
                {
                    case WelcomeMessage receivedWelcome when receivedWelcome.ParticipantId == participantId:
                        State = new PartyCoordinatorState(
                            PartyCoordinatorRole.Participant,
                            roomCode,
                            receivedWelcome.MapName,
                            participantId,
                            receivedWelcome.DisplayName,
                            receivedWelcome.Participants.Select(ToSessionParticipant).ToArray());
                        welcome.TrySetResult(receivedWelcome);
                        StateChanged?.Invoke(State);
                        break;
                    case RejectMessage reject:
                        welcome.TrySetException(new PartyRejectedException(reject.Reason));
                        break;
                    case ParticipantJoinedMessage joined when State.Role == PartyCoordinatorRole.Participant:
                        if (State.Participants.All(item => item.Id != joined.Participant.Id))
                        {
                            State = State with
                            {
                                Participants = State.Participants
                                    .Append(ToSessionParticipant(joined.Participant))
                                    .ToArray(),
                            };
                            StateChanged?.Invoke(State);
                        }

                        break;
                    case ParticipantLeftMessage left when State.Role == PartyCoordinatorRole.Participant:
                        State = State with
                        {
                            Participants = State.Participants
                                .Where(item => item.Id != left.ParticipantId)
                                .ToArray(),
                        };
                        StateChanged?.Invoke(State);
                        break;
                    case PositionMessage position when State.Role == PartyCoordinatorRole.Participant && IsFinite(position):
                        UpdatePosition(position);
                        break;
                    case MapChangedMessage mapChanged when State.Role == PartyCoordinatorRole.Participant:
                        State = State with { MapName = mapChanged.MapName };
                        StateChanged?.Invoke(State);
                        break;
                    case GoodbyeMessage:
                        CloseAndClear();
                        break;
                }
            }
        }
        finally
        {
            messageGate.Release();
        }
    }

    private void UpdatePosition(PositionMessage position)
    {
        var index = State.Participants.ToList().FindIndex(item => item.Id == position.ParticipantId);
        if (index < 0)
        {
            return;
        }

        var participants = State.Participants.ToArray();
        var existing = participants[index];
        participants[index] = existing with
        {
            LatestPosition = new PartyPosition(
                position.X,
                position.Y,
                position.Z,
                position.ForwardX,
                position.ForwardZ,
                position.CapturedAt,
                position.MapName),
            PositionReceivedAt = timeProvider.GetUtcNow(),
        };
        State = State with { Participants = participants };
        StateChanged?.Invoke(State);
    }

    private void CloseAndClear()
    {
        if (Interlocked.Exchange(ref closed, 1) != 0)
        {
            return;
        }

        lock (stateSync)
        {
            transport.MessageReceived -= OnMessageReceived;
            transport.Disconnected -= OnDisconnected;
            transport.Dispose();
            welcome.TrySetCanceled();
            State = PartyCoordinatorState.Empty;
            StateChanged?.Invoke(State);
        }
    }

    private static SessionParticipant ToSessionParticipant(PartyParticipant participant) => new(
        participant.Id,
        participant.DisplayName,
        participant.ColorIndex);

    private static bool IsFinite(PositionMessage position) =>
        double.IsFinite(position.X)
        && double.IsFinite(position.Y)
        && double.IsFinite(position.Z)
        && (!position.ForwardX.HasValue || double.IsFinite(position.ForwardX.Value))
        && (!position.ForwardZ.HasValue || double.IsFinite(position.ForwardZ.Value));
}
