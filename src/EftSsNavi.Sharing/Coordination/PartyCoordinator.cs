using EftSsNavi.Sharing.Protocol;
using EftSsNavi.Sharing.Session;

namespace EftSsNavi.Sharing.Coordination;

public sealed class PartyCoordinator : IPartyCoordinator
{
    private readonly Func<IPartySignaling> signalingFactory;
    private readonly IPeerTransportFactory peerFactory;
    private readonly TimeProvider timeProvider;
    private readonly Func<Guid> participantIdFactory;
    private readonly Func<string> roomCodeFactory;
    private readonly SemaphoreSlim gate = new(1, 1);
    private PartyHost? host;
    private PartyClient? client;
    private Action<PartyCoordinatorState>? hostStateHandler;
    private Action<PartyCoordinatorState>? clientStateHandler;
    private long activeGeneration;
    private int disposed;

    public PartyCoordinator(
        Func<IPartySignaling> signalingFactory,
        IPeerTransportFactory peerFactory,
        TimeProvider timeProvider,
        Func<Guid>? participantIdFactory = null,
        Func<string>? roomCodeFactory = null)
    {
        ArgumentNullException.ThrowIfNull(signalingFactory);
        ArgumentNullException.ThrowIfNull(peerFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.signalingFactory = signalingFactory;
        this.peerFactory = peerFactory;
        this.timeProvider = timeProvider;
        this.participantIdFactory = participantIdFactory ?? Guid.NewGuid;
        this.roomCodeFactory = roomCodeFactory ?? RoomCode.Generate;
    }

    public event Action<PartyCoordinatorState>? StateChanged;

    public PartyCoordinatorState State { get; private set; } = PartyCoordinatorState.Empty;

    public async Task StartHostAsync(
        string displayName,
        string? mapName,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ClearActiveAsync(cancellationToken).ConfigureAwait(false);
            var signaling = signalingFactory();
            PartyHost created;
            try
            {
                created = new PartyHost(
                    participantIdFactory(),
                    displayName,
                    roomCodeFactory(),
                    mapName,
                    signaling,
                    peerFactory,
                    timeProvider);
            }
            catch
            {
                await DisposeSignalingIgnoringFailureAsync(signaling).ConfigureAwait(false);
                throw;
            }

            var generation = Interlocked.Increment(ref activeGeneration);
            Action<PartyCoordinatorState> handler = state => ApplyState(generation, state);
            hostStateHandler = handler;
            created.StateChanged += handler;
            host = created;
            try
            {
                await created.StartAsync(cancellationToken).ConfigureAwait(false);
                ApplyState(generation, created.State);
            }
            catch (Exception error)
            {
                Interlocked.Increment(ref activeGeneration);
                created.StateChanged -= handler;
                host = null;
                hostStateHandler = null;
                ApplyState(PartyCoordinatorState.Empty);
                await DisposeHostPreservingFailureAsync(created, error).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task JoinAsync(
        string displayName,
        string roomCode,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ClearActiveAsync(cancellationToken).ConfigureAwait(false);
            var signaling = signalingFactory();
            PartyClient created;
            try
            {
                created = new PartyClient(
                    participantIdFactory(),
                    displayName,
                    roomCode,
                    signaling,
                    peerFactory,
                    timeProvider);
            }
            catch
            {
                await DisposeSignalingIgnoringFailureAsync(signaling).ConfigureAwait(false);
                throw;
            }

            var generation = Interlocked.Increment(ref activeGeneration);
            Action<PartyCoordinatorState> handler = state => ApplyState(generation, state);
            clientStateHandler = handler;
            created.StateChanged += handler;
            client = created;
            try
            {
                await created.JoinAsync(cancellationToken).ConfigureAwait(false);
                ApplyState(generation, created.State);
            }
            catch (Exception error)
            {
                Interlocked.Increment(ref activeGeneration);
                created.StateChanged -= handler;
                client = null;
                clientStateHandler = null;
                ApplyState(PartyCoordinatorState.Empty);
                await DisposeClientPreservingFailureAsync(created, error).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<string> ReissueRoomCodeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var activeHost = host ?? throw new InvalidOperationException("Only a host can reissue a room code.");
            var code = roomCodeFactory();
            await activeHost.ReissueRoomCodeAsync(code, cancellationToken).ConfigureAwait(false);
            return code;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SendPositionAsync(PartyPosition position, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (host is not null)
            {
                await host.SendPositionAsync(position, cancellationToken).ConfigureAwait(false);
            }
            else if (client is not null)
            {
                await client.SendPositionAsync(position, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task ChangeMapAsync(string? mapName, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (host is not null)
            {
                await host.ChangeMapAsync(mapName, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task LeaveAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (client is not null)
            {
                var activeClient = client;
                client = null;
                Interlocked.Increment(ref activeGeneration);
                if (clientStateHandler is not null)
                {
                    activeClient.StateChanged -= clientStateHandler;
                    clientStateHandler = null;
                }

                Exception? leaveError = null;
                try
                {
                    await activeClient.LeaveAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    leaveError = error;
                }

                try
                {
                    await activeClient.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception error) when (leaveError is null)
                {
                    leaveError = error;
                }
                catch
                {
                    // Preserve the Leave failure after disposal was attempted.
                }

                ApplyState(PartyCoordinatorState.Empty);
                ApplyFailure(leaveError);
            }

            ApplyState(PartyCoordinatorState.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task EndAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        try
        {
            if (host is not null)
            {
                var activeHost = host;
                host = null;
                Interlocked.Increment(ref activeGeneration);
                if (hostStateHandler is not null)
                {
                    activeHost.StateChanged -= hostStateHandler;
                    hostStateHandler = null;
                }

                Exception? endError = null;
                try
                {
                    await activeHost.EndAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception error)
                {
                    endError = error;
                }

                try
                {
                    await activeHost.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception error) when (endError is null)
                {
                    endError = error;
                }
                catch
                {
                    // Preserve the End failure after disposal was attempted.
                }

                ApplyState(PartyCoordinatorState.Empty);
                ApplyFailure(endError);
            }

            ApplyState(PartyCoordinatorState.Empty);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await ClearActiveAsync(CancellationToken.None).ConfigureAwait(false);
            ApplyState(PartyCoordinatorState.Empty);
        }
        finally
        {
            gate.Release();
            gate.Dispose();
        }
    }

    private async Task ClearActiveAsync(CancellationToken cancellationToken)
    {
        Exception? cleanupError = null;
        Interlocked.Increment(ref activeGeneration);
        if (host is not null)
        {
            var activeHost = host;
            host = null;
            if (hostStateHandler is not null)
            {
                activeHost.StateChanged -= hostStateHandler;
                hostStateHandler = null;
            }

            try
            {
                await activeHost.EndAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                cleanupError = error;
            }

            try
            {
                await activeHost.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception error) when (cleanupError is null)
            {
                cleanupError = error;
            }
            catch
            {
                // Preserve the first cleanup failure.
            }
        }

        if (client is not null)
        {
            var activeClient = client;
            client = null;
            if (clientStateHandler is not null)
            {
                activeClient.StateChanged -= clientStateHandler;
                clientStateHandler = null;
            }

            try
            {
                await activeClient.LeaveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (cleanupError is null)
            {
                cleanupError = error;
            }
            catch
            {
                // Preserve the first cleanup failure.
            }

            try
            {
                await activeClient.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception error) when (cleanupError is null)
            {
                cleanupError = error;
            }
            catch
            {
                // Preserve the first cleanup failure.
            }
        }

        ApplyState(PartyCoordinatorState.Empty);
        if (cleanupError is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanupError).Throw();
        }
    }

    private void ApplyState(long generation, PartyCoordinatorState state)
    {
        if (generation == Volatile.Read(ref activeGeneration))
        {
            ApplyState(state);
        }
    }

    private void ApplyState(PartyCoordinatorState state)
    {
        State = state;
        foreach (var handler in StateChanged?.GetInvocationList().Cast<Action<PartyCoordinatorState>>() ?? [])
        {
            try
            {
                handler(state);
            }
            catch
            {
                // Application event failures must not interrupt network lifecycle cleanup.
            }
        }
    }

    private static async Task DisposeHostPreservingFailureAsync(PartyHost host, Exception? originalError)
    {
        try
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception error) when (originalError is null)
        {
            originalError = error;
        }
        catch
        {
            // Preserve the operation failure that initiated cleanup.
        }

        ApplyFailure(originalError);
    }

    private static async Task DisposeClientPreservingFailureAsync(PartyClient client, Exception? originalError)
    {
        try
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception error) when (originalError is null)
        {
            originalError = error;
        }
        catch
        {
            // Preserve the operation failure that initiated cleanup.
        }

        ApplyFailure(originalError);
    }

    private static async Task DisposeSignalingIgnoringFailureAsync(IPartySignaling signaling)
    {
        if (signaling is IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch
            {
                // Constructor failure remains the actionable error.
            }
        }
    }

    private static void ApplyFailure(Exception? error)
    {
        if (error is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(
        Volatile.Read(ref disposed) != 0,
        this);
}
