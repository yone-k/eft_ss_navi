namespace EftSsNavi.Sharing.Transport;

public sealed class WebRtcPeerTransport : IPeerTransport
{
    public static readonly TimeSpan DefaultConnectionTimeout = TimeSpan.FromSeconds(30);

    private const string DataChannelLabel = "eftssnavi-party";
    private readonly IWebRtcPeer _peer;
    private readonly TimeSpan _connectionTimeout;
    private readonly SemaphoreSlim _negotiationLock = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private int _disconnectedRaised;
    private int _disposed;

    public WebRtcPeerTransport(IReadOnlyList<string> stunServers)
        : this(stunServers, new SipsorceryPeerFactory(), DefaultConnectionTimeout)
    {
    }

    internal WebRtcPeerTransport(
        IReadOnlyList<string> stunServers,
        IWebRtcPeerFactory peerFactory,
        TimeSpan connectionTimeout)
    {
        ArgumentNullException.ThrowIfNull(stunServers);
        ArgumentNullException.ThrowIfNull(peerFactory);
        if (connectionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(connectionTimeout));
        }

        _connectionTimeout = connectionTimeout;
        _peer = peerFactory.Create(stunServers.ToArray());
        _peer.MessageReceived += OnMessageReceived;
        _peer.Disconnected += OnDisconnected;
    }

    public event Action<string>? MessageReceived;

    public event Action? Disconnected;

    public Task<string> CreateOfferAsync(CancellationToken cancellationToken) =>
        ExecuteWithTimeoutAsync(CreateOfferCoreAsync, cancellationToken);

    public Task<string> CreateAnswerAsync(string remoteOffer, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteOffer);
        return ExecuteWithTimeoutAsync(
            token => CreateAnswerCoreAsync(remoteOffer, token),
            cancellationToken);
    }

    public Task ApplyAnswerAsync(string remoteAnswer, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteAnswer);
        return ExecuteWithTimeoutAsync<object?>(
            async token =>
            {
                await _peer.SetRemoteDescriptionAsync(
                    new WebRtcSessionDescription(WebRtcSessionDescriptionType.Answer, remoteAnswer),
                    token).ConfigureAwait(false);
                return null;
            },
            cancellationToken);
    }

    public Task WaitUntilConnectedAsync(CancellationToken cancellationToken) =>
        ExecuteWithTimeoutAsync<object?>(
            async token =>
            {
                var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                void OnDataChannelOpened() => completion.TrySetResult();
                void OnPeerDisconnected() => completion.TrySetException(
                    new InvalidOperationException("The WebRTC peer disconnected before its data channel opened."));

                _peer.DataChannelOpened += OnDataChannelOpened;
                _peer.Disconnected += OnPeerDisconnected;
                try
                {
                    if (_peer.IsDisconnected)
                    {
                        OnPeerDisconnected();
                    }
                    else if (_peer.IsDataChannelOpen)
                    {
                        completion.TrySetResult();
                    }

                    await completion.Task.WaitAsync(token).ConfigureAwait(false);
                    return null;
                }
                finally
                {
                    _peer.DataChannelOpened -= OnDataChannelOpened;
                    _peer.Disconnected -= OnPeerDisconnected;
                }
            },
            cancellationToken);

    public Task SendAsync(string message, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        if (!_peer.IsDataChannelOpen)
        {
            throw new InvalidOperationException("The WebRTC data channel is not open.");
        }

        return _peer.SendAsync(message, cancellationToken);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _lifetimeCancellation.Cancel();
        _peer.MessageReceived -= OnMessageReceived;
        _peer.Disconnected -= OnDisconnected;
        _peer.Dispose();
    }

    private async Task<string> CreateOfferCoreAsync(CancellationToken cancellationToken)
    {
        await _peer.CreateDataChannelAsync(
            DataChannelLabel,
            ordered: true,
            reliable: true,
            cancellationToken).ConfigureAwait(false);

        var offer = _peer.CreateOffer();
        await SetLocalDescriptionAndWaitForIceAsync(offer, cancellationToken).ConfigureAwait(false);
        return _peer.LocalDescriptionSdp;
    }

    private async Task<string> CreateAnswerCoreAsync(
        string remoteOffer,
        CancellationToken cancellationToken)
    {
        await _peer.SetRemoteDescriptionAsync(
            new WebRtcSessionDescription(WebRtcSessionDescriptionType.Offer, remoteOffer),
            cancellationToken).ConfigureAwait(false);

        var answer = _peer.CreateAnswer();
        await SetLocalDescriptionAndWaitForIceAsync(answer, cancellationToken).ConfigureAwait(false);
        return _peer.LocalDescriptionSdp;
    }

    private async Task SetLocalDescriptionAndWaitForIceAsync(
        WebRtcSessionDescription description,
        CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnIceGatheringCompleted() => completion.TrySetResult();

        _peer.IceGatheringCompleted += OnIceGatheringCompleted;
        try
        {
            await _peer.SetLocalDescriptionAsync(description, cancellationToken).ConfigureAwait(false);
            await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _peer.IceGatheringCompleted -= OnIceGatheringCompleted;
        }
    }

    private async Task<T> ExecuteWithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        using var timeout = new CancellationTokenSource(_connectionTimeout);
        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCancellation.Token,
            timeout.Token);

        try
        {
            await _negotiationLock.WaitAsync(operationCancellation.Token).ConfigureAwait(false);
            try
            {
                return await operation(operationCancellation.Token)
                    .WaitAsync(operationCancellation.Token)
                    .ConfigureAwait(false);
            }
            finally
            {
                _negotiationLock.Release();
            }
        }
        catch (OperationCanceledException) when (
            timeout.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested
            && !_lifetimeCancellation.IsCancellationRequested)
        {
            Dispose();
            throw new TimeoutException($"WebRTC negotiation exceeded {_connectionTimeout.TotalSeconds:0.###} seconds.");
        }
        catch (OperationCanceledException)
        {
            Dispose();
            throw;
        }
    }

    private void OnMessageReceived(string message)
    {
        if (Volatile.Read(ref _disposed) == 0)
        {
            MessageReceived?.Invoke(message);
        }
    }

    private void OnDisconnected()
    {
        if (Volatile.Read(ref _disposed) == 0
            && Interlocked.Exchange(ref _disconnectedRaised, 1) == 0)
        {
            Disconnected?.Invoke();
        }
    }
}
